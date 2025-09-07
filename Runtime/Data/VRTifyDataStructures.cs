using UnityEngine;
using System.Collections.Generic;
using System;

namespace VRTify.Data
{
    /// <summary>
    /// Configuration data for a notification application
    /// </summary>
    [Serializable]
    public class NotificationApp
    {
        [Header("Application Info")]
        public string appName;
        public string displayName;
        public string iconPath;
        public string executablePath;

        [Header("VRChat Integration")]
        public string oscParameter;
        public bool enabled = true;
        public int priority = 0; // Lower number = higher priority

        [Header("Visual Settings")]
        public Vector2 hudPosition = Vector2.zero;
        public Color tintColor = Color.white;
        public float iconScale = 1.0f;

        [Header("Animation Settings")]
        public float animationDuration = 0.3f;
        public AnimationType animationType = AnimationType.FadeScale;

        [Header("Advanced")]
        public List<string> notificationKeywords = new List<string>();
        public bool useCustomIcon = false;
        public Texture2D customIcon;

        public NotificationApp()
        {
            // Default constructor
        }

        public NotificationApp(string appName, string displayName)
        {
            this.appName = appName;
            this.displayName = displayName;
            this.oscParameter = $"/avatar/parameters/VRTify_{appName.Replace(" ", "")}";
            this.enabled = true;
        }

        /// <summary>
        /// Gets the sanitized parameter name for OSC
        /// </summary>
        public string GetSanitizedParameterName()
        {
            return $"VRTify_{appName.Replace(" ", "").Replace("-", "").Replace(".", "")}";
        }

        /// <summary>
        /// Validates the notification app configuration
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(appName) &&
                   !string.IsNullOrEmpty(displayName) &&
                   !string.IsNullOrEmpty(oscParameter);
        }
    }

    /// <summary>
    /// Main settings configuration for VRTify system
    /// </summary>
    [Serializable]
    public class VRTifySettings
    {
        [Header("HUD Configuration")]
        public Vector2 hudSize = new Vector2(400, 100);
        public Vector3 hudPosition = new Vector3(0, -0.3f, 0.5f);
        public float hudCurve = 0.1f;
        public float hudOpacity = 0.9f;

        [Header("Notification Behavior")]
        public int maxSimultaneousNotifications = 5;
        public float notificationDuration = 3.0f;
        public float notificationFadeTime = 0.5f;
        public bool enableNotificationQueue = true;
        public bool enablePrioritySystem = true;

        [Header("Icon Settings")]
        public float iconSize = 64f;
        public float iconSpacing = 10f;
        public int iconAtlasSize = 512;
        public FilterMode iconFilterMode = FilterMode.Bilinear;

        [Header("Animation Curves")]
        public AnimationCurve fadeInCurve = AnimationCurve.EaseInOut(0, 0, 0.3f, 1);
        public AnimationCurve scaleInCurve = AnimationCurve.EaseInOut(0, 0.8f, 0.3f, 1);
        public AnimationCurve slideInCurve = AnimationCurve.EaseInOut(0, -50, 0.3f, 0);
        public AnimationCurve fadeOutCurve = AnimationCurve.EaseInOut(0, 1, 0.3f, 0);

        [Header("VR Optimization")]
        public bool enableVROptimizations = true;
        public bool useViewspacePositioning = true;
        public bool enableDepthCurvature = true;
        public float vrComfortDistance = 0.5f;

        [Header("Performance")]
        public bool enableLOD = true;
        public float lodDistance = 5.0f;
        public int maxTextureResolution = 1024;
        public bool enableMipmaps = true;

        [Header("Debug")]
        public bool enableDebugMode = false;
        public bool showDebugInfo = false;
        public bool logNotifications = true;

        /// <summary>
        /// Validates the settings configuration
        /// </summary>
        public bool ValidateSettings()
        {
            bool isValid = true;

            if (maxSimultaneousNotifications <= 0)
            {
                Debug.LogWarning("VRTify: maxSimultaneousNotifications must be greater than 0");
                isValid = false;
            }

            if (notificationDuration <= 0)
            {
                Debug.LogWarning("VRTify: notificationDuration must be greater than 0");
                isValid = false;
            }

            if (iconSize <= 0)
            {
                Debug.LogWarning("VRTify: iconSize must be greater than 0");
                isValid = false;
            }

            return isValid;
        }

        /// <summary>
        /// Resets settings to default values
        /// </summary>
        public void ResetToDefaults()
        {
            hudSize = new Vector2(400, 100);
            hudPosition = new Vector3(0, -0.3f, 0.5f);
            hudCurve = 0.1f;
            hudOpacity = 0.9f;

            maxSimultaneousNotifications = 5;
            notificationDuration = 3.0f;
            notificationFadeTime = 0.5f;
            enableNotificationQueue = true;
            enablePrioritySystem = true;

            iconSize = 64f;
            iconSpacing = 10f;
            iconAtlasSize = 512;
            iconFilterMode = FilterMode.Bilinear;

            fadeInCurve = AnimationCurve.EaseInOut(0, 0, 0.3f, 1);
            scaleInCurve = AnimationCurve.EaseInOut(0, 0.8f, 0.3f, 1);
            slideInCurve = AnimationCurve.EaseInOut(0, -50, 0.3f, 0);
            fadeOutCurve = AnimationCurve.EaseInOut(0, 1, 0.3f, 0);

            enableVROptimizations = true;
            useViewspacePositioning = true;
            enableDepthCurvature = true;
            vrComfortDistance = 0.5f;

            enableLOD = true;
            lodDistance = 5.0f;
            maxTextureResolution = 1024;
            enableMipmaps = true;

            enableDebugMode = false;
            showDebugInfo = false;
            logNotifications = true;
        }
    }

    /// <summary>
    /// Animation types for notifications
    /// </summary>
    public enum AnimationType
    {
        Fade,
        Scale,
        Slide,
        FadeScale,
        FadeSlide,
        ScaleSlide,
        FadeScaleSlide,
        Bounce,
        Spin,
        Pulse
    }

    /// <summary>
    /// Notification priority levels
    /// </summary>
    public enum NotificationPriority
    {
        Critical = 0,   // System alerts, calls
        High = 1,       // Direct messages, mentions
        Medium = 2,     // General messages
        Low = 3,        // Background notifications
        Minimal = 4     // Status updates
    }

    /// <summary>
    /// HUD positioning modes
    /// </summary>
    public enum HUDPositionMode
    {
        Fixed,          // Fixed position in world space
        Viewspace,      // Follows view but maintains distance
        ScreenSpace,    // Always visible on screen
        Adaptive        // Changes based on context
    }

    /// <summary>
    /// Icon extraction result data
    /// </summary>
    [Serializable]
    public class IconExtractionResult
    {
        public bool success;
        public Texture2D texture;
        public string errorMessage;
        public Vector2 originalSize;
        public string sourcePath;

        public IconExtractionResult(bool success, Texture2D texture = null, string errorMessage = "")
        {
            this.success = success;
            this.texture = texture;
            this.errorMessage = errorMessage;
            this.originalSize = texture ? new Vector2(texture.width, texture.height) : Vector2.zero;
        }
    }

    /// <summary>
    /// Atlas generation data
    /// </summary>
    [Serializable]
    public class IconAtlasData
    {
        public Texture2D atlasTexture;
        public List<IconAtlasEntry> entries = new List<IconAtlasEntry>();
        public int atlasSize;
        public int iconSize;
        public int iconsPerRow;

        public IconAtlasData(int atlasSize, int iconSize)
        {
            this.atlasSize = atlasSize;
            this.iconSize = iconSize;
            this.iconsPerRow = atlasSize / iconSize;
        }

        /// <summary>
        /// Gets UV coordinates for a specific app
        /// </summary>
        public Vector4 GetUVRect(string appName)
        {
            var entry = entries.Find(e => e.appName == appName);
            return entry?.uvRect ?? Vector4.zero;
        }
    }

    /// <summary>
    /// Individual icon entry in atlas
    /// </summary>
    [Serializable]
    public class IconAtlasEntry
    {
        public string appName;
        public Vector4 uvRect; // x, y, width, height in UV coordinates
        public Vector2Int atlasPosition; // Position in atlas grid
        public bool isValid;

        public IconAtlasEntry(string appName, Vector2Int atlasPosition, int iconSize, int atlasSize)
        {
            this.appName = appName;
            this.atlasPosition = atlasPosition;

            // Calculate UV coordinates
            float uvX = (float)atlasPosition.x / atlasSize;
            float uvY = (float)atlasPosition.y / atlasSize;
            float uvWidth = (float)iconSize / atlasSize;
            float uvHeight = (float)iconSize / atlasSize;

            this.uvRect = new Vector4(uvX, uvY, uvWidth, uvHeight);
            this.isValid = true;
        }
    }

    /// <summary>
    /// Generation progress data
    /// </summary>
    [Serializable]
    public class VRTifyGenerationProgress
    {
        public string currentStep;
        public float progress;
        public bool isComplete;
        public List<string> completedSteps = new List<string>();
        public List<string> errors = new List<string>();

        public void UpdateProgress(string step, float progress)
        {
            this.currentStep = step;
            this.progress = progress;
        }

        public void CompleteStep(string step)
        {
            if (!completedSteps.Contains(step))
            {
                completedSteps.Add(step);
            }
        }

        public void AddError(string error)
        {
            errors.Add($"[{DateTime.Now:HH:mm:ss}] {error}");
        }
    }

    /// <summary>
    /// OSC Configuration data
    /// </summary>
    [Serializable]
    public class OSCConfiguration
    {
        [Header("Connection Settings")]
        public string vrchatIP = "127.0.0.1";
        public int vrchatPort = 9000;
        public int listenPort = 9001;

        [Header("Monitoring Settings")]
        public bool enableWindowsNotifications = true;
        public bool enableDiscordRPC = false;
        public float notificationCooldown = 1.0f;

        [Header("App Specific Settings")]
        public List<OSCAppConfig> appConfigs = new List<OSCAppConfig>();

        public OSCConfiguration()
        {
            // Default constructor
        }

        /// <summary>
        /// Gets configuration for a specific app
        /// </summary>
        public OSCAppConfig GetAppConfig(string appName)
        {
            return appConfigs.Find(c => c.appName == appName);
        }

        /// <summary>
        /// Adds or updates app configuration
        /// </summary>
        public void SetAppConfig(OSCAppConfig config)
        {
            var existing = appConfigs.Find(c => c.appName == config.appName);
            if (existing != null)
            {
                var index = appConfigs.IndexOf(existing);
                appConfigs[index] = config;
            }
            else
            {
                appConfigs.Add(config);
            }
        }
    }

    /// <summary>
    /// Individual app OSC configuration
    /// </summary>
    [Serializable]
    public class OSCAppConfig
    {
        public string appName;
        public string oscParameter;
        public bool enabled = true;
        public float triggerDuration = 3.0f;
        public List<string> notificationFilters = new List<string>();
        public bool useCustomFiltering = false;

        public OSCAppConfig(string appName, string oscParameter)
        {
            this.appName = appName;
            this.oscParameter = oscParameter;
        }
    }

    /// <summary>
    /// Serializable list wrapper for JSON serialization
    /// </summary>
    [Serializable]
    public class SerializableList<T>
    {
        public List<T> items = new List<T>();

        public SerializableList()
        {
            // Default constructor
        }

        public SerializableList(List<T> items)
        {
            this.items = items ?? new List<T>();
        }

        public static implicit operator List<T>(SerializableList<T> serializableList)
        {
            return serializableList?.items ?? new List<T>();
        }

        public static implicit operator SerializableList<T>(List<T> list)
        {
            return new SerializableList<T>(list);
        }
    }

    /// <summary>
    /// VRTify system status
    /// </summary>
    [Serializable]
    public class VRTifySystemStatus
    {
        public bool isInitialized;
        public bool hasValidAvatar;
        public bool hasValidApps;
        public bool isOSCConnected;
        public int enabledAppsCount;
        public DateTime lastGeneration;
        public string version = "1.0.0";

        public VRTifySystemStatus()
        {
            lastGeneration = DateTime.Now;
        }

        /// <summary>
        /// Checks if system is ready for generation
        /// </summary>
        public bool IsReadyForGeneration()
        {
            return hasValidAvatar && hasValidApps && enabledAppsCount > 0;
        }
    }
}