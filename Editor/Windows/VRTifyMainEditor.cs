using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using VRC.SDK3.Avatars.Components;
using VRTify.Core;
using VRTify.Data;
using VRTify.Editor.Icons;
using VRTify.Editor.Generators;
using VRTify.Editor.Utils;

namespace VRTify.Editor.Windows
{
    /// <summary>
    /// Main VRTify editor window for configuring and generating notification systems
    /// </summary>
    public class VRTifyMainEditor : EditorWindow
    {
        [SerializeField] private VRCAvatarDescriptor targetAvatar;
        [SerializeField] private Vector2 scrollPosition;
        [SerializeField] private List<NotificationApp> detectedApps = new List<NotificationApp>();
        [SerializeField] private VRTifySettings settings = new VRTifySettings();
        [SerializeField] private bool showAdvancedSettings = false;
        [SerializeField] private bool showAppSettings = true;
        [SerializeField] private bool showGenerationSettings = true;
        [SerializeField] private VRTifyAvatarSetup.HUDPosition selectedHUDPosition = VRTifyAvatarSetup.HUDPosition.BottomCenter;

        private VRTifyIconExtractor iconExtractor;
        private bool isInitialized = false;
        private VRTifySystemStatus systemStatus;

        [MenuItem("Tools/VRTify/Main Editor", priority = 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<VRTifyMainEditor>("VRTify - Dynamic VR Notifications");
            window.minSize = new Vector2(650, 750);
            window.Initialize();
        }

        [MenuItem("Tools/VRTify/Quick Setup", priority = 1)]
        public static void QuickSetup()
        {
            var window = GetWindow<VRTifyMainEditor>("VRTify - Quick Setup");
            window.minSize = new Vector2(650, 500);
            window.Initialize();
            window.AutoDetectAndSetup();
        }

        [MenuItem("Tools/VRTify/System Health Check", priority = 10)]
        public static void PerformHealthCheck()
        {
            VRTifySystemGenerator.PerformSystemHealthCheck();
        }

        private void Initialize()
        {
            if (isInitialized) return;

            // Perform system health check
            if (!VRTifySystemGenerator.PerformSystemHealthCheck())
            {
                EditorUtility.DisplayDialog("VRTify System Check Failed",
                    "VRTify system check failed. Please check the console for details and ensure VRChat SDK3 is properly installed.",
                    "OK");
                return;
            }

            VRTifyFileManager.CreateDirectoryStructure();

            iconExtractor = new VRTifyIconExtractor();
            systemStatus = VRTifySettingsManager.GetSystemStatus();

            LoadSettings();
            if (detectedApps.Count == 0)
            {
                ScanForInstalledApps();
            }

            isInitialized = true;
        }

        private void OnGUI()
        {
            if (!isInitialized) Initialize();
            if (iconExtractor == null) return;

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawHeader();
            DrawSystemStatus();
            DrawAvatarSelection();
            DrawHUDPositionSettings();
            DrawAppConfiguration();
            DrawSystemSettings();
            DrawGenerationControls();
            DrawStatusAndInstructions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            GUILayout.Space(10);

            // VRTify Logo/Title
            var titleStyle = new GUIStyle(EditorStyles.largeLabel)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = new Color(0.2f, 0.8f, 1f);

            GUILayout.Label("🔔 VRTify", titleStyle);

            var subtitleStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Italic
            };
            subtitleStyle.normal.textColor = Color.gray;

            GUILayout.Label("Dynamic VR Notification System for VRChat", subtitleStyle);

            // Version and status info
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label($"v{systemStatus.version}", EditorStyles.miniLabel);
            if (systemStatus.isInitialized)
            {
                GUILayout.Label("✓ Ready", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(15);
        }

        private void DrawSystemStatus()
        {
            if (systemStatus.enabledAppsCount > 0 || systemStatus.hasValidAvatar)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label("📊 System Status", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"Last Generation: {(systemStatus.lastGeneration != default ? systemStatus.lastGeneration.ToString("yyyy-MM-dd HH:mm") : "Never")}", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (systemStatus.enabledAppsCount > 0)
                {
                    GUILayout.Label($"Active Apps: {systemStatus.enabledAppsCount}", EditorStyles.miniLabel);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                GUILayout.Space(5);
            }
        }

        private void DrawAvatarSelection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var foldoutStyle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
            GUILayout.Label("🎭 Avatar Configuration", foldoutStyle);

            EditorGUI.indentLevel++;

            var newAvatar = (VRCAvatarDescriptor)EditorGUILayout.ObjectField(
                "Target Avatar", targetAvatar, typeof(VRCAvatarDescriptor), true);

            if (newAvatar != targetAvatar)
            {
                targetAvatar = newAvatar;
                systemStatus.hasValidAvatar = targetAvatar != null;
                VRTifySettingsManager.UpdateSystemStatus(systemStatus);
            }

            if (targetAvatar != null)
            {
                EditorGUILayout.HelpBox($"✓ Selected: {targetAvatar.name}\nReady for VRTify integration!", MessageType.Info);

                // Show existing VRTify setup if found
                var existingSetup = targetAvatar.transform.GetComponentsInChildren<Transform>()
                    .FirstOrDefault(t => t.name.Contains("VRTify"));

                if (existingSetup != null)
                {
                    EditorGUILayout.HelpBox($"⚠ Existing VRTify setup found: {existingSetup.name}\nRegeneration will replace it.", MessageType.Warning);
                }

                // Quick avatar info
                var expressionParams = targetAvatar.expressionParameters;

                // Fix for the FX controller check
                RuntimeAnimatorController fxController = null;
                var fxLayer = targetAvatar.baseAnimationLayers?.FirstOrDefault(l => l.type == VRCAvatarDescriptor.AnimLayerType.FX);
                if (fxLayer.HasValue)
                {
                    fxController = fxLayer.Value.animatorController;
                }

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"Expression Parameters: {(expressionParams ? "✓" : "✗")}", EditorStyles.miniLabel);
                GUILayout.Label($"FX Controller: {(fxController ? "✓" : "✗")}", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("Please select a VRChat avatar to continue.", MessageType.Warning);

                // Auto-detect button
                if (GUILayout.Button("🔍 Auto-detect Avatar in Scene"))
                {
                    AutoDetectAvatar();
                }
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            GUILayout.Space(10);
        }

        private void DrawHUDPositionSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("🎯 HUD Position", EditorStyles.boldLabel);

            EditorGUI.indentLevel++;
            selectedHUDPosition = (VRTifyAvatarSetup.HUDPosition)EditorGUILayout.EnumPopup("HUD Position", selectedHUDPosition);

            // Visual preview of positions
            EditorGUILayout.BeginHorizontal();
            DrawHUDPositionPreview();
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            GUILayout.Space(10);
        }

        private void DrawHUDPositionPreview()
        {
            var previewRect = GUILayoutUtility.GetRect(150, 100);
            EditorGUI.DrawRect(previewRect, new Color(0.2f, 0.2f, 0.2f, 0.3f));

            // Draw grid
            var positions = System.Enum.GetValues(typeof(VRTifyAvatarSetup.HUDPosition));
            var cols = 3;
            var rows = 3;

            for (int i = 0; i < positions.Length; i++)
            {
                var pos = (VRTifyAvatarSetup.HUDPosition)positions.GetValue(i);
                var x = i % cols;
                var y = i / cols;

                var buttonRect = new Rect(
                    previewRect.x + x * (previewRect.width / cols),
                    previewRect.y + y * (previewRect.height / rows),
                    previewRect.width / cols - 2,
                    previewRect.height / rows - 2
                );

                var isSelected = pos == selectedHUDPosition;
                var buttonColor = isSelected ? Color.cyan : Color.gray;

                EditorGUI.DrawRect(buttonRect, buttonColor * 0.5f);

                if (GUI.Button(buttonRect, "", GUIStyle.none))
                {
                    selectedHUDPosition = pos;
                }

                // Draw position name
                var labelStyle = new GUIStyle(EditorStyles.miniLabel);
                labelStyle.alignment = TextAnchor.MiddleCenter;
                labelStyle.normal.textColor = isSelected ? Color.white : Color.gray;
                GUI.Label(buttonRect, pos.ToString().Replace("Middle", "Mid").Replace("Center", "C"), labelStyle);
            }
        }

        private void DrawAppConfiguration()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            showAppSettings = EditorGUILayout.Foldout(showAppSettings,
                $"📱 Application Configuration ({detectedApps.Count(a => a.enabled)} enabled)", true);

            if (showAppSettings)
            {
                EditorGUI.indentLevel++;

                // Control buttons
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("🔍 Rescan Apps", GUILayout.Height(25)))
                {
                    ScanForInstalledApps();
                }
                if (GUILayout.Button("✓ Enable All", GUILayout.Height(25)))
                {
                    detectedApps.ForEach(app => app.enabled = true);
                    SaveSettings();
                }
                if (GUILayout.Button("✗ Disable All", GUILayout.Height(25)))
                {
                    detectedApps.ForEach(app => app.enabled = false);
                    SaveSettings();
                }
                if (GUILayout.Button("➕ Add Custom", GUILayout.Height(25)))
                {
                    AddCustomApp();
                }
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(5);

                if (detectedApps.Count == 0)
                {
                    EditorGUILayout.HelpBox("No applications detected. Click 'Rescan Apps' to scan your system, or 'Add Custom' to manually add applications.", MessageType.Warning);
                }
                else
                {
                    // Apps list with improved UI
                    var scrollViewHeight = Mathf.Min(300f, detectedApps.Count * 30f + 20f);
                    var appScrollPos = EditorGUILayout.BeginScrollView(Vector2.zero, GUILayout.Height(scrollViewHeight));

                    for (int i = 0; i < detectedApps.Count; i++)
                    {
                        DrawAppConfigurationEntry(detectedApps[i], i);
                    }

                    EditorGUILayout.EndScrollView();

                    // Statistics
                    var stats = VRTifySystemGenerator.GetGenerationStats(detectedApps);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label($"Total: {stats.totalApps} | Enabled: {stats.enabledApps} | With Icons: {stats.withIcons}", EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    if (stats.totalSize > 0)
                    {
                        GUILayout.Label($"Size: {stats.totalSize / 1024f / 1024f:F1} MB", EditorStyles.miniLabel);
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(10);
        }

        private void DrawAppConfigurationEntry(NotificationApp app, int index)
        {
            var bgColor = index % 2 == 0 ? new Color(0, 0, 0, 0.1f) : new Color(1, 1, 1, 0.05f);
            var originalBg = GUI.backgroundColor;
            GUI.backgroundColor = bgColor;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox, GUILayout.Height(25));
            GUI.backgroundColor = originalBg;

            // Enable toggle
            var newEnabled = EditorGUILayout.Toggle(app.enabled, GUILayout.Width(20));
            if (newEnabled != app.enabled)
            {
                app.enabled = newEnabled;
                SaveSettings();
            }

            // App icon status
            var iconStatus = !string.IsNullOrEmpty(app.iconPath) ? "🎨" : "📋";
            GUILayout.Label(iconStatus, GUILayout.Width(20));

            // App name
            GUILayout.Label(app.displayName, EditorStyles.boldLabel, GUILayout.Width(140));
            GUILayout.Label($"({app.appName})", EditorStyles.miniLabel, GUILayout.Width(80));

            // Priority
            GUILayout.Label("P:", EditorStyles.miniLabel, GUILayout.Width(15));
            var newPriority = EditorGUILayout.IntField(app.priority, GUILayout.Width(30));
            if (newPriority != app.priority)
            {
                app.priority = newPriority;
                SaveSettings();
            }

            // Tint color
            var newTint = EditorGUILayout.ColorField(app.tintColor, GUILayout.Width(50));
            if (newTint != app.tintColor)
            {
                app.tintColor = newTint;
                SaveSettings();
            }

            // Test icon button
            if (GUILayout.Button("🔍", GUILayout.Width(25)))
            {
                TestExtractIcon(app);
            }

            // Remove button
            if (GUILayout.Button("🗑️", GUILayout.Width(25)))
            {
                if (EditorUtility.DisplayDialog("Remove App", $"Remove {app.displayName} from VRTify?", "Remove", "Cancel"))
                {
                    detectedApps.RemoveAt(index);
                    SaveSettings();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSystemSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "⚙️ Advanced Settings", true);

            if (showAdvancedSettings)
            {
                EditorGUI.indentLevel++;

                DrawHUDSettings();
                DrawNotificationSettings();
                DrawAnimationSettings();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(10);
        }

        private void DrawHUDSettings()
        {
            GUILayout.Label("🎯 HUD Configuration", EditorStyles.boldLabel);

            settings.hudSize = EditorGUILayout.Vector2Field("Size", settings.hudSize);
            settings.hudOpacity = EditorGUILayout.Slider("Opacity", settings.hudOpacity, 0f, 1f);
            settings.hudCurve = EditorGUILayout.Slider("VR Curvature", settings.hudCurve, 0f, 0.5f);

            GUILayout.Space(5);
        }

        private void DrawNotificationSettings()
        {
            GUILayout.Label("🔔 Notification Behavior", EditorStyles.boldLabel);

            settings.maxSimultaneousNotifications = EditorGUILayout.IntSlider("Max Simultaneous", settings.maxSimultaneousNotifications, 1, 10);
            settings.notificationDuration = EditorGUILayout.Slider("Duration (seconds)", settings.notificationDuration, 1f, 10f);
            settings.iconSize = EditorGUILayout.Slider("Icon Size", settings.iconSize, 32f, 128f);
            settings.iconSpacing = EditorGUILayout.Slider("Icon Spacing", settings.iconSpacing, 5f, 50f);

            GUILayout.Space(5);
        }

        private void DrawAnimationSettings()
        {
            GUILayout.Label("🎬 Animation Settings", EditorStyles.boldLabel);

            settings.fadeInCurve = EditorGUILayout.CurveField("Fade In Curve", settings.fadeInCurve);
            settings.scaleInCurve = EditorGUILayout.CurveField("Scale In Curve", settings.scaleInCurve);
            settings.slideInCurve = EditorGUILayout.CurveField("Slide In Curve", settings.slideInCurve);
            settings.fadeOutCurve = EditorGUILayout.CurveField("Fade Out Curve", settings.fadeOutCurve);
        }

        private void DrawGenerationControls()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            showGenerationSettings = EditorGUILayout.Foldout(showGenerationSettings, "🚀 Generation Controls", true);

            if (showGenerationSettings)
            {
                EditorGUI.indentLevel++;

                // Main generation button
                var buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold
                };

                var isReadyToGenerate = targetAvatar != null && detectedApps.Any(a => a.enabled);

                GUI.enabled = isReadyToGenerate;

                if (GUILayout.Button("🚀 Generate VRTify System for Avatar", buttonStyle, GUILayout.Height(50)))
                {
                    GenerateCompleteSystem();
                }

                GUI.enabled = true;

                if (!isReadyToGenerate)
                {
                    var warning = targetAvatar == null ? "Please select an avatar" : "Please enable at least one application";
                    EditorGUILayout.HelpBox(warning, MessageType.Warning);
                }

                // Individual generation options
                GUILayout.Space(10);
                GUILayout.Label("Individual Components:", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Test Icon Extraction"))
                {
                    TestIconExtraction();
                }
                if (GUILayout.Button("Preview HUD Layout"))
                {
                    PreviewHUDLayout();
                }
                if (GUILayout.Button("Validate Setup"))
                {
                    ValidateCurrentSetup();
                }
                EditorGUILayout.EndHorizontal();

                // Settings management
                GUILayout.Space(10);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("💾 Save Settings"))
                {
                    SaveSettings();
                    EditorUtility.DisplayDialog("VRTify", "Settings saved successfully!", "OK");
                }
                if (GUILayout.Button("📂 Load Settings"))
                {
                    LoadSettings();
                    EditorUtility.DisplayDialog("VRTify", "Settings loaded successfully!", "OK");
                }
                if (GUILayout.Button("🔄 Reset Settings"))
                {
                    ResetSettings();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            GUILayout.Space(10);
        }

        private void DrawStatusAndInstructions()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("📋 VRTify Generation Overview", EditorStyles.boldLabel);

            var instructionText =
                "✅ Dynamic HUD with custom positioning\n" +
                "✅ Icon extraction from installed applications\n" +
                "✅ Smooth fade/scale/slide animations\n" +
                "✅ VRChat Avatar 3.0 integration\n" +
                "✅ OSC parameter setup for external control\n" +
                "✅ Automatic animator controller generation\n" +
                "✅ Expression parameters configuration\n" +
                "✅ Multi-notification support with smart layout\n" +
                "✅ VR-optimized viewspace positioning\n" +
                "✅ Priority-based notification system";

            GUILayout.TextArea(instructionText, GUILayout.Height(180));

            // Status information
            if (targetAvatar != null && detectedApps.Any(a => a.enabled))
            {
                var enabledCount = detectedApps.Count(a => a.enabled);
                EditorGUILayout.HelpBox($"✓ Ready to generate VRTify system!\nTarget: {targetAvatar.name}\nApps: {enabledCount} enabled", MessageType.Info);
            }
            else if (targetAvatar == null)
            {
                EditorGUILayout.HelpBox("⚠ Please select an avatar to continue.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("⚠ Please enable at least one application.", MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        #region Core Methods

        private void ScanForInstalledApps()
        {
            EditorUtility.DisplayProgressBar("VRTify", "Scanning for installed applications...", 0.5f);

            try
            {
                detectedApps = iconExtractor.ScanForInstalledApps();
                systemStatus.hasValidApps = detectedApps.Count > 0;
                systemStatus.enabledAppsCount = detectedApps.Count(a => a.enabled);
                VRTifySettingsManager.UpdateSystemStatus(systemStatus);
                SaveSettings();
                Debug.Log($"VRTify: Found {detectedApps.Count} applications");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void AutoDetectAvatar()
        {
            var avatars = FindObjectsOfType<VRCAvatarDescriptor>();
            if (avatars.Length == 1)
            {
                targetAvatar = avatars[0];
                EditorUtility.DisplayDialog("VRTify", $"Auto-detected avatar: {targetAvatar.name}", "OK");
            }
            else if (avatars.Length > 1)
            {
                EditorUtility.DisplayDialog("VRTify", $"Found {avatars.Length} avatars in scene. Please select one manually.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("VRTify", "No VRChat avatars found in the current scene.", "OK");
            }
        }

        private void AutoDetectAndSetup()
        {
            AutoDetectAvatar();
            ScanForInstalledApps();

            // Enable common apps by default
            var commonApps = new[] { "Discord", "Steam", "Spotify", "Chrome", "Firefox", "Teams", "Slack" };
            foreach (var app in detectedApps)
            {
                app.enabled = commonApps.Contains(app.appName);
            }

            SaveSettings();
        }

        private void TestExtractIcon(NotificationApp app)
        {
            var result = iconExtractor.GetIconExtractionResult(app.iconPath);
            if (result.success)
            {
                EditorUtility.DisplayDialog("VRTify",
                    $"✓ Successfully extracted icon for {app.displayName}\n" +
                    $"Size: {result.originalSize.x}x{result.originalSize.y}\n" +
                    $"Source: {result.sourcePath}", "OK");

                if (result.texture != null)
                    Object.DestroyImmediate(result.texture);
            }
            else
            {
                EditorUtility.DisplayDialog("VRTify",
                    $"✗ Failed to extract icon for {app.displayName}\n" +
                    $"Error: {result.errorMessage}\n" +
                    $"Path: {app.iconPath}", "OK");
            }
        }

        private void TestIconExtraction()
        {
            var enabledApps = detectedApps.Where(a => a.enabled).ToList();
            if (enabledApps.Count == 0)
            {
                EditorUtility.DisplayDialog("VRTify", "No enabled apps to test icon extraction.", "OK");
                return;
            }

            int successful = 0;
            foreach (var app in enabledApps)
            {
                var result = iconExtractor.GetIconExtractionResult(app.iconPath);
                if (result.success)
                {
                    successful++;
                    if (result.texture != null)
                        Object.DestroyImmediate(result.texture);
                }
            }

            EditorUtility.DisplayDialog("VRTify",
                $"Icon Extraction Test Results:\n" +
                $"✓ Successful: {successful}/{enabledApps.Count}\n" +
                $"✗ Failed: {enabledApps.Count - successful}/{enabledApps.Count}", "OK");
        }

        private void PreviewHUDLayout()
        {
            EditorUtility.DisplayDialog("VRTify",
                $"HUD Layout Preview:\n" +
                $"Position: {selectedHUDPosition}\n" +
                $"Size: {settings.hudSize.x}x{settings.hudSize.y}\n" +
                $"Icon Size: {settings.iconSize}px\n" +
                $"Icon Spacing: {settings.iconSpacing}px\n" +
                $"Max Icons: {settings.maxSimultaneousNotifications}\n" +
                $"VR Curvature: {settings.hudCurve}", "OK");
        }

        private void ValidateCurrentSetup()
        {
            var issues = new List<string>();

            if (targetAvatar == null)
                issues.Add("No avatar selected");

            if (!detectedApps.Any(a => a.enabled))
                issues.Add("No apps enabled");

            if (!settings.ValidateSettings())
                issues.Add("Invalid settings configuration");

            var appsWithoutIcons = detectedApps.Where(a => a.enabled && string.IsNullOrEmpty(a.iconPath)).Count();
            if (appsWithoutIcons > 0)
                issues.Add($"{appsWithoutIcons} apps without icons");

            if (issues.Count == 0)
            {
                EditorUtility.DisplayDialog("VRTify", "✅ Setup validation passed!\nReady for generation.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("VRTify",
                    $"⚠ Setup validation found issues:\n\n" +
                    string.Join("\n", issues.Select(i => $"• {i}")), "OK");
            }
        }

        private void AddCustomApp()
        {
            var customApp = new NotificationApp
            {
                appName = "CustomApp",
                displayName = "Custom Application",
                iconPath = "",
                oscParameter = "/avatar/parameters/VRTify_CustomApp",
                enabled = true,
                priority = detectedApps.Count
            };

            detectedApps.Add(customApp);
            SaveSettings();
        }

        private void GenerateCompleteSystem()
        {
            var enabledApps = detectedApps.Where(a => a.enabled).ToList();
            var success = VRTifySystemGenerator.GenerateCompleteSystem(targetAvatar, enabledApps, settings, selectedHUDPosition);

            if (success)
            {
                // Update system status
                systemStatus.isInitialized = true;
                systemStatus.hasValidAvatar = true;
                systemStatus.hasValidApps = true;
                systemStatus.enabledAppsCount = enabledApps.Count;
                systemStatus.lastGeneration = System.DateTime.Now;
                VRTifySettingsManager.UpdateSystemStatus(systemStatus);

                SaveSettings();
            }
        }

        private void SaveSettings()
        {
            VRTifySettingsManager.SaveSettings(settings, detectedApps);
        }

        private void LoadSettings()
        {
            var loadedData = VRTifySettingsManager.LoadSettings();
            if (loadedData.settings != null)
            {
                settings = loadedData.settings;
            }
            if (loadedData.apps != null)
            {
                detectedApps = loadedData.apps;
            }

            // Update system status
            systemStatus = VRTifySettingsManager.GetSystemStatus();
        }

        private void ResetSettings()
        {
            if (EditorUtility.DisplayDialog("VRTify", "Reset all settings to default?\n\nThis will:\n• Reset all configuration options\n• Clear detected apps\n• Rescan applications", "Reset", "Cancel"))
            {
                settings = new VRTifySettings();
                detectedApps.Clear();
                selectedHUDPosition = VRTifyAvatarSetup.HUDPosition.BottomCenter;

                // Reset system status
                systemStatus = new VRTifySystemStatus();
                VRTifySettingsManager.UpdateSystemStatus(systemStatus);

                ScanForInstalledApps();
                SaveSettings();

                EditorUtility.DisplayDialog("VRTify", "Settings reset successfully!", "OK");
            }
        }

        private void OnDisable()
        {
            // Auto-save settings when window is closed
            if (isInitialized)
            {
                SaveSettings();
            }
        }

        private void OnDestroy()
        {
            // Cleanup
            if (isInitialized)
            {
                SaveSettings();
            }
        }

        private void OnFocus()
        {
            // Refresh system status when window gets focus
            if (isInitialized)
            {
                systemStatus = VRTifySettingsManager.GetSystemStatus();
                Repaint();
            }
        }

        // Handle undo/redo
        private void OnUndoRedo()
        {
            Repaint();
        }

        #endregion

        #region Menu Items and Utilities

        [MenuItem("Tools/VRTify/Documentation", priority = 20)]
        public static void OpenDocumentation()
        {
            Application.OpenURL("https://github.com/YourRepo/VRTify/wiki");
        }

        [MenuItem("Tools/VRTify/Settings/Export Settings", priority = 30)]
        public static void ExportSettings()
        {
            var exportPath = EditorUtility.SaveFilePanel("Export VRTify Settings", "", "VRTify_Settings", "json");
            if (!string.IsNullOrEmpty(exportPath))
            {
                var (settings, apps) = VRTifySettingsManager.LoadSettings();
                VRTifySettingsManager.ExportSettings(exportPath, settings, apps);
                EditorUtility.DisplayDialog("VRTify", $"Settings exported to:\n{exportPath}", "OK");
            }
        }

        [MenuItem("Tools/VRTify/Settings/Import Settings", priority = 31)]
        public static void ImportSettings()
        {
            var importPath = EditorUtility.OpenFilePanel("Import VRTify Settings", "", "json");
            if (!string.IsNullOrEmpty(importPath))
            {
                var (settings, apps, success) = VRTifySettingsManager.ImportSettings(importPath);
                if (success)
                {
                    EditorUtility.DisplayDialog("VRTify", $"Settings imported successfully!\n{apps?.Count ?? 0} applications loaded.", "OK");

                    // Refresh any open VRTify windows
                    var windows = Resources.FindObjectsOfTypeAll<VRTifyMainEditor>();
                    foreach (var window in windows)
                    {
                        window.LoadSettings();
                        window.Repaint();
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("VRTify", "Failed to import settings. Please check the file format.", "OK");
                }
            }
        }

        [MenuItem("Tools/VRTify/Settings/Reset to Defaults", priority = 32)]
        public static void ResetToDefaults()
        {
            if (EditorUtility.DisplayDialog("VRTify",
                "Reset VRTify to factory defaults?\n\n" +
                "This will:\n" +
                "• Delete all settings\n" +
                "• Clear application configurations\n" +
                "• Reset system status\n" +
                "• Create backup of current settings\n\n" +
                "This action cannot be undone!", "Reset", "Cancel"))
            {
                VRTifySettingsManager.ResetToFactoryDefaults();
                EditorUtility.DisplayDialog("VRTify", "VRTify has been reset to factory defaults.", "OK");

                // Refresh any open windows
                var windows = Resources.FindObjectsOfTypeAll<VRTifyMainEditor>();
                foreach (var window in windows)
                {
                    window.Initialize();
                    window.Repaint();
                }
            }
        }

        [MenuItem("Tools/VRTify/Utilities/Clean Generated Assets", priority = 40)]
        public static void CleanGeneratedAssets()
        {
            if (EditorUtility.DisplayDialog("VRTify",
                "Clean all generated VRTify assets?\n\n" +
                "This will delete:\n" +
                "• Generated shaders\n" +
                "• Generated materials\n" +
                "• Generated animations\n" +
                "• Generated textures\n" +
                "• Generated controllers\n\n" +
                "Avatar setups will need to be regenerated.", "Clean", "Cancel"))
            {
                VRTifyFileManager.CleanGeneratedFiles();
                EditorUtility.DisplayDialog("VRTify", "Generated assets cleaned successfully!", "OK");
            }
        }

        [MenuItem("Tools/VRTify/Utilities/Show Data Usage", priority = 41)]
        public static void ShowDataUsage()
        {
            var (settingsSize, backupsSize, totalSize) = VRTifySettingsManager.GetDataUsageInfo();
            var vrtifySize = VRTifyFileManager.GetVRTifyDiskUsage();

            var message = $"VRTify Data Usage:\n\n" +
                         $"Settings: {settingsSize / 1024f:F1} KB\n" +
                         $"Backups: {backupsSize / 1024f / 1024f:F1} MB\n" +
                         $"Total VRTify: {vrtifySize / 1024f / 1024f:F1} MB\n\n" +
                         $"Disk Usage Total: {totalSize / 1024f / 1024f:F1} MB";

            EditorUtility.DisplayDialog("VRTify Data Usage", message, "OK");
        }

        [MenuItem("Tools/VRTify/Utilities/Create Backup", priority = 42)]
        public static void CreateManualBackup()
        {
            var backupName = $"Manual_Backup_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
            VRTifySettingsManager.CreateSettingsBackup(backupName);
            EditorUtility.DisplayDialog("VRTify", $"Manual backup created:\n{backupName}", "OK");
        }

        [MenuItem("Tools/VRTify/Utilities/Restore from Backup", priority = 43)]
        public static void RestoreFromBackup()
        {
            var availableBackups = VRTifySettingsManager.GetAvailableBackups();
            if (availableBackups.Count == 0)
            {
                EditorUtility.DisplayDialog("VRTify", "No backups available to restore from.", "OK");
                return;
            }

            // Show backup selection (simplified - in real implementation you'd want a proper selection UI)
            var backupNames = availableBackups.Select(b => b.name).ToArray(); // Fix: use b.name instead of Path.GetFileName(b)
            var selectedIndex = 0; // For this example, we'll use the most recent

            if (EditorUtility.DisplayDialog("VRTify",
                $"Restore from backup?\n\nMost recent backup:\n{backupNames[selectedIndex]}\n\n" +
                "This will overwrite current settings.", "Restore", "Cancel"))
            {
                var success = VRTifySettingsManager.RestoreSettingsFromBackup(availableBackups[selectedIndex].path); // Fix: use .path property
                if (success)
                {
                    EditorUtility.DisplayDialog("VRTify", "Settings restored successfully!", "OK");

                    // Refresh windows
                    var windows = Resources.FindObjectsOfTypeAll<VRTifyMainEditor>();
                    foreach (var window in windows)
                    {
                        window.LoadSettings();
                        window.Repaint();
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("VRTify", "Failed to restore settings from backup.", "OK");
                }
            }
        }

        [MenuItem("Tools/VRTify/Debug/Enable Debug Mode", priority = 50)]
        public static void EnableDebugMode()
        {
            var (settings, apps) = VRTifySettingsManager.LoadSettings();
            settings.enableDebugMode = true;
            settings.showDebugInfo = true;
            settings.logNotifications = true;
            VRTifySettingsManager.SaveSettings(settings, apps);

            Debug.Log("VRTify: Debug mode enabled");
            EditorUtility.DisplayDialog("VRTify", "Debug mode enabled. Check console for detailed logs.", "OK");
        }

        [MenuItem("Tools/VRTify/Debug/Disable Debug Mode", priority = 51)]
        public static void DisableDebugMode()
        {
            var (settings, apps) = VRTifySettingsManager.LoadSettings();
            settings.enableDebugMode = false;
            settings.showDebugInfo = false;
            settings.logNotifications = false;
            VRTifySettingsManager.SaveSettings(settings, apps);

            Debug.Log("VRTify: Debug mode disabled");
            EditorUtility.DisplayDialog("VRTify", "Debug mode disabled.", "OK");
        }

        [MenuItem("Tools/VRTify/Debug/Generate Test Data", priority = 52)]
        public static void GenerateTestData()
        {
            var testApps = new List<NotificationApp>
            {
                new NotificationApp("TestApp1", "Test Application 1") { enabled = true, priority = 0 },
                new NotificationApp("TestApp2", "Test Application 2") { enabled = true, priority = 1 },
                new NotificationApp("TestApp3", "Test Application 3") { enabled = false, priority = 2 }
            };

            var testSettings = new VRTifySettings();
            testSettings.enableDebugMode = true;

            VRTifySettingsManager.SaveSettings(testSettings, testApps);
            EditorUtility.DisplayDialog("VRTify", "Test data generated successfully!", "OK");

            // Refresh windows
            var windows = Resources.FindObjectsOfTypeAll<VRTifyMainEditor>();
            foreach (var window in windows)
            {
                window.LoadSettings();
                window.Repaint();
            }
        }

        [MenuItem("Tools/VRTify/About", priority = 100)]
        public static void ShowAbout()
        {
            var systemStatus = VRTifySettingsManager.GetSystemStatus();
            var (settingsSize, backupsSize, totalSize) = VRTifySettingsManager.GetDataUsageInfo();

            var aboutText = $"VRTify - Dynamic VR Notification System\n" +
                           $"Version: {systemStatus.version}\n\n" +
                           $"System Status:\n" +
                           $"• Initialized: {(systemStatus.isInitialized ? "✓" : "✗")}\n" +
                           $"• Valid Avatar: {(systemStatus.hasValidAvatar ? "✓" : "✗")}\n" +
                           $"• Valid Apps: {(systemStatus.hasValidApps ? "✓" : "✗")}\n" +
                           $"• Enabled Apps: {systemStatus.enabledAppsCount}\n" +
                           $"• Last Generation: {(systemStatus.lastGeneration != default ? systemStatus.lastGeneration.ToString("yyyy-MM-dd HH:mm") : "Never")}\n\n" +
                           $"Data Usage: {totalSize / 1024f / 1024f:F1} MB\n\n" +
                           $"Unity Version: {Application.unityVersion}\n" +
                           $"Platform: {Application.platform}\n\n" +
                           $"Created for VRChat Avatar 3.0\n" +
                           $"Requires VRChat SDK3 - Avatars";

            EditorUtility.DisplayDialog("About VRTify", aboutText, "OK");
        }

        #endregion

        #region Validation Methods

        /// <summary>
        /// Validates VRChat SDK installation and compatibility
        /// </summary>
        private bool ValidateVRChatSDK()
        {
            return VRTifySystemGenerator.ValidateVRChatSDK();
        }

        /// <summary>
        /// Validates current avatar selection
        /// </summary>
        private bool ValidateAvatar()
        {
            if (targetAvatar == null)
            {
                Debug.LogWarning("VRTify: No avatar selected");
                return false;
            }

            // Check if avatar has required components
            var animator = targetAvatar.GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogWarning($"VRTify: Avatar {targetAvatar.name} has no Animator component");
                return false;
            }

            // Check if avatar has humanoid avatar
            if (animator.avatar == null || !animator.avatar.isHuman)
            {
                Debug.LogWarning($"VRTify: Avatar {targetAvatar.name} does not have a humanoid avatar setup");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates app configuration
        /// </summary>
        private bool ValidateApps()
        {
            var enabledApps = detectedApps.Where(a => a.enabled).ToList();
            if (enabledApps.Count == 0)
            {
                Debug.LogWarning("VRTify: No apps enabled");
                return false;
            }

            // Check for duplicate OSC parameters
            var parameterGroups = enabledApps.GroupBy(a => a.oscParameter);
            var duplicates = parameterGroups.Where(g => g.Count() > 1);

            if (duplicates.Any())
            {
                foreach (var group in duplicates)
                {
                    Debug.LogWarning($"VRTify: Duplicate OSC parameter: {group.Key} used by {group.Count()} apps");
                }
                return false;
            }

            // Validate each app
            var invalidApps = enabledApps.Where(a => !a.IsValid()).ToList();
            if (invalidApps.Any())
            {
                foreach (var app in invalidApps)
                {
                    Debug.LogWarning($"VRTify: Invalid app configuration: {app.appName}");
                }
                return false;
            }

            return true;
        }

        /// <summary>
        /// Performs complete validation of current setup
        /// </summary>
        private bool ValidateCompleteSetup()
        {
            var issues = new List<string>();

            if (!ValidateVRChatSDK())
                issues.Add("VRChat SDK3 not properly installed");

            if (!ValidateAvatar())
                issues.Add("Avatar validation failed");

            if (!ValidateApps())
                issues.Add("App configuration validation failed");

            if (!settings.ValidateSettings())
                issues.Add("Settings validation failed");

            if (issues.Any())
            {
                Debug.LogError($"VRTify: Setup validation failed:\n{string.Join("\n", issues)}");
                return false;
            }

            Debug.Log("VRTify: Setup validation passed");
            return true;
        }

        #endregion
    }
}