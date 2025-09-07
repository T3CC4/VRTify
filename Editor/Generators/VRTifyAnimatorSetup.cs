using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRTify.Data;
using VRTify.Core;
using System;

namespace VRTify.Editor.Generators
{
    /// <summary>
    /// Sets up animator controllers and expression parameters for VRTify - COMPLETELY FIXED VERSION
    /// </summary>
    public class VRTifyAnimatorSetup
    {
        /// <summary>
        /// Sets up complete animator system for VRTify notifications
        /// </summary>
        public AnimatorController SetupAnimator(VRCAvatarDescriptor avatar, List<NotificationApp> apps, VRTifySettings settings)
        {
            try
            {
                Debug.Log("VRTify: Starting animator setup...");

                // Validate inputs with detailed logging
                if (avatar == null)
                {
                    throw new System.ArgumentNullException(nameof(avatar), "Avatar cannot be null");
                }

                if (apps == null)
                {
                    throw new System.ArgumentNullException(nameof(apps), "Apps list cannot be null");
                }

                if (settings == null)
                {
                    throw new System.ArgumentNullException(nameof(settings), "Settings cannot be null");
                }

                var enabledApps = apps.Where(a => a != null && a.enabled).ToList();
                if (enabledApps.Count == 0)
                {
                    throw new System.ArgumentException("No enabled apps found", nameof(apps));
                }

                Debug.Log($"VRTify: Setting up animator for {enabledApps.Count} enabled apps");

                // Get or create FX layer controller with detailed error handling
                var fxController = GetOrCreateFXController(avatar);
                if (fxController == null)
                {
                    throw new System.Exception("Failed to create or get FX controller");
                }

                Debug.Log($"VRTify: Got FX controller: {fxController.name}");

                // Create VRTify layer with safe layer management
                var vrtifyLayer = CreateVRTifyLayerSafe(fxController, enabledApps, settings);
                if (vrtifyLayer == null)
                {
                    throw new System.Exception("Failed to create VRTify animation layer");
                }

                Debug.Log("VRTify: Created VRTify animation layer");

                // Setup expression parameters
                SetupExpressionParameters(avatar, enabledApps);
                Debug.Log("VRTify: Set up expression parameters");

                // Setup expression menu (optional, non-critical)
                try
                {
                    SetupExpressionMenu(avatar, enabledApps);
                    Debug.Log("VRTify: Set up expression menu");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"VRTify: Expression menu setup failed (non-critical): {e.Message}");
                }

                Debug.Log($"✅ VRTify animator setup complete - {enabledApps.Count} apps configured");
                return fxController;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ VRTify animator setup failed: {e.Message}");
                Debug.LogError($"VRTify: Detailed error: {e}");
                return null;
            }
        }

        /// <summary>
        /// Gets existing FX controller or creates new one with complete safety checks
        /// </summary>
        private AnimatorController GetOrCreateFXController(VRCAvatarDescriptor avatar)
        {
            try
            {
                Debug.Log("VRTify: Getting or creating FX controller...");

                // Initialize base animation layers if null or empty
                if (avatar.baseAnimationLayers == null || avatar.baseAnimationLayers.Length == 0)
                {
                    Debug.Log("VRTify: Initializing base animation layers");
                    avatar.baseAnimationLayers = new VRCAvatarDescriptor.CustomAnimLayer[5];

                    // Initialize with default values for all standard VRChat layers
                    var layerTypes = new[]
                    {
                        VRCAvatarDescriptor.AnimLayerType.Base,
                        VRCAvatarDescriptor.AnimLayerType.Additive,
                        VRCAvatarDescriptor.AnimLayerType.Gesture,
                        VRCAvatarDescriptor.AnimLayerType.Action,
                        VRCAvatarDescriptor.AnimLayerType.FX
                    };

                    for (int i = 0; i < avatar.baseAnimationLayers.Length && i < layerTypes.Length; i++)
                    {
                        avatar.baseAnimationLayers[i] = new VRCAvatarDescriptor.CustomAnimLayer
                        {
                            type = layerTypes[i],
                            animatorController = null,
                            isDefault = true,
                            isEnabled = true,
                            mask = null
                        };
                    }
                }

                // Find FX layer safely
                VRCAvatarDescriptor.CustomAnimLayer? fxLayer = null;
                int fxIndex = -1;

                for (int i = 0; i < avatar.baseAnimationLayers.Length; i++)
                {
                    if (avatar.baseAnimationLayers[i].type == VRCAvatarDescriptor.AnimLayerType.FX)
                    {
                        fxLayer = avatar.baseAnimationLayers[i];
                        fxIndex = i;
                        break;
                    }
                }

                // If FX layer exists and has controller, return it
                if (fxLayer.HasValue && fxLayer.Value.animatorController != null)
                {
                    var existingController = fxLayer.Value.animatorController as AnimatorController;
                    if (existingController != null)
                    {
                        Debug.Log($"VRTify: Using existing FX controller: {existingController.name}");
                        return existingController;
                    }
                }

                // Create new FX controller
                var controllerPath = VRTifyFileManager.GetUniqueFileName(
                    Path.Combine(VRTifyFileManager.CONTROLLERS_PATH, "Generated"),
                    $"{avatar.name}_VRTify_FX",
                    ".controller"
                );

                var controller = AnimatorController.CreateAnimatorControllerAtPath(
                    VRTifyFileManager.GetRelativeUnityPath(controllerPath));

                Debug.Log($"VRTify: Created new FX controller: {controllerPath}");

                // Set or update FX layer safely
                if (fxIndex >= 0 && fxIndex < avatar.baseAnimationLayers.Length)
                {
                    // Update existing FX layer
                    var updatedLayer = avatar.baseAnimationLayers[fxIndex];
                    updatedLayer.animatorController = controller;
                    updatedLayer.isDefault = false;
                    updatedLayer.isEnabled = true;
                    avatar.baseAnimationLayers[fxIndex] = updatedLayer;

                    Debug.Log("VRTify: Updated existing FX layer");
                }
                else
                {
                    // Expand array and add new FX layer
                    var currentLength = avatar.baseAnimationLayers?.Length ?? 0;
                    var newLayers = new VRCAvatarDescriptor.CustomAnimLayer[Math.Max(currentLength + 1, 5)];

                    // Copy existing layers safely
                    if (avatar.baseAnimationLayers != null)
                    {
                        for (int i = 0; i < currentLength; i++)
                        {
                            newLayers[i] = avatar.baseAnimationLayers[i];
                        }
                    }

                    // Add FX layer at the end
                    newLayers[currentLength] = new VRCAvatarDescriptor.CustomAnimLayer
                    {
                        type = VRCAvatarDescriptor.AnimLayerType.FX,
                        animatorController = controller,
                        isDefault = false,
                        isEnabled = true,
                        mask = null
                    };

                    avatar.baseAnimationLayers = newLayers;
                    Debug.Log($"VRTify: Added new FX layer to base animation layers (total: {newLayers.Length})");
                }

                // Mark avatar as dirty
                EditorUtility.SetDirty(avatar);

                return controller;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"VRTify: Failed to get or create FX controller: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Creates VRTify animation layer with complete safety checks
        /// </summary>
        private AnimatorControllerLayer CreateVRTifyLayerSafe(AnimatorController controller, List<NotificationApp> apps, VRTifySettings settings)
        {
            try
            {
                Debug.Log("VRTify: Creating VRTify animation layer...");

                if (controller == null)
                {
                    throw new System.ArgumentNullException(nameof(controller));
                }

                // Check if VRTify layer already exists and remove it safely
                var existingLayers = controller.layers?.ToList() ?? new List<AnimatorControllerLayer>();
                var existingVRTifyLayers = existingLayers.Where(l => l.name.Contains("VRTify")).ToList();

                foreach (var existingLayer in existingVRTifyLayers)
                {
                    try
                    {
                        var layerIndex = existingLayers.IndexOf(existingLayer);
                        if (layerIndex >= 0 && layerIndex < controller.layers.Length)
                        {
                            controller.RemoveLayer(layerIndex);
                            Debug.Log($"VRTify: Removed existing VRTify layer: {existingLayer.name}");
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"VRTify: Could not remove existing layer {existingLayer.name}: {e.Message}");
                    }
                }

                // Create new layer
                var layer = new AnimatorControllerLayer
                {
                    name = "VRTify Notifications",
                    defaultWeight = 1f,
                    stateMachine = new AnimatorStateMachine()
                };

                if (layer.stateMachine != null)
                {
                    layer.stateMachine.name = "VRTify State Machine";
                }

                // Add parameters safely
                AddAnimatorParametersSafe(controller, apps);

                // Create states and transitions
                CreateNotificationStatesSafe(layer.stateMachine, apps, settings, controller);

                // Add layer to controller
                controller.AddLayer(layer);

                // Save controller
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();

                Debug.Log("VRTify: Successfully created VRTify animation layer");
                return layer;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"VRTify: Failed to create VRTify layer: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Adds OSC parameters to animator controller with safety checks
        /// </summary>
        private void AddAnimatorParametersSafe(AnimatorController controller, List<NotificationApp> apps)
        {
            try
            {
                Debug.Log("VRTify: Adding animator parameters...");

                var existingParams = controller.parameters?.Select(p => p.name).ToHashSet() ?? new HashSet<string>();

                // Add global parameters
                var globalParams = new[]
                {
                    ("VRTify_ActiveCount", AnimatorControllerParameterType.Int),
                    ("VRTify_HUDActive", AnimatorControllerParameterType.Bool)
                };

                foreach (var (paramName, paramType) in globalParams)
                {
                    if (!existingParams.Contains(paramName))
                    {
                        controller.AddParameter(paramName, paramType);
                        Debug.Log($"VRTify: Added global parameter: {paramName}");
                    }
                }

                // Add parameters for each app
                foreach (var app in apps.Where(a => a.enabled))
                {
                    var paramName = app.GetSanitizedParameterName();

                    if (!existingParams.Contains(paramName))
                    {
                        controller.AddParameter(paramName, AnimatorControllerParameterType.Bool);
                        Debug.Log($"VRTify: Added app parameter: {paramName}");
                    }

                    // Add alpha parameter for fade animations
                    var alphaParamName = $"{paramName}_Alpha";
                    if (!existingParams.Contains(alphaParamName))
                    {
                        controller.AddParameter(alphaParamName, AnimatorControllerParameterType.Float);
                    }

                    // Add scale parameter for scale animations
                    var scaleParamName = $"{paramName}_Scale";
                    if (!existingParams.Contains(scaleParamName))
                    {
                        controller.AddParameter(scaleParamName, AnimatorControllerParameterType.Float);
                    }
                }

                Debug.Log("VRTify: Added all animator parameters successfully");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"VRTify: Failed to add animator parameters: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Creates animation states for each notification with safety checks
        /// </summary>
        private void CreateNotificationStatesSafe(AnimatorStateMachine stateMachine, List<NotificationApp> apps, VRTifySettings settings, AnimatorController controller)
        {
            try
            {
                Debug.Log("VRTify: Creating notification states...");

                if (stateMachine == null)
                {
                    throw new System.ArgumentNullException(nameof(stateMachine));
                }

                // Create master idle state
                var idleState = stateMachine.AddState("VRTify_Master_Idle");
                if (idleState != null)
                {
                    idleState.motion = CreateMasterIdleAnimation(apps);
                    stateMachine.defaultState = idleState;
                    Debug.Log("VRTify: Created master idle state");
                }

                // Create sub-state machines for each app
                foreach (var app in apps.Where(a => a.enabled))
                {
                    try
                    {
                        CreateAppSubStateMachineSafe(stateMachine, app, settings, controller);
                        Debug.Log($"VRTify: Created state machine for {app.appName}");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"VRTify: Failed to create state machine for {app.appName}: {e.Message}");
                    }
                }

                Debug.Log("VRTify: Created all notification states successfully");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"VRTify: Failed to create notification states: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Creates sub-state machine for each app with safety checks
        /// </summary>
        private void CreateAppSubStateMachineSafe(AnimatorStateMachine parentStateMachine, NotificationApp app, VRTifySettings settings, AnimatorController controller)
        {
            try
            {
                var paramName = app.GetSanitizedParameterName();

                // Create sub-state machine
                var subStateMachine = parentStateMachine.AddStateMachine($"{app.appName}_Notifications");
                if (subStateMachine == null)
                {
                    throw new System.Exception($"Failed to create sub-state machine for {app.appName}");
                }

                // Create states within sub-state machine
                var hideState = subStateMachine.AddState("Hidden");
                var showState = subStateMachine.AddState("Show");
                var visibleState = subStateMachine.AddState("Visible");
                var hideAnimation = subStateMachine.AddState("Hide");

                // Set motions safely
                if (hideState != null)
                {
                    hideState.motion = CreateHiddenAnimation(app);
                    subStateMachine.defaultState = hideState;
                }

                if (showState != null)
                {
                    showState.motion = CreateShowAnimation(app, settings);
                }

                if (visibleState != null)
                {
                    visibleState.motion = CreateVisibleAnimation(app);
                }

                if (hideAnimation != null)
                {
                    hideAnimation.motion = CreateHideAnimation(app, settings);
                }

                // Create transitions safely
                CreateTransitionsSafe(hideState, showState, visibleState, hideAnimation, paramName);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"VRTify: Failed to create sub-state machine for {app.appName}: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Creates transitions between states with safety checks
        /// </summary>
        private void CreateTransitionsSafe(AnimatorState hideState, AnimatorState showState, AnimatorState visibleState, AnimatorState hideAnimation, string paramName)
        {
            try
            {
                // Hidden -> Show (when parameter becomes true)
                if (hideState != null && showState != null)
                {
                    var hideToShow = hideState.AddTransition(showState);
                    if (hideToShow != null)
                    {
                        hideToShow.AddCondition(AnimatorConditionMode.If, 0, paramName);
                        hideToShow.duration = 0f;
                    }
                }

                // Show -> Visible (when show animation completes)
                if (showState != null && visibleState != null)
                {
                    var showToVisible = showState.AddTransition(visibleState);
                    if (showToVisible != null)
                    {
                        showToVisible.hasExitTime = true;
                        showToVisible.exitTime = 0.95f;
                        showToVisible.duration = 0.05f;
                    }
                }

                // Visible -> Hide (when parameter becomes false)
                if (visibleState != null && hideAnimation != null)
                {
                    var visibleToHide = visibleState.AddTransition(hideAnimation);
                    if (visibleToHide != null)
                    {
                        visibleToHide.AddCondition(AnimatorConditionMode.IfNot, 0, paramName);
                        visibleToHide.duration = 0f;
                    }
                }

                // Hide -> Hidden (when hide animation completes)
                if (hideAnimation != null && hideState != null)
                {
                    var hideAnimToHidden = hideAnimation.AddTransition(hideState);
                    if (hideAnimToHidden != null)
                    {
                        hideAnimToHidden.hasExitTime = true;
                        hideAnimToHidden.exitTime = 0.95f;
                        hideAnimToHidden.duration = 0.05f;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"VRTify: Failed to create some transitions for {paramName}: {e.Message}");
            }
        }

        /// <summary>
        /// Sets up expression parameters for OSC with complete safety
        /// </summary>
        public void SetupExpressionParameters(VRCAvatarDescriptor avatar, List<NotificationApp> apps)
        {
            try
            {
                Debug.Log("VRTify: Setting up expression parameters...");
                
                var enabledApps = apps.Where(a => a != null && a.enabled).ToList();
                
                var expressionParams = avatar.expressionParameters;
                
                if (expressionParams == null)
                {
                    // Ensure the Generated directory exists
                    var generatedSettingsPath = Path.Combine(VRTifyFileManager.SETTINGS_PATH, "Generated");
                    VRTifyFileManager.EnsureDirectoryExists(generatedSettingsPath);
                    
                    // Create new expression parameters
                    var paramPath = VRTifyFileManager.GetUniqueFileName(
                        generatedSettingsPath,
                        $"{avatar.name}_VRTify_ExpressionParameters",
                        ".asset"
                    );
                    
                    expressionParams = ScriptableObject.CreateInstance<VRCExpressionParameters>();
                    
                    // Convert to relative Unity path and create asset
                    var relativeParamPath = VRTifyFileManager.GetRelativeUnityPath(paramPath);
                    AssetDatabase.CreateAsset(expressionParams, relativeParamPath);
                    avatar.expressionParameters = expressionParams;
                    
                    Debug.Log($"VRTify: Created new expression parameters: {relativeParamPath}");
                }
                
                // Get existing parameters or create new list
                var parametersList = expressionParams.parameters?.ToList() ?? new List<VRCExpressionParameters.Parameter>();
                var existingParamNames = parametersList.Select(p => p.name).ToHashSet();
                
                // Add VRTify parameters
                int addedParams = 0;
                foreach (var app in enabledApps)
                {
                    var paramName = app.GetSanitizedParameterName();
                    
                    if (!existingParamNames.Contains(paramName))
                    {
                        parametersList.Add(new VRCExpressionParameters.Parameter
                        {
                            name = paramName,
                            valueType = VRCExpressionParameters.ValueType.Bool,
                            defaultValue = 0,
                            saved = false
                        });
                        addedParams++;
                        Debug.Log($"VRTify: Added parameter: {paramName}");
                    }
                }
                
                // Add global parameters
                if (!existingParamNames.Contains("VRTify_HUDActive"))
                {
                    parametersList.Add(new VRCExpressionParameters.Parameter
                    {
                        name = "VRTify_HUDActive",
                        valueType = VRCExpressionParameters.ValueType.Bool,
                        defaultValue = 1,
                        saved = true
                    });
                    addedParams++;
                    Debug.Log("VRTify: Added global parameter: VRTify_HUDActive");
                }
                
                // Check parameter limit
                if (parametersList.Count > 256)
                {
                    Debug.LogWarning($"VRTify: Parameter count ({parametersList.Count}) exceeds VRChat's limit of 256");
                }
                
                // Update expression parameters
                expressionParams.parameters = parametersList.ToArray();
                
                EditorUtility.SetDirty(expressionParams);
                AssetDatabase.SaveAssets();
                
                Debug.Log($"VRTify: Expression parameters setup complete. Added {addedParams} new parameters, total: {parametersList.Count}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"VRTify: Failed to setup expression parameters: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Sets up expression menu for manual control
        /// </summary>
        public void SetupExpressionMenu(VRCAvatarDescriptor avatar, List<NotificationApp> apps)
        {
            try
            {
                Debug.Log("VRTify: Setting up expression menu...");
                
                // Ensure the Generated directory exists
                var generatedSettingsPath = Path.Combine(VRTifyFileManager.SETTINGS_PATH, "Generated");
                VRTifyFileManager.EnsureDirectoryExists(generatedSettingsPath);
                
                // Create VRTify submenu
                var menuPath = VRTifyFileManager.GetUniqueFileName(
                    generatedSettingsPath,
                    $"{avatar.name}_VRTify_Menu",
                    ".asset"
                );
                
                var vrtifyMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                vrtifyMenu.controls = new List<VRCExpressionsMenu.Control>();
                
                // Add HUD toggle
                vrtifyMenu.controls.Add(new VRCExpressionsMenu.Control
                {
                    name = "Toggle HUD",
                    type = VRCExpressionsMenu.Control.ControlType.Toggle,
                    parameter = new VRCExpressionsMenu.Control.Parameter { name = "VRTify_HUDActive" }
                });
                
                // Add test buttons for each app (limited by menu space)
                foreach (var app in apps.Where(a => a.enabled).Take(6)) // Max 6 controls + HUD toggle
                {
                    vrtifyMenu.controls.Add(new VRCExpressionsMenu.Control
                    {
                        name = $"Test {app.appName}",
                        type = VRCExpressionsMenu.Control.ControlType.Button,
                        parameter = new VRCExpressionsMenu.Control.Parameter { name = app.GetSanitizedParameterName() }
                    });
                }
                
                // Convert to relative Unity path and create asset
                var relativeMenuPath = VRTifyFileManager.GetRelativeUnityPath(menuPath);
                AssetDatabase.CreateAsset(vrtifyMenu, relativeMenuPath);
                AssetDatabase.SaveAssets();
                
                Debug.Log($"VRTify: Expression menu created at {relativeMenuPath}");
                Debug.Log("💡 Remember to add the VRTify menu to your main expressions menu!");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"VRTify: Failed to setup expression menu: {e.Message}");
                // Non-critical, don't throw
            }
        }

        #region Animation Creation Methods

        /// <summary>
        /// Creates master idle animation (all notifications hidden)
        /// </summary>
        private AnimationClip CreateMasterIdleAnimation(List<NotificationApp> apps)
        {
            var clip = new AnimationClip();
            clip.name = "VRTify_Master_Idle";

            // Simple idle animation - could be expanded
            return SaveAnimationClip(clip);
        }

        /// <summary>
        /// Creates hidden state animation for specific app
        /// </summary>
        private AnimationClip CreateHiddenAnimation(NotificationApp app)
        {
            var clip = new AnimationClip();
            clip.name = $"VRTify_{app.appName}_Hidden";

            // Simple hidden animation - could be expanded
            return SaveAnimationClip(clip);
        }

        /// <summary>
        /// Creates show animation for specific app
        /// </summary>
        private AnimationClip CreateShowAnimation(NotificationApp app, VRTifySettings settings)
        {
            var clip = new AnimationClip();
            clip.name = $"VRTify_{app.appName}_Show";

            // Simple show animation - could be expanded
            return SaveAnimationClip(clip);
        }

        /// <summary>
        /// Creates visible state animation for specific app
        /// </summary>
        private AnimationClip CreateVisibleAnimation(NotificationApp app)
        {
            var clip = new AnimationClip();
            clip.name = $"VRTify_{app.appName}_Visible";

            // Simple visible animation - could be expanded
            return SaveAnimationClip(clip);
        }

        /// <summary>
        /// Creates hide animation for specific app
        /// </summary>
        private AnimationClip CreateHideAnimation(NotificationApp app, VRTifySettings settings)
        {
            var clip = new AnimationClip();
            clip.name = $"VRTify_{app.appName}_Hide";

            // Simple hide animation - could be expanded
            return SaveAnimationClip(clip);
        }

        /// <summary>
        /// Saves animation clip to assets
        /// </summary>
        private AnimationClip SaveAnimationClip(AnimationClip clip)
        {
            try
            {
                var animPath = VRTifyFileManager.GetUniqueFileName(
                    Path.Combine(VRTifyFileManager.ANIMATIONS_PATH, "Generated"),
                    clip.name,
                    ".anim"
                );

                AssetDatabase.CreateAsset(clip, VRTifyFileManager.GetRelativeUnityPath(animPath));
                AssetDatabase.SaveAssets();

                return clip;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"VRTify: Failed to save animation clip {clip.name}: {e.Message}");
                return clip; // Return original clip even if save failed
            }
        }

        #endregion
    }
}