using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VRTify.Data;
using VRTify.Core;

namespace VRTify.Editor.Utils
{
    /// <summary>
    /// Enhanced settings manager for VRTify with improved error handling and backup features
    /// </summary>
    public static class VRTifySettingsManager
    {
        private const string SETTINGS_VERSION = "1.0.0";

        /// <summary>
        /// Saves VRTify settings and app configuration to disk with versioning
        /// </summary>
        public static void SaveSettings(VRTifySettings settings, List<NotificationApp> apps)
        {
            try
            {
                VRTifyFileManager.EnsureDirectoryExists(VRTifyFileManager.SETTINGS_PATH);

                // Validate before saving
                if (settings != null && !settings.ValidateSettings())
                {
                    Debug.LogWarning("VRTify: Settings validation failed during save, applying fixes...");
                    settings.ResetToDefaults();
                }

                // Create versioned settings wrapper
                var settingsWrapper = new VersionedSettings
                {
                    version = SETTINGS_VERSION,
                    timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    unityVersion = Application.unityVersion,
                    settings = settings ?? new VRTifySettings()
                };

                // Save main settings with version info
                var settingsJson = JsonUtility.ToJson(settingsWrapper, true);
                VRTifyFileManager.WriteTextFile(VRTifyFileManager.SETTINGS_FILE, settingsJson);

                // Save apps configuration with validation
                var validApps = apps?.Where(a => a != null && a.IsValid()).ToList() ?? new List<NotificationApp>();
                var appsWrapper = new VersionedApps
                {
                    version = SETTINGS_VERSION,
                    timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    apps = validApps
                };

                var appsJson = JsonUtility.ToJson(appsWrapper, true);
                VRTifyFileManager.WriteTextFile(VRTifyFileManager.APPS_FILE, appsJson);

                // Save system status
                UpdateSystemStatusInternal(settings, validApps);

                // Auto-cleanup old backups
                CleanupOldBackups();

                Debug.Log($"VRTify: Settings saved successfully ({validApps.Count} apps)");

                if (apps != null && apps.Count != validApps.Count)
                {
                    Debug.LogWarning($"VRTify: {apps.Count - validApps.Count} invalid apps were excluded from save");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"VRTify: Failed to save settings: {e.Message}");
                EditorUtility.DisplayDialog("VRTify Save Error",
                    $"Failed to save settings:\n{e.Message}\n\nPlease check file permissions and disk space.", "OK");
            }
        }

        /// <summary>
        /// Loads VRTify settings and app configuration from disk with migration support
        /// </summary>
        public static (VRTifySettings settings, List<NotificationApp> apps) LoadSettings()
        {
            VRTifySettings settings = null;
            List<NotificationApp> apps = null;

            try
            {
                // Load main settings
                var settingsJson = VRTifyFileManager.ReadTextFile(VRTifyFileManager.SETTINGS_FILE);
                if (!string.IsNullOrEmpty(settingsJson))
                {
                    try
                    {
                        // Try loading as versioned settings first
                        var versionedSettings = JsonUtility.FromJson<VersionedSettings>(settingsJson);
                        if (versionedSettings?.settings != null)
                        {
                            settings = versionedSettings.settings;

                            // Check for version compatibility
                            if (versionedSettings.version != SETTINGS_VERSION)
                            {
                                Debug.LogWarning($"VRTify: Settings version mismatch. Expected {SETTINGS_VERSION}, found {versionedSettings.version}");
                                settings = MigrateSettings(settings, versionedSettings.version);
                            }
                        }
                        else
                        {
                            // Fallback to direct settings loading (legacy)
                            settings = JsonUtility.FromJson<VRTifySettings>(settingsJson);
                            if (settings != null)
                            {
                                Debug.Log("VRTify: Loaded legacy settings format, will upgrade on next save");
                            }
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"VRTify: Failed to parse settings JSON: {e.Message}");
                        settings = new VRTifySettings();
                    }
                }

                // Load apps configuration
                var appsJson = VRTifyFileManager.ReadTextFile(VRTifyFileManager.APPS_FILE);
                if (!string.IsNullOrEmpty(appsJson))
                {
                    try
                    {
                        // Try loading as versioned apps first
                        var versionedApps = JsonUtility.FromJson<VersionedApps>(appsJson);
                        if (versionedApps?.apps != null)
                        {
                            apps = versionedApps.apps;

                            // Check version compatibility
                            if (versionedApps.version != SETTINGS_VERSION)
                            {
                                Debug.LogWarning($"VRTify: Apps version mismatch. Expected {SETTINGS_VERSION}, found {versionedApps.version}");
                                apps = MigrateApps(apps, versionedApps.version);
                            }
                        }
                        else
                        {
                            // Fallback to direct apps loading (legacy)
                            var appsWrapper = JsonUtility.FromJson<SerializableList<NotificationApp>>(appsJson);
                            apps = appsWrapper?.items;
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"VRTify: Failed to parse apps JSON: {e.Message}");
                        apps = new List<NotificationApp>();
                    }
                }

                // Validate and repair loaded data
                ValidateAndRepairSettings(ref settings, ref apps);

                Debug.Log($"VRTify: Settings loaded successfully ({apps?.Count ?? 0} apps)");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"VRTify: Failed to load settings: {e.Message}");
                settings = new VRTifySettings();
                apps = new List<NotificationApp>();
            }

            // Return defaults if loading failed
            return (settings ?? new VRTifySettings(), apps ?? new List<NotificationApp>());
        }

        /// <summary>
        /// Creates a timestamped backup of current settings
        /// </summary>
        public static void CreateSettingsBackup(string backupName = null)
        {
            try
            {
                if (string.IsNullOrEmpty(backupName))
                {
                    backupName = $"Auto_Backup_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
                }

                var backupDir = Path.Combine(VRTifyFileManager.OUTPUT_PATH, "Backups", backupName);
                VRTifyFileManager.EnsureDirectoryExists(backupDir);

                // Backup settings files
                var filesToBackup = new[]
                {
                    (VRTifyFileManager.SETTINGS_FILE, "VRTifySettings.json"),
                    (VRTifyFileManager.APPS_FILE, "VRTifyApps.json"),
                    (VRTifyFileManager.OSC_CONFIG_FILE, "OSCConfig.json")
                };

                int backedUpFiles = 0;
                foreach (var (sourceFile, targetFileName) in filesToBackup)
                {
                    if (File.Exists(sourceFile))
                    {
                        var targetPath = Path.Combine(backupDir, targetFileName);
                        File.Copy(sourceFile, targetPath, true);
                        backedUpFiles++;
                    }
                }

                // Create backup info file
                var backupInfo = new BackupInfo
                {
                    backupDate = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    vrtifyVersion = SETTINGS_VERSION,
                    unityVersion = Application.unityVersion,
                    platform = Application.platform.ToString(),
                    filesBackedUp = backedUpFiles,
                    backupType = backupName.StartsWith("Auto_") ? "Automatic" : "Manual"
                };

                var backupInfoJson = JsonUtility.ToJson(backupInfo, true);
                VRTifyFileManager.WriteTextFile(Path.Combine(backupDir, "BackupInfo.json"), backupInfoJson);

                Debug.Log($"VRTify: Settings backup '{backupName}' created with {backedUpFiles} files");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"VRTify: Failed to create settings backup: {e.Message}");
            }
        }

        /// <summary>
        /// Restores settings from a backup with validation
        /// </summary>
        public static bool RestoreSettingsFromBackup(string backupPath)
        {
            try
            {
                if (!Directory.Exists(backupPath))
                {
                    Debug.LogError($"VRTify: Backup directory not found: {backupPath}");
                    return false;
                }

                // Read backup info
                var backupInfoPath = Path.Combine(backupPath, "BackupInfo.json");
                BackupInfo backupInfo = null;
                if (File.Exists(backupInfoPath))
                {
                    var backupInfoJson = File.ReadAllText(backupInfoPath);
                    backupInfo = JsonUtility.FromJson<BackupInfo>(backupInfoJson);
                }

                // Create safety backup before restore
                CreateSettingsBackup($"Before_Restore_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}");

                var filesToRestore = new[]
                {
                    ("VRTifySettings.json", VRTifyFileManager.SETTINGS_FILE),
                    ("VRTifyApps.json", VRTifyFileManager.APPS_FILE),
                    ("OSCConfig.json", VRTifyFileManager.OSC_CONFIG_FILE)
                };

                int restoredFiles = 0;
                foreach (var (backupFileName, targetPath) in filesToRestore)
                {
                    var backupFilePath = Path.Combine(backupPath, backupFileName);
                    if (File.Exists(backupFilePath))
                    {
                        VRTifyFileManager.EnsureDirectoryExists(Path.GetDirectoryName(targetPath));
                        File.Copy(backupFilePath, targetPath, true);
                        restoredFiles++;
                    }
                }

                if (restoredFiles > 0)
                {
                    // Validate restored settings
                    var (settings, apps) = LoadSettings();
                    ValidateAndRepairSettings(ref settings, ref apps);
                    SaveSettings(settings, apps);

                    var backupDate = backupInfo?.backupDate ?? "Unknown";
                    Debug.Log($"VRTify: Settings restored from backup dated {backupDate} ({restoredFiles} files)");
                    return true;
                }
                else
                {
                    Debug.LogWarning("VRTify: No valid backup files found to restore");
                    return false;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"VRTify: Failed to restore settings from backup: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets a list of available backup directories with info
        /// </summary>
        public static List<BackupEntry> GetAvailableBackups()
        {
            var backups = new List<BackupEntry>();

            try
            {
                var backupsDir = Path.Combine(VRTifyFileManager.OUTPUT_PATH, "Backups");
                if (Directory.Exists(backupsDir))
                {
                    var directories = Directory.GetDirectories(backupsDir);
                    foreach (var dir in directories)
                    {
                        var backupInfoPath = Path.Combine(dir, "BackupInfo.json");
                        BackupInfo backupInfo = null;

                        if (File.Exists(backupInfoPath))
                        {
                            try
                            {
                                var json = File.ReadAllText(backupInfoPath);
                                backupInfo = JsonUtility.FromJson<BackupInfo>(json);
                            }
                            catch
                            {
                                // Ignore invalid backup info files
                            }
                        }

                        backups.Add(new BackupEntry
                        {
                            path = dir,
                            name = Path.GetFileName(dir),
                            info = backupInfo,
                            creationTime = Directory.GetCreationTime(dir)
                        });
                    }

                    // Sort by creation date (newest first)
                    backups.Sort((a, b) => b.creationTime.CompareTo(a.creationTime));
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"VRTify: Failed to get available backups: {e.Message}");
            }

            return backups;
        }

        /// <summary>
        /// Exports settings to a portable file
        /// </summary>
        public static void ExportSettings(string exportPath, VRTifySettings settings, List<NotificationApp> apps)
        {
            try
            {
                var exportData = new ExportData
                {
                    exportDate = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    vrtifyVersion = SETTINGS_VERSION,
                    settings = settings,
                    apps = apps?.Where(a => a != null && a.IsValid()).ToList() ?? new List<NotificationApp>(),
                    systemInfo = new ExportData.SystemInfo
                    {
                        unityVersion = Application.unityVersion,
                        platform = Application.platform.ToString(),
                        operatingSystem = SystemInfo.operatingSystem
                    }
                };

                var exportJson = JsonUtility.ToJson(exportData, true);
                VRTifyFileManager.WriteTextFile(exportPath, exportJson);

                Debug.Log($"VRTify: Settings exported to {exportPath} ({exportData.apps.Count} apps)");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"VRTify: Failed to export settings: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Imports settings from a portable file with validation
        /// </summary>
        public static (VRTifySettings settings, List<NotificationApp> apps, bool success) ImportSettings(string importPath)
        {
            try
            {
                var importJson = VRTifyFileManager.ReadTextFile(importPath);
                if (string.IsNullOrEmpty(importJson))
                {
                    Debug.LogError("VRTify: Import file is empty or could not be read");
                    return (null, null, false);
                }

                var exportData = JsonUtility.FromJson<ExportData>(importJson);
                if (exportData?.settings == null)
                {
                    Debug.LogError("VRTify: Invalid import file format");
                    return (null, null, false);
                }

                // Validate imported settings
                var settings = exportData.settings;
                if (!settings.ValidateSettings())
                {
                    Debug.LogWarning("VRTify: Imported settings failed validation, applying fixes");
                    settings.ResetToDefaults();
                }

                // Validate imported apps
                var validApps = new List<NotificationApp>();
                if (exportData.apps != null)
                {
                    foreach (var app in exportData.apps)
                    {
                        if (app != null && app.IsValid())
                        {
                            validApps.Add(app);
                        }
                        else
                        {
                            Debug.LogWarning($"VRTify: Skipping invalid imported app: {app?.appName ?? "null"}");
                        }
                    }
                }

                // Check version compatibility
                if (exportData.vrtifyVersion != SETTINGS_VERSION)
                {
                    Debug.LogWarning($"VRTify: Import version mismatch. File: {exportData.vrtifyVersion}, Current: {SETTINGS_VERSION}");
                    settings = MigrateSettings(settings, exportData.vrtifyVersion);
                    validApps = MigrateApps(validApps, exportData.vrtifyVersion);
                }

                Debug.Log($"VRTify: Settings imported successfully from {exportData.exportDate} - {validApps.Count} apps");
                return (settings, validApps, true);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"VRTify: Failed to import settings: {e.Message}");
                return (null, null, false);
            }
        }

        /// <summary>
        /// Validates settings integrity and repairs common issues
        /// </summary>
        public static bool ValidateAndRepairSettings(ref VRTifySettings settings, ref List<NotificationApp> apps)
        {
            bool wasRepaired = false;

            try
            {
                // Validate and repair settings
                if (settings == null)
                {
                    settings = new VRTifySettings();
                    wasRepaired = true;
                    Debug.Log("VRTify: Created new default settings");
                }
                else if (!settings.ValidateSettings())
                {
                    var backup = settings;
                    settings.ResetToDefaults();

                    // Try to preserve some user preferences
                    try
                    {
                        settings.hudPosition = backup.hudPosition;
                        settings.hudSize = backup.hudSize;
                        settings.maxSimultaneousNotifications = backup.maxSimultaneousNotifications;
                        settings.notificationDuration = backup.notificationDuration;
                        settings.iconSize = backup.iconSize;
                        settings.iconSpacing = backup.iconSpacing;
                    }
                    catch
                    {
                        // If any preserved values are invalid, they'll use defaults
                    }

                    wasRepaired = true;
                    Debug.Log("VRTify: Repaired invalid settings");
                }

                // Validate and repair apps
                if (apps == null)
                {
                    apps = new List<NotificationApp>();
                    wasRepaired = true;
                }
                else
                {
                    int originalCount = apps.Count;
                    apps.RemoveAll(app => app == null || !app.IsValid());

                    if (apps.Count != originalCount)
                    {
                        wasRepaired = true;
                        Debug.Log($"VRTify: Removed {originalCount - apps.Count} invalid apps");
                    }

                    // Fix duplicate OSC parameters
                    var usedParameters = new HashSet<string>();
                    foreach (var app in apps)
                    {
                        var originalParam = app.oscParameter;
                        int counter = 1;

                        while (usedParameters.Contains(app.oscParameter))
                        {
                            app.oscParameter = $"{originalParam}_{counter}";
                            counter++;
                            wasRepaired = true;
                        }

                        usedParameters.Add(app.oscParameter);
                    }

                    // Reassign priorities to ensure they're sequential
                    for (int i = 0; i < apps.Count; i++)
                    {
                        if (apps[i].priority != i)
                        {
                            apps[i].priority = i;
                            wasRepaired = true;
                        }
                    }
                }

                if (wasRepaired)
                {
                    Debug.Log("VRTify: Settings validation and repair completed");
                }

                return !wasRepaired; // Return true if no repairs were needed
            }
            catch (System.Exception e)
            {
                Debug.LogError($"VRTify: Failed to validate and repair settings: {e.Message}");

                // Last resort: reset everything
                settings = new VRTifySettings();
                apps = new List<NotificationApp>();

                return false;
            }
        }

        /// <summary>
        /// Gets current system status
        /// </summary>
        public static VRTifySystemStatus GetSystemStatus()
        {
            try
            {
                var statusPath = Path.Combine(VRTifyFileManager.SETTINGS_PATH, "SystemStatus.json");
                var statusJson = VRTifyFileManager.ReadTextFile(statusPath);

                if (!string.IsNullOrEmpty(statusJson))
                {
                    var status = JsonUtility.FromJson<VRTifySystemStatus>(statusJson);
                    if (status != null)
                    {
                        return status;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"VRTify: Could not load system status: {e.Message}");
            }

            return new VRTifySystemStatus();
        }

        /// <summary>
        /// Updates system status
        /// </summary>
        public static void UpdateSystemStatus(VRTifySystemStatus status)
        {
            try
            {
                var statusJson = JsonUtility.ToJson(status, true);
                var statusPath = Path.Combine(VRTifyFileManager.SETTINGS_PATH, "SystemStatus.json");
                VRTifyFileManager.WriteTextFile(statusPath, statusJson);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"VRTify: Could not save system status: {e.Message}");
            }
        }

        /// <summary>
        /// Internal method to update system status during save
        /// </summary>
        private static void UpdateSystemStatusInternal(VRTifySettings settings, List<NotificationApp> apps)
        {
            try
            {
                var status = GetSystemStatus();
                status.isInitialized = true;
                status.hasValidApps = apps.Count > 0;
                status.enabledAppsCount = apps.Count(a => a.enabled);
                // Note: hasValidAvatar is updated elsewhere when avatar is selected

                UpdateSystemStatus(status);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"VRTify: Could not update system status: {e.Message}");
            }
        }

        /// <summary>
        /// Cleans up old backup files (keeps only the most recent 10)
        /// </summary>
        public static void CleanupOldBackups()
        {
            try
            {
                var backups = GetAvailableBackups();
                const int maxBackupsToKeep = 10;

                if (backups.Count > maxBackupsToKeep)
                {
                    var backupsToDelete = backups.Skip(maxBackupsToKeep);

                    foreach (var backup in backupsToDelete)
                    {
                        try
                        {
                            Directory.Delete(backup.path, true);
                            Debug.Log($"VRTify: Deleted old backup: {backup.name}");
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogWarning($"VRTify: Could not delete backup {backup.name}: {e.Message}");
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"VRTify: Failed to cleanup old backups: {e.Message}");
            }
        }

        /// <summary>
        /// Resets all settings to factory defaults
        /// </summary>
        public static void ResetToFactoryDefaults()
        {
            try
            {
                // Create backup before reset
                CreateSettingsBackup("Before_Factory_Reset");

                // Reset to defaults
                var defaultSettings = new VRTifySettings();
                var emptyApps = new List<NotificationApp>();

                SaveSettings(defaultSettings, emptyApps);

                // Reset system status
                var status = new VRTifySystemStatus
                {
                    isInitialized = false,
                    hasValidAvatar = false,
                    hasValidApps = false,
                    enabledAppsCount = 0,
                    lastGeneration = System.DateTime.Now
                };
                UpdateSystemStatus(status);

                Debug.Log("VRTify: Reset to factory defaults completed");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"VRTify: Failed to reset to factory defaults: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gets disk usage information for VRTify data
        /// </summary>
        public static (long settingsSize, long backupsSize, long totalSize) GetDataUsageInfo()
        {
            long settingsSize = 0;
            long backupsSize = 0;

            try
            {
                // Calculate settings files size
                var settingsFiles = new[]
                {
                    VRTifyFileManager.SETTINGS_FILE,
                    VRTifyFileManager.APPS_FILE,
                    VRTifyFileManager.OSC_CONFIG_FILE,
                    Path.Combine(VRTifyFileManager.SETTINGS_PATH, "SystemStatus.json")
                };

                foreach (var file in settingsFiles)
                {
                    if (File.Exists(file))
                    {
                        settingsSize += new FileInfo(file).Length;
                    }
                }

                // Calculate backups size
                var backupsDir = Path.Combine(VRTifyFileManager.OUTPUT_PATH, "Backups");
                if (Directory.Exists(backupsDir))
                {
                    var backupFiles = Directory.GetFiles(backupsDir, "*", SearchOption.AllDirectories);
                    foreach (var file in backupFiles)
                    {
                        backupsSize += new FileInfo(file).Length;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"VRTify: Could not calculate data usage: {e.Message}");
            }

            return (settingsSize, backupsSize, settingsSize + backupsSize);
        }

        /// <summary>
        /// Migrates settings from older versions
        /// </summary>
        private static VRTifySettings MigrateSettings(VRTifySettings settings, string fromVersion)
        {
            try
            {
                Debug.Log($"VRTify: Migrating settings from version {fromVersion} to {SETTINGS_VERSION}");

                // Add migration logic here for future versions
                switch (fromVersion)
                {
                    case "0.9.0":
                        // Example migration: add new default values
                        if (!settings.enableVROptimizations)
                            settings.enableVROptimizations = true;
                        break;

                    default:
                        Debug.LogWarning($"VRTify: Unknown settings version {fromVersion}, using current settings as-is");
                        break;
                }

                // Ensure settings are valid after migration
                if (!settings.ValidateSettings())
                {
                    Debug.LogWarning("VRTify: Migrated settings failed validation, resetting to defaults");
                    settings.ResetToDefaults();
                }

                return settings;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"VRTify: Settings migration failed: {e.Message}");
                var newSettings = new VRTifySettings();
                newSettings.ResetToDefaults();
                return newSettings;
            }
        }

        /// <summary>
        /// Migrates app configurations from older versions
        /// </summary>
        private static List<NotificationApp> MigrateApps(List<NotificationApp> apps, string fromVersion)
        {
            try
            {
                Debug.Log($"VRTify: Migrating apps from version {fromVersion} to {SETTINGS_VERSION}");

                var migratedApps = new List<NotificationApp>();

                foreach (var app in apps)
                {
                    if (app != null)
                    {
                        // Apply version-specific migrations
                        switch (fromVersion)
                        {
                            case "0.9.0":
                                // Example: ensure new properties have default values
                                if (app.animationDuration <= 0)
                                    app.animationDuration = 0.3f;
                                break;
                        }

                        // Validate app after migration
                        if (app.IsValid())
                        {
                            migratedApps.Add(app);
                        }
                        else
                        {
                            Debug.LogWarning($"VRTify: App {app.appName} failed validation after migration, skipping");
                        }
                    }
                }

                return migratedApps;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"VRTify: Apps migration failed: {e.Message}");
                return new List<NotificationApp>();
            }
        }

        #region Helper Classes

        [System.Serializable]
        private class VersionedSettings
        {
            public string version;
            public string timestamp;
            public string unityVersion;
            public VRTifySettings settings;
        }

        [System.Serializable]
        private class VersionedApps
        {
            public string version;
            public string timestamp;
            public List<NotificationApp> apps;
        }

        // Ändere die Sichtbarkeit von BackupInfo von private zu public, damit sie mit BackupEntry.info kompatibel ist.
        [System.Serializable]
        public class BackupInfo
        {
            public string backupDate;
            public string vrtifyVersion;
            public string unityVersion;
            public string platform;
            public int filesBackedUp;
            public string backupType;
        }

        [System.Serializable]
        public class BackupEntry
        {
            public string path;
            public string name;
            public BackupInfo info;
            public System.DateTime creationTime;
        }

        [System.Serializable]
        private class ExportData
        {
            public string exportDate;
            public string vrtifyVersion;
            public VRTifySettings settings;
            public List<NotificationApp> apps;
            public SystemInfo systemInfo;

            [System.Serializable]
            public class SystemInfo
            {
                public string unityVersion;
                public string platform;
                public string operatingSystem;
            }
        }

        #endregion
    }
}