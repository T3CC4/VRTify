using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace VRTify.Core
{
    /// <summary>
    /// Manages file and directory operations for the VRTify system
    /// </summary>
    public static class VRTifyFileManager
    {
        // Base paths for VRTify directory structure
        public const string BASE_PATH = "Assets/VRTify";
        public const string EDITOR_PATH = BASE_PATH + "/Editor";
        public const string RUNTIME_PATH = BASE_PATH + "/Runtime";
        public const string RESOURCES_PATH = BASE_PATH + "/Resources";
        public const string TEXTURES_PATH = BASE_PATH + "/Textures";
        public const string MATERIALS_PATH = BASE_PATH + "/Materials";
        public const string SHADERS_PATH = BASE_PATH + "/Shaders";
        public const string ANIMATIONS_PATH = BASE_PATH + "/Animations";
        public const string CONTROLLERS_PATH = BASE_PATH + "/Controllers";
        public const string PREFABS_PATH = BASE_PATH + "/Prefabs";
        public const string SETTINGS_PATH = BASE_PATH + "/Settings";
        public const string OUTPUT_PATH = BASE_PATH + "/Output";
        public const string DOCUMENTATION_PATH = BASE_PATH + "/Documentation";

        // Specific file paths
        public const string SETTINGS_FILE = SETTINGS_PATH + "/VRTifySettings.json";
        public const string APPS_FILE = SETTINGS_PATH + "/VRTifyApps.json";
        public const string OSC_CONFIG_FILE = SETTINGS_PATH + "/OSCConfig.json";

        /// <summary>
        /// Creates the complete VRTify directory structure
        /// </summary>
        public static void CreateDirectoryStructure()
        {
            var directories = new[]
            {
                // Core directories
                BASE_PATH,
                
                // Editor directories
                EDITOR_PATH,
                EDITOR_PATH + "/Core",
                EDITOR_PATH + "/Windows",
                EDITOR_PATH + "/Generators",
                EDITOR_PATH + "/Utils",
                EDITOR_PATH + "/Icons",
                
                // Runtime directories
                RUNTIME_PATH,
                RUNTIME_PATH + "/Scripts",
                RUNTIME_PATH + "/Components",
                RUNTIME_PATH + "/Data",
                
                // Asset directories
                RESOURCES_PATH,
                TEXTURES_PATH,
                MATERIALS_PATH,
                SHADERS_PATH,
                ANIMATIONS_PATH,
                CONTROLLERS_PATH,
                PREFABS_PATH,
                SETTINGS_PATH,
                OUTPUT_PATH,
                OUTPUT_PATH + "/OSC",
                OUTPUT_PATH + "/Builds",
                DOCUMENTATION_PATH,
                
                // Generated content subdirectories
                TEXTURES_PATH + "/Generated",
                MATERIALS_PATH + "/Generated",
                SHADERS_PATH + "/Generated",
                ANIMATIONS_PATH + "/Generated",
                CONTROLLERS_PATH + "/Generated"
            };

            foreach (var directory in directories)
            {
                EnsureDirectoryExists(directory);
            }

            CreateReadmeFiles();
            CreateAssemblyDefinitions();
            CreateGitIgnoreFile();

            AssetDatabase.Refresh();
            Debug.Log("VRTify: Directory structure created successfully!");
        }

        /// <summary>
        /// Ensures a directory exists, creates it if it doesn't
        /// </summary>
        public static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                Debug.Log($"VRTify: Created directory: {path}");
            }
        }

        /// <summary>
        /// Gets a safe file name by removing invalid characters
        /// </summary>
        public static string GetSafeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var invalidChar in invalidChars)
            {
                fileName = fileName.Replace(invalidChar, '_');
            }
            return fileName;
        }

        /// <summary>
        /// Gets the next available file name if the file already exists
        /// </summary>
        public static string GetUniqueFileName(string basePath, string fileName, string extension)
        {
            var fullPath = Path.Combine(basePath, fileName + extension);
            if (!File.Exists(fullPath))
                return fullPath;

            int counter = 1;
            while (File.Exists(Path.Combine(basePath, $"{fileName}_{counter}{extension}")))
            {
                counter++;
            }

            return Path.Combine(basePath, $"{fileName}_{counter}{extension}");
        }

        /// <summary>
        /// Creates README files for documentation
        /// </summary>
        private static void CreateReadmeFiles()
        {
            // Main README
            var mainReadme = @"# VRTify - Dynamic VR Notification System

VRTify is a comprehensive notification system for VRChat that automatically detects installed applications and creates a dynamic HUD for displaying notifications in VR.

## Features

🔔 **Dynamic App Detection** - Automatically finds and configures installed applications
📱 **Smart Icon Extraction** - Extracts high-quality icons from applications
🎭 **VRChat Integration** - Seamless Avatar 3.0 integration with OSC
🎨 **Customizable HUD** - Flexible positioning and styling options
🎬 **Smooth Animations** - Professional fade, scale, and slide animations
⚡ **Real-time Monitoring** - Live notification monitoring via OSC
🎯 **Priority System** - Smart notification prioritization and queuing
🔧 **Easy Setup** - One-click generation and configuration

## Directory Structure

- **Editor/**: Unity Editor scripts and tools
- **Runtime/**: Runtime scripts and components  
- **Shaders/**: Generated and custom shaders
- **Materials/**: Generated materials
- **Animations/**: Generated animation clips and controllers
- **Textures/**: Icon atlases and textures
- **Prefabs/**: Reusable prefabs
- **Settings/**: Configuration files
- **Output/**: Generated OSC applications and builds
- **Documentation/**: User guides and technical docs

## Quick Start

1. Open VRTify: `Tools > VRTify > Main Editor`
2. Select your VRChat avatar
3. Configure desired applications  
4. Click ""Generate Complete VRTify System""
5. Upload your avatar to VRChat
6. Run the generated OSC monitor application

## Requirements

- Unity 2022.3.22f1 (VRChat current version)
- VRChat SDK3 - Avatars
- Windows 10/11 (for notification monitoring)

## Version: 1.0.0
## License: MIT
";

            WriteTextFile(Path.Combine(BASE_PATH, "README.md"), mainReadme);

            // Editor README
            var editorReadme = @"# VRTify Editor Scripts

This directory contains all Unity Editor scripts for the VRTify system.

## Structure

- **Core/**: Core functionality and file management
- **Windows/**: Editor window implementations
- **Generators/**: Code generators for shaders, animations, etc.
- **Utils/**: Utility classes and helpers
- **Icons/**: Icon extraction and processing

## Key Classes

- `VRTifyMainEditor`: Main editor window
- `VRTifyFileManager`: File and directory management
- `VRTifyIconExtractor`: Application icon extraction
- `VRTifyShaderGenerator`: Dynamic shader generation
- `VRTifyAnimatorSetup`: Animator controller setup
- `VRTifySystemGenerator`: Complete system generation
";

            WriteTextFile(Path.Combine(EDITOR_PATH, "README.md"), editorReadme);

            // Runtime README
            var runtimeReadme = @"# VRTify Runtime

This directory contains runtime scripts and components for the VRTify system.

## Structure

- **Scripts/**: Core runtime functionality
- **Components/**: MonoBehaviour components
- **Data/**: Data structures and serializable classes

## Components

- **VRTifyNotificationHUD**: Main HUD component for notifications
- **VRTifyNotificationManager**: Manages notification state and animations
- **VRTifyOSCReceiver**: Receives OSC messages from external applications
";

            WriteTextFile(Path.Combine(RUNTIME_PATH, "README.md"), runtimeReadme);
        }

        /// <summary>
        /// Creates assembly definition files for proper Unity compilation
        /// </summary>
        private static void CreateAssemblyDefinitions()
        {
            // Editor Assembly Definition
            var editorAsmDef = @"{
    ""name"": ""VRTify.Editor"",
    ""rootNamespace"": ""VRTify.Editor"",
    ""references"": [
        ""VRTify.Runtime"",
        ""VRC.SDK3A"",
        ""VRC.SDK3A.Editor""
    ],
    ""includePlatforms"": [
        ""Editor""
    ],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": false,
    ""overrideReferences"": false,
    ""precompiledReferences"": [],
    ""autoReferenced"": true,
    ""defineConstraints"": [],
    ""versionDefines"": [],
    ""noEngineReferences"": false
}";

            WriteTextFile(Path.Combine(EDITOR_PATH, "VRTify.Editor.asmdef"), editorAsmDef);

            // Runtime Assembly Definition
            var runtimeAsmDef = @"{
    ""name"": ""VRTify.Runtime"",
    ""rootNamespace"": ""VRTify"",
    ""references"": [
        ""VRC.SDK3A""
    ],
    ""includePlatforms"": [],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": false,
    ""overrideReferences"": false,
    ""precompiledReferences"": [],
    ""autoReferenced"": true,
    ""defineConstraints"": [],
    ""versionDefines"": [],
    ""noEngineReferences"": false
}";

            WriteTextFile(Path.Combine(RUNTIME_PATH, "VRTify.Runtime.asmdef"), runtimeAsmDef);
        }

        /// <summary>
        /// Creates .gitignore file for version control
        /// </summary>
        private static void CreateGitIgnoreFile()
        {
            var gitignore = @"# VRTify Generated Files
/Output/Builds/
/Output/OSC/*.exe
/Textures/Generated/
/Materials/Generated/
/Shaders/Generated/
/Animations/Generated/
/Controllers/Generated/

# Temporary Files
*.tmp
*.temp
~*

# User Settings (keep structure, ignore content)
/Settings/*.json
!/Settings/README.md

# Unity Meta Files for Generated Content
/Output/**/*.meta
";

            WriteTextFile(Path.Combine(BASE_PATH, ".gitignore"), gitignore);
        }

        /// <summary>
        /// Safely writes text to a file
        /// </summary>
        public static void WriteTextFile(string path, string content)
        {
            try
            {
                EnsureDirectoryExists(Path.GetDirectoryName(path));
                File.WriteAllText(path, content);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"VRTify: Failed to write file {path}: {e.Message}");
            }
        }

        /// <summary>
        /// Safely reads text from a file
        /// </summary>
        public static string ReadTextFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    return File.ReadAllText(path);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"VRTify: Failed to read file {path}: {e.Message}");
            }
            return null;
        }

        /// <summary>
        /// Safely writes bytes to a file
        /// </summary>
        public static void WriteBytesToFile(string path, byte[] data)
        {
            try
            {
                EnsureDirectoryExists(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, data);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"VRTify: Failed to write bytes to {path}: {e.Message}");
            }
        }

        /// <summary>
        /// Gets all files in a directory with a specific extension
        /// </summary>
        public static string[] GetFilesWithExtension(string directory, string extension)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    return Directory.GetFiles(directory, $"*.{extension}", SearchOption.AllDirectories);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"VRTify: Failed to get files from {directory}: {e.Message}");
            }
            return new string[0];
        }

        /// <summary>
        /// Cleans up generated files (useful for regeneration)
        /// </summary>
        public static void CleanGeneratedFiles()
        {
            var generatedDirectories = new[]
            {
                TEXTURES_PATH + "/Generated",
                MATERIALS_PATH + "/Generated",
                SHADERS_PATH + "/Generated",
                ANIMATIONS_PATH + "/Generated",
                CONTROLLERS_PATH + "/Generated"
            };

            foreach (var dir in generatedDirectories)
            {
                if (Directory.Exists(dir))
                {
                    try
                    {
                        Directory.Delete(dir, true);
                        EnsureDirectoryExists(dir);
                        Debug.Log($"VRTify: Cleaned generated directory: {dir}");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"VRTify: Could not clean directory {dir}: {e.Message}");
                    }
                }
            }

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Gets the relative Unity path from an absolute path
        /// </summary>
        public static string GetRelativeUnityPath(string absolutePath)
        {
            var dataPath = Application.dataPath;
            var assetsPath = dataPath.Substring(0, dataPath.Length - "Assets".Length);

            if (absolutePath.StartsWith(assetsPath))
            {
                return absolutePath.Substring(assetsPath.Length).Replace('\\', '/');
            }

            return absolutePath.Replace('\\', '/');
        }

        /// <summary>
        /// Creates a backup of important files before regeneration
        /// </summary>
        public static void CreateBackup()
        {
            var backupDir = Path.Combine(OUTPUT_PATH, "Backups", System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
            EnsureDirectoryExists(backupDir);

            // Backup settings
            if (File.Exists(SETTINGS_FILE))
            {
                File.Copy(SETTINGS_FILE, Path.Combine(backupDir, "VRTifySettings.json"));
            }

            if (File.Exists(APPS_FILE))
            {
                File.Copy(APPS_FILE, Path.Combine(backupDir, "VRTifyApps.json"));
            }

            Debug.Log($"VRTify: Backup created at {backupDir}");
        }

        /// <summary>
        /// Validates that all required directories exist
        /// </summary>
        public static bool ValidateDirectoryStructure()
        {
            var requiredDirectories = new[]
            {
                BASE_PATH, EDITOR_PATH, RUNTIME_PATH, SETTINGS_PATH, OUTPUT_PATH
            };

            foreach (var dir in requiredDirectories)
            {
                if (!Directory.Exists(dir))
                {
                    Debug.LogError($"VRTify: Required directory missing: {dir}");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Gets disk usage information for VRTify directories
        /// </summary>
        public static long GetVRTifyDiskUsage()
        {
            long totalSize = 0;

            try
            {
                if (Directory.Exists(BASE_PATH))
                {
                    var files = Directory.GetFiles(BASE_PATH, "*", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        totalSize += new FileInfo(file).Length;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"VRTify: Could not calculate disk usage: {e.Message}");
            }

            return totalSize;
        }
    }
}