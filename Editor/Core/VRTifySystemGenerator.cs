using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRTify.Data;
using VRTify.Editor.Generators;
using VRTify.Editor.Icons;
using VRTify.Editor.Utils;

namespace VRTify.Core
{
    /// <summary>
    /// Main system generator that orchestrates the complete VRTify setup process
    /// </summary>
    public static class VRTifySystemGenerator
    {
        /// <summary>
        /// Generates complete VRTify notification system on avatar
        /// </summary>
        public static bool GenerateCompleteSystem(VRCAvatarDescriptor avatar, List<NotificationApp> enabledApps, VRTifySettings settings, VRTifyAvatarSetup.HUDPosition hudPosition = VRTifyAvatarSetup.HUDPosition.BottomCenter)
        {
            if (avatar == null)
            {
                Debug.LogError("VRTify: No avatar selected!");
                return false;
            }

            if (enabledApps == null || !enabledApps.Any())
            {
                Debug.LogError("VRTify: No apps enabled for notification system!");
                return false;
            }

            try
            {
                EditorUtility.DisplayProgressBar("VRTify", "Starting system generation...", 0f);

                var generationProgress = new VRTifyGenerationProgress();
                generationProgress.UpdateProgress("Initializing", 0.1f);

                // Step 1: Validate inputs
                if (!ValidateInputs(avatar, enabledApps, settings))
                {
                    EditorUtility.ClearProgressBar();
                    return false;
                }
                generationProgress.CompleteStep("Validation");
                EditorUtility.DisplayProgressBar("VRTify", "Validation complete", 0.2f);

                // Step 2: Backup existing configuration
                CreateBackup(avatar);
                generationProgress.CompleteStep("Backup");
                EditorUtility.DisplayProgressBar("VRTify", "Backup created", 0.3f);

                // Step 3: Extract and process icons
                var iconExtractor = new VRTifyIconExtractor();
                var processedApps = ProcessAppIcons(iconExtractor, enabledApps, settings);
                generationProgress.CompleteStep("Icon Processing");
                EditorUtility.DisplayProgressBar("VRTify", "Icons processed", 0.5f);

                // Step 4: Setup avatar HUD system
                var hudObject = VRTifyAvatarSetup.SetupVRTifyOnAvatar(avatar, processedApps, settings, hudPosition);
                if (hudObject == null)
                {
                    throw new System.Exception("Failed to create HUD object");
                }
                generationProgress.CompleteStep("HUD Setup");
                EditorUtility.DisplayProgressBar("VRTify", "HUD system created", 0.7f);

                // Step 5: Setup animator system
                var animatorSetup = new VRTifyAnimatorSetup();
                var animatorController = animatorSetup.SetupAnimator(avatar, processedApps, settings);
                if (animatorController == null)
                {
                    throw new System.Exception("Failed to setup animator");
                }
                generationProgress.CompleteStep("Animator Setup");
                EditorUtility.DisplayProgressBar("VRTify", "Animator configured", 0.9f);

                // Step 6: Finalize and save
                FinalizeSetup(avatar, processedApps, settings, hudObject, animatorController);
                generationProgress.CompleteStep("Finalization");
                generationProgress.isComplete = true;

                EditorUtility.ClearProgressBar();

                // Show success dialog
                ShowSuccessDialog(avatar, processedApps, hudObject);

                Debug.Log($"✅ VRTify system generation completed successfully!");
                Debug.Log($"📊 Generated for {processedApps.Count} applications on avatar '{avatar.name}'");

                return true;
            }
            catch (System.Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"❌ VRTify system generation failed: {e.Message}");

                ShowErrorDialog(e.Message);
                return false;
            }
        }

        /// <summary>
        /// Validates all inputs before generation
        /// </summary>
        private static bool ValidateInputs(VRCAvatarDescriptor avatar, List<NotificationApp> apps, VRTifySettings settings)
        {
            // Validate avatar
            if (avatar == null)
            {
                Debug.LogError("VRTify: Avatar is null");
                return false;
            }

            if (avatar.transform == null)
            {
                Debug.LogError("VRTify: Avatar transform is null");
                return false;
            }

            // Validate apps
            if (apps == null || apps.Count == 0)
            {
                Debug.LogError("VRTify: No apps provided");
                return false;
            }

            var invalidApps = apps.Where(app => !app.IsValid()).ToList();
            if (invalidApps.Any())
            {
                Debug.LogWarning($"VRTify: Found {invalidApps.Count} invalid apps, they will be skipped");
                foreach (var app in invalidApps)
                {
                    Debug.LogWarning($"VRTify: Invalid app: {app.appName} - {app.displayName}");
                }
            }

            // Validate settings
            if (settings == null)
            {
                Debug.LogError("VRTify: Settings is null");
                return false;
            }

            if (!settings.ValidateSettings())
            {
                Debug.LogError("VRTify: Settings validation failed");
                return false;
            }

            // Check for existing VRTify setup
            var existingHUD = avatar.transform.GetComponentsInChildren<Transform>()
                .FirstOrDefault(t => t.name.Contains("VRTify"));

            if (existingHUD != null)
            {
                bool shouldReplace = EditorUtility.DisplayDialog(
                    "VRTify Setup Found",
                    $"Found existing VRTify setup on avatar '{avatar.name}'.\n\nDo you want to replace it?",
                    "Replace", "Cancel"
                );

                if (!shouldReplace)
                {
                    Debug.Log("VRTify: Generation cancelled by user");
                    return false;
                }

                // Clean up existing setup
                CleanupExistingSetup(avatar);
            }

            return true;
        }

        /// <summary>
        /// Processes application icons and validates them
        /// </summary>
        private static List<NotificationApp> ProcessAppIcons(VRTifyIconExtractor iconExtractor, List<NotificationApp> apps, VRTifySettings settings)
        {
            var processedApps = new List<NotificationApp>();

            foreach (var app in apps.Where(a => a.enabled && a.IsValid()))
            {
                try
                {
                    // Try to extract icon if path is provided
                    if (!string.IsNullOrEmpty(app.iconPath))
                    {
                        var iconResult = iconExtractor.GetIconExtractionResult(app.iconPath);
                        if (iconResult.success)
                        {
                            processedApps.Add(app);
                            Debug.Log($"✓ Processed icon for {app.displayName}");
                        }
                        else
                        {
                            Debug.LogWarning($"⚠ Failed to extract icon for {app.displayName}: {iconResult.errorMessage}");

                            // Add app anyway but without icon
                            app.iconPath = "";
                            app.useCustomIcon = false;
                            processedApps.Add(app);
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"⚠ No icon path for {app.displayName}, using default");
                        processedApps.Add(app);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"❌ Error processing {app.displayName}: {e.Message}");
                }
            }

            if (processedApps.Count == 0)
            {
                throw new System.Exception("No valid apps could be processed");
            }

            // Reassign priorities to ensure sequential order
            for (int i = 0; i < processedApps.Count; i++)
            {
                processedApps[i].priority = i;
            }

            return processedApps;
        }

        /// <summary>
        /// Creates backup of current avatar configuration
        /// </summary>
        private static void CreateBackup(VRCAvatarDescriptor avatar)
        {
            try
            {
                VRTifySettingsManager.CreateSettingsBackup($"Avatar_{avatar.name}_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}");

                // Also backup avatar's current FX controller if it exists
                var fxLayer = avatar.baseAnimationLayers?.FirstOrDefault(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX);
                if (fxLayer.HasValue && fxLayer.Value.animatorController != null)
                {
                    var controllerPath = AssetDatabase.GetAssetPath(fxLayer.Value.animatorController);
                    if (!string.IsNullOrEmpty(controllerPath))
                    {
                        Debug.Log($"💾 Existing FX controller backed up: {controllerPath}");
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"⚠ Backup creation failed: {e.Message}");
            }
        }

        /// <summary>
        /// Cleans up existing VRTify setup
        /// </summary>
        private static void CleanupExistingSetup(VRCAvatarDescriptor avatar)
        {
            try
            {
                // Remove existing VRTify GameObjects
                var existingObjects = avatar.transform.GetComponentsInChildren<Transform>()
                    .Where(t => t.name.Contains("VRTify"))
                    .Select(t => t.gameObject)
                    .ToList();

                foreach (var obj in existingObjects)
                {
                    if (obj != avatar.gameObject) // Don't delete the avatar itself
                    {
                        Object.DestroyImmediate(obj);
                        Debug.Log($"🗑️ Removed existing VRTify object: {obj.name}");
                    }
                }

                // Clean up animator layers
                var fxLayer = avatar.baseAnimationLayers?.FirstOrDefault(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX);
                if (fxLayer.HasValue && fxLayer.Value.animatorController is UnityEditor.Animations.AnimatorController controller)
                {
                    var vrtifyLayers = controller.layers.Where(l => l.name.Contains("VRTify")).ToList();
                    foreach (var layer in vrtifyLayers)
                    {
                        var layerIndex = System.Array.IndexOf(controller.layers, layer);
                        if (layerIndex >= 0)
                        {
                            controller.RemoveLayer(layerIndex);
                            Debug.Log($"🗑️ Removed existing VRTify animator layer: {layer.name}");
                        }
                    }
                }

                Debug.Log("🧹 Cleanup of existing VRTify setup completed");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"⚠ Cleanup warning: {e.Message}");
            }
        }

        /// <summary>
        /// Finalizes the setup and saves all assets
        /// </summary>
        private static void FinalizeSetup(VRCAvatarDescriptor avatar, List<NotificationApp> apps, VRTifySettings settings, GameObject hudObject, UnityEditor.Animations.AnimatorController animatorController)
        {
            try
            {
                // Save updated settings
                VRTifySettingsManager.SaveSettings(settings, apps);

                // Update system status
                var systemStatus = new VRTifySystemStatus
                {
                    isInitialized = true,
                    hasValidAvatar = true,
                    hasValidApps = true,
                    enabledAppsCount = apps.Count,
                    lastGeneration = System.DateTime.Now
                };
                VRTifySettingsManager.UpdateSystemStatus(systemStatus);

                // Mark avatar as dirty for saving
                EditorUtility.SetDirty(avatar);
                EditorUtility.SetDirty(hudObject);
                EditorUtility.SetDirty(animatorController);

                // Save all assets
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log("💾 All assets saved successfully");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"⚠ Finalization warning: {e.Message}");
            }
        }

        /// <summary>
        /// Shows success dialog with next steps
        /// </summary>
        private static void ShowSuccessDialog(VRCAvatarDescriptor avatar, List<NotificationApp> apps, GameObject hudObject)
        {
            var message = $"✅ VRTify Setup Complete!\n\n" +
                         $"Avatar: {avatar.name}\n" +
                         $"Applications: {apps.Count}\n" +
                         $"HUD Object: {hudObject.name}\n\n" +
                         $"Next Steps:\n" +
                         $"1. Test your avatar in Play Mode\n" +
                         $"2. Upload your avatar to VRChat\n" +
                         $"3. Enable OSC in VRChat settings\n" +
                         $"4. Download and run VRTify OSC Monitor\n\n" +
                         $"OSC Parameters created:\n";

            foreach (var app in apps.Take(5)) // Show first 5
            {
                message += $"• {app.oscParameter}\n";
            }

            if (apps.Count > 5)
            {
                message += $"• ... and {apps.Count - 5} more\n";
            }

            EditorUtility.DisplayDialog("VRTify Generation Complete", message, "Awesome!");
        }

        /// <summary>
        /// Shows error dialog with troubleshooting info
        /// </summary>
        private static void ShowErrorDialog(string errorMessage)
        {
            var message = $"❌ VRTify Generation Failed\n\n" +
                         $"Error: {errorMessage}\n\n" +
                         $"Troubleshooting:\n" +
                         $"• Make sure avatar has valid hierarchy\n" +
                         $"• Check that apps have valid icon paths\n" +
                         $"• Verify VRChat SDK is properly installed\n" +
                         $"• Try with fewer applications first\n\n" +
                         $"Check Console for detailed error logs.";

            EditorUtility.DisplayDialog("VRTify Generation Error", message, "OK");
        }

        /// <summary>
        /// Validates VRChat SDK compatibility
        /// </summary>
        public static bool ValidateVRChatSDK()
        {
            try
            {
                // Check if VRChat SDK types are available
                var avatarDescriptorType = typeof(VRCAvatarDescriptor);
                var expressionParamsType = typeof(VRCExpressionParameters);

                if (avatarDescriptorType == null || expressionParamsType == null)
                {
                    Debug.LogError("VRTify: VRChat SDK3 not found! Please install VRChat SDK3 - Avatars");
                    return false;
                }

                Debug.Log("✓ VRChat SDK3 compatibility verified");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"VRTify: VRChat SDK validation failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets generation statistics for the current setup
        /// </summary>
        public static (int totalApps, int enabledApps, int withIcons, long totalSize) GetGenerationStats(List<NotificationApp> apps)
        {
            if (apps == null) return (0, 0, 0, 0);

            var totalApps = apps.Count;
            var enabledApps = apps.Count(a => a.enabled);
            var withIcons = apps.Count(a => !string.IsNullOrEmpty(a.iconPath));
            var totalSize = VRTifyFileManager.GetVRTifyDiskUsage();

            return (totalApps, enabledApps, withIcons, totalSize);
        }

        /// <summary>
        /// Performs system health check
        /// </summary>
        public static bool PerformSystemHealthCheck()
        {
            try
            {
                Debug.Log("🔍 Performing VRTify system health check...");

                // Check directory structure
                if (!VRTifyFileManager.ValidateDirectoryStructure())
                {
                    Debug.LogWarning("⚠ Directory structure validation failed, recreating...");
                    VRTifyFileManager.CreateDirectoryStructure();
                }

                // Check VRChat SDK
                if (!ValidateVRChatSDK())
                {
                    return false;
                }

                // Check Unity version
                var unityVersion = Application.unityVersion;
                if (!unityVersion.StartsWith("2022.3"))
                {
                    Debug.LogWarning($"⚠ VRTify is designed for Unity 2022.3.x (VRChat current). You're using {unityVersion}");
                }

                Debug.Log("✅ System health check passed");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ System health check failed: {e.Message}");
                return false;
            }
        }
    }
}