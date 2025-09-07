using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using System.Drawing;
using System.Drawing.Imaging;
using VRTify.Data;
using VRTify.Core;

namespace VRTify.Editor.Icons
{
    /// <summary>
    /// Handles extraction of application icons from the Windows system
    /// </summary>
    public class VRTifyIconExtractor
    {
        private readonly Dictionary<string, string> commonApps = new Dictionary<string, string>
        {
            { "Discord", "Discord" },
            { "Steam", "Steam" },
            { "Spotify", "Spotify" },
            { "WhatsApp", "WhatsApp Desktop" },
            { "Telegram", "Telegram Desktop" },
            { "Slack", "Slack" },
            { "Teams", "Microsoft Teams" },
            { "Chrome", "Google Chrome" },
            { "Firefox", "Mozilla Firefox" },
            { "Edge", "Microsoft Edge" },
            { "Twitch", "Twitch" },
            { "OBS", "OBS Studio" },
            { "VLC", "VLC Media Player" },
            { "VSCode", "Visual Studio Code" },
            { "Notepad++", "Notepad++" },
            { "Blender", "Blender" },
            { "Photoshop", "Adobe Photoshop" },
            { "Unity", "Unity" },
            { "Skype", "Skype" },
            { "Zoom", "Zoom" },
            { "Epic Games", "Epic Games Launcher" },
            { "Battle.net", "Battle.net" },
            { "Origin", "Origin" },
            { "Uplay", "Ubisoft Connect" },
            { "iTunes", "iTunes" },
            { "Audacity", "Audacity" }
        };

        private readonly Dictionary<string, string[]> alternativeExecutableNames = new Dictionary<string, string[]>
        {
            { "Discord", new[] { "Discord.exe", "DiscordCanary.exe", "DiscordPTB.exe" } },
            { "Chrome", new[] { "chrome.exe", "GoogleChrome.exe" } },
            { "Firefox", new[] { "firefox.exe", "FirefoxESR.exe" } },
            { "VSCode", new[] { "Code.exe", "Code - Insiders.exe" } },
            { "Teams", new[] { "Teams.exe", "ms-teams.exe" } },
            { "OBS", new[] { "obs64.exe", "obs32.exe", "obs-studio.exe" } }
        };

        /// <summary>
        /// Scans the system for installed applications and returns a list of NotificationApp objects
        /// </summary>
        public List<NotificationApp> ScanForInstalledApps()
        {
            var apps = new List<NotificationApp>();
            
            Debug.Log("VRTify: Starting enhanced application scan...");
            
            // 1. Scan Discord specifically with multiple methods
            ScanDiscordSpecifically(apps);
            
            // 2. Scan common installation directories
            ScanDirectory(@"C:\Program Files", apps);
            ScanDirectory(@"C:\Program Files (x86)", apps);
            ScanDirectory(Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Programs"), apps);
            
            // 3. Scan user AppData for portable apps
            ScanUserAppData(apps);
            
            // 4. Scan Windows Store apps
            ScanWindowsStoreApps(apps);
            
            // 5. Scan registry for installed programs
            ScanRegistry(apps);
            
            // 6. Scan Steam games directory
            ScanSteamGames(apps);
            
            // 7. Scan for running processes (last resort)
            ScanRunningProcesses(apps);
            
            // Remove duplicates and sort by name
            apps = apps.GroupBy(a => a.appName.ToLower())
                        .Select(g => g.OrderBy(a => !string.IsNullOrEmpty(a.iconPath)).First())
                        .OrderBy(a => a.displayName)
                        .ToList();
            
            // Assign OSC parameters and validate
            for (int i = 0; i < apps.Count; i++)
            {
                apps[i].oscParameter = $"/avatar/parameters/{apps[i].GetSanitizedParameterName()}";
                apps[i].priority = i;
                
                // Validate icon extraction
                if (!string.IsNullOrEmpty(apps[i].iconPath))
                {
                    var testTexture = ExtractIconAsTexture(apps[i].iconPath);
                    if (testTexture == null)
                    {
                        Debug.LogWarning($"VRTify: Could not extract icon for {apps[i].displayName}");
                        apps[i].iconPath = ""; // Clear invalid path
                    }
                    else
                    {
                        Object.DestroyImmediate(testTexture);
                    }
                }
            }
            
            Debug.Log($"VRTify: Scan completed. Found {apps.Count} applications.");
            return apps;
        }

        /// <summary>
        /// Specifically scans for Discord in all known locations
        /// </summary>
        private void ScanDiscordSpecifically(List<NotificationApp> apps)
        {
            Debug.Log("VRTify: Scanning specifically for Discord...");
            
            var discordPaths = new[]
            {
                // Discord Desktop App locations
                Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Discord"),
                Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "Discord"),
                @"C:\Users\" + System.Environment.UserName + @"\AppData\Local\Discord",
                @"C:\Users\" + System.Environment.UserName + @"\AppData\Roaming\Discord",
                
                // Discord PTB (Public Test Build)
                Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "DiscordPTB"),
                
                // Discord Canary
                Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "DiscordCanary"),
                
                // Discord Development
                Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "DiscordDevelopment"),
                
                // Portable installations
                @"C:\Discord",
                @"D:\Discord",
                @"C:\Program Files\Discord",
                @"C:\Program Files (x86)\Discord"
            };
            
            foreach (var basePath in discordPaths)
            {
                if (Directory.Exists(basePath))
                {
                    Debug.Log($"VRTify: Found Discord directory: {basePath}");
                    
                    // Look for Discord executable
                    var discordExe = FindDiscordExecutable(basePath);
                    if (!string.IsNullOrEmpty(discordExe))
                    {
                        var discordType = GetDiscordType(basePath);
                        var app = new NotificationApp("Discord", $"Discord{discordType}")
                        {
                            iconPath = discordExe,
                            executablePath = discordExe,
                            enabled = true
                        };
                        
                        apps.Add(app);
                        Debug.Log($"VRTify: Found {app.displayName} at {discordExe}");
                    }
                }
            }
            
            // Also check Windows Store Discord
            var storeDiscordPath = FindWindowsStoreDiscord();
            if (!string.IsNullOrEmpty(storeDiscordPath))
            {
                var app = new NotificationApp("Discord", "Discord (Store)")
                {
                    iconPath = storeDiscordPath,
                    executablePath = storeDiscordPath,
                    enabled = true
                };
                apps.Add(app);
                Debug.Log($"VRTify: Found Discord Store version");
            }
        }

        /// <summary>
        /// Finds Discord executable in a given directory
        /// </summary>
        private string FindDiscordExecutable(string directory)
        {
            try
            {
                // Discord usually installs in versioned subdirectories
                var possiblePaths = new[]
                {
                    Path.Combine(directory, "Discord.exe"),
                    Path.Combine(directory, "app-*", "Discord.exe"), // Wildcard pattern
                    Path.Combine(directory, "Update.exe") // Updater can also be used
                };
                
                // Check direct executable
                var directExe = Path.Combine(directory, "Discord.exe");
                if (File.Exists(directExe))
                {
                    return directExe;
                }
                
                // Check versioned subdirectories
                var appDirs = Directory.GetDirectories(directory, "app-*", SearchOption.TopDirectoryOnly);
                foreach (var appDir in appDirs.OrderByDescending(d => d)) // Get latest version
                {
                    var discordExe = Path.Combine(appDir, "Discord.exe");
                    if (File.Exists(discordExe))
                    {
                        return discordExe;
                    }
                }
                
                // Check for Update.exe as fallback
                var updateExe = Path.Combine(directory, "Update.exe");
                if (File.Exists(updateExe))
                {
                    return updateExe;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"VRTify: Error searching for Discord in {directory}: {e.Message}");
            }
            
            return null;
        }

        /// <summary>
        /// Determines Discord type from path
        /// </summary>
        private string GetDiscordType(string path)
        {
            if (path.ToLower().Contains("ptb"))
                return " PTB";
            if (path.ToLower().Contains("canary"))
                return " Canary";
            if (path.ToLower().Contains("development"))
                return " Development";
            
            return ""; // Stable version
        }

        /// <summary>
        /// Finds Windows Store version of Discord
        /// </summary>
        private string FindWindowsStoreDiscord()
        {
            try
            {
                var storeAppsPath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Packages");
                if (!Directory.Exists(storeAppsPath)) return null;

                var discordPackages = new[]
                {
                    "DiscordInc.Discord",
                    "Discord.Discord",
                    "53621DiscordInc.Discord"
                };

                foreach (var packageName in discordPackages)
                {
                    var packageDirs = Directory.GetDirectories(storeAppsPath, $"{packageName}*", SearchOption.TopDirectoryOnly);
                    foreach (var packageDir in packageDirs)
                    {
                        var manifestPath = Path.Combine(packageDir, "AppxManifest.xml");
                        if (File.Exists(manifestPath))
                        {
                            // Try to find app icon
                            var iconPath = ExtractStoreAppIcon(packageDir);
                            if (!string.IsNullOrEmpty(iconPath))
                            {
                                return iconPath;
                            }
                            // Return package dir as fallback
                            return packageDir;
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"VRTify: Error scanning for Windows Store Discord: {e.Message}");
            }
            
            return null;
        }

        /// <summary>
        /// Scans user AppData for portable applications
        /// </summary>
        private void ScanUserAppData(List<NotificationApp> apps)
        {
            try
            {
                var appDataPaths = new[]
                {
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData)
                };
                
                foreach (var appDataPath in appDataPaths)
                {
                    foreach (var appKey in commonApps.Keys)
                    {
                        var appDir = Path.Combine(appDataPath, appKey);
                        if (Directory.Exists(appDir))
                        {
                            var executablePath = FindExecutable(appDir, appKey);
                            if (!string.IsNullOrEmpty(executablePath))
                            {
                                var app = new NotificationApp(appKey, commonApps[appKey])
                                {
                                    iconPath = executablePath,
                                    executablePath = executablePath,
                                    enabled = true
                                };
                                
                                apps.Add(app);
                                Debug.Log($"VRTify: Found {app.displayName} in AppData at {executablePath}");
                            }
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"VRTify: Could not scan user AppData: {e.Message}");
            }
        }

        /// <summary>
        /// Scans currently running processes for applications
        /// </summary>
        private void ScanRunningProcesses(List<NotificationApp> apps)
        {
            try
            {
                Debug.Log("VRTify: Scanning running processes...");
                
                var processes = System.Diagnostics.Process.GetProcesses();
                var existingAppNames = apps.Select(a => a.appName.ToLower()).ToHashSet();
                
                foreach (var process in processes)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(process.MainModule?.FileName))
                        {
                            var processName = process.ProcessName.ToLower();
                            var fileName = Path.GetFileNameWithoutExtension(process.MainModule.FileName).ToLower();
                            
                            foreach (var appKey in commonApps.Keys)
                            {
                                var appKeyLower = appKey.ToLower();
                                
                                if (!existingAppNames.Contains(appKeyLower) && 
                                    (processName.Contains(appKeyLower) || fileName.Contains(appKeyLower)))
                                {
                                    var app = new NotificationApp(appKey, commonApps[appKey])
                                    {
                                        iconPath = process.MainModule.FileName,
                                        executablePath = process.MainModule.FileName,
                                        enabled = true
                                    };
                                    
                                    apps.Add(app);
                                    existingAppNames.Add(appKeyLower);
                                    Debug.Log($"VRTify: Found {app.displayName} via running process at {app.executablePath}");
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Skip processes we can't access
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"VRTify: Could not scan running processes: {e.Message}");
            }
        }

        /// <summary>
        /// Scans a directory for applications
        /// </summary>
        private void ScanDirectory(string directory, List<NotificationApp> apps)
        {
            if (!Directory.Exists(directory))
            {
                Debug.LogWarning($"VRTify: Directory does not exist: {directory}");
                return;
            }

            try
            {
                foreach (var subDir in Directory.GetDirectories(directory))
                {
                    var dirName = Path.GetFileName(subDir);

                    foreach (var appKey in commonApps.Keys)
                    {
                        if (dirName.ToLower().Contains(appKey.ToLower()) ||
                            commonApps[appKey].ToLower().Contains(dirName.ToLower()))
                        {
                            var executablePath = FindExecutable(subDir, appKey);
                            if (!string.IsNullOrEmpty(executablePath))
                            {
                                var app = new NotificationApp(appKey, commonApps[appKey])
                                {
                                    iconPath = executablePath,
                                    executablePath = executablePath,
                                    enabled = true
                                };

                                apps.Add(app);
                                Debug.Log($"VRTify: Found {app.displayName} at {executablePath}");
                            }
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"VRTify: Could not scan directory {directory}: {e.Message}");
            }
        }

        /// <summary>
        /// Scans Windows Store applications
        /// </summary>
        private void ScanWindowsStoreApps(List<NotificationApp> apps)
        {
            try
            {
                var storeAppsPath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Packages");
                if (!Directory.Exists(storeAppsPath)) return;

                var storeAppMappings = new Dictionary<string, string>
                {
                    { "Microsoft.SkypeApp", "Skype" },
                    { "SpotifyAB.SpotifyMusic", "Spotify" },
                    { "Microsoft.MicrosoftTeams", "Microsoft Teams" },
                    { "5319275A.WhatsAppDesktop", "WhatsApp" },
                    { "TelegramDesktop", "Telegram" },
                    { "Microsoft.MicrosoftEdge", "Microsoft Edge" },
                    { "DiscordInc.Discord", "Discord" }
                };

                foreach (var packageDir in Directory.GetDirectories(storeAppsPath))
                {
                    var packageName = Path.GetFileName(packageDir);

                    foreach (var mapping in storeAppMappings)
                    {
                        if (packageName.Contains(mapping.Key))
                        {
                            var manifestPath = Path.Combine(packageDir, "AppxManifest.xml");
                            if (File.Exists(manifestPath))
                            {
                                var iconPath = ExtractStoreAppIcon(packageDir);
                                var app = new NotificationApp(mapping.Value, mapping.Value)
                                {
                                    iconPath = iconPath,
                                    executablePath = packageDir,
                                    enabled = true
                                };

                                apps.Add(app);
                                Debug.Log($"VRTify: Found Windows Store app {app.displayName}");
                            }
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"VRTify: Could not scan Windows Store apps: {e.Message}");
            }
        }

        /// <summary>
        /// Scans registry for installed programs
        /// </summary>
        private void ScanRegistry(List<NotificationApp> apps)
        {
            try
            {
                var registryPaths = new[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                };

                foreach (var registryPath in registryPaths)
                {
                    using (var uninstallKey = Registry.LocalMachine.OpenSubKey(registryPath))
                    {
                        if (uninstallKey != null)
                        {
                            ScanRegistryKey(uninstallKey, apps);
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"VRTify: Could not scan registry: {e.Message}");
            }
        }

        /// <summary>
        /// Scans a specific registry key for applications
        /// </summary>
        private void ScanRegistryKey(RegistryKey parentKey, List<NotificationApp> apps)
        {
            foreach (var subKeyName in parentKey.GetSubKeyNames())
            {
                try
                {
                    using (var subKey = parentKey.OpenSubKey(subKeyName))
                    {
                        var displayName = subKey?.GetValue("DisplayName")?.ToString();
                        var installLocation = subKey?.GetValue("InstallLocation")?.ToString();
                        var displayIcon = subKey?.GetValue("DisplayIcon")?.ToString();

                        if (!string.IsNullOrEmpty(displayName))
                        {
                            foreach (var appKey in commonApps.Keys)
                            {
                                if (displayName.ToLower().Contains(appKey.ToLower()) ||
                                    appKey.ToLower().Contains(displayName.ToLower().Split(' ')[0]))
                                {
                                    string executablePath = null;

                                    // Try display icon first
                                    if (!string.IsNullOrEmpty(displayIcon) && displayIcon.EndsWith(".exe") && File.Exists(displayIcon))
                                    {
                                        executablePath = displayIcon;
                                    }
                                    // Try install location
                                    else if (!string.IsNullOrEmpty(installLocation))
                                    {
                                        executablePath = FindExecutable(installLocation, appKey);
                                    }

                                    if (!string.IsNullOrEmpty(executablePath))
                                    {
                                        var app = new NotificationApp(appKey, displayName)
                                        {
                                            iconPath = executablePath,
                                            executablePath = executablePath,
                                            enabled = true
                                        };

                                        apps.Add(app);
                                        Debug.Log($"VRTify: Found {app.displayName} via registry at {executablePath}");
                                    }
                                }
                            }
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"VRTify: Could not read registry key {subKeyName}: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Scans Steam games directory for additional applications
        /// </summary>
        private void ScanSteamGames(List<NotificationApp> apps)
        {
            try
            {
                var steamPath = GetSteamInstallPath();
                if (!string.IsNullOrEmpty(steamPath))
                {
                    var steamAppsPath = Path.Combine(steamPath, "steamapps", "common");
                    if (Directory.Exists(steamAppsPath))
                    {
                        // Add Steam itself
                        var steamExe = Path.Combine(steamPath, "steam.exe");
                        if (File.Exists(steamExe))
                        {
                            var steamApp = new NotificationApp("Steam", "Steam")
                            {
                                iconPath = steamExe,
                                executablePath = steamExe,
                                enabled = true
                            };
                            apps.Add(steamApp);
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"VRTify: Could not scan Steam directory: {e.Message}");
            }
        }

        /// <summary>
        /// Gets Steam installation path from registry
        /// </summary>
        private string GetSteamInstallPath()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam") ??
                                Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam"))
                {
                    return key?.GetValue("InstallPath")?.ToString();
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Finds executable files in a directory for a specific app
        /// </summary>
        private string FindExecutable(string directory, string appName)
        {
            if (!Directory.Exists(directory)) return null;

            try
            {
                // Check for alternative executable names first
                if (alternativeExecutableNames.ContainsKey(appName))
                {
                    foreach (var execName in alternativeExecutableNames[appName])
                    {
                        var files = Directory.GetFiles(directory, execName, SearchOption.AllDirectories);
                        if (files.Length > 0)
                        {
                            return files[0];
                        }
                    }
                }

                // Try common naming patterns
                var possibleNames = new[]
                {
                    $"{appName}.exe",
                    $"{appName.ToLower()}.exe",
                    $"{appName.Replace(" ", "")}.exe",
                    $"{appName.Replace(" ", "").ToLower()}.exe",
                    $"{appName.Replace(" ", "-").ToLower()}.exe"
                };

                foreach (var name in possibleNames)
                {
                    var files = Directory.GetFiles(directory, name, SearchOption.AllDirectories);
                    if (files.Length > 0)
                    {
                        return files[0];
                    }
                }

                // Fallback: find the largest .exe file (likely the main executable)
                var exeFiles = Directory.GetFiles(directory, "*.exe", SearchOption.AllDirectories)
                                       .Where(f => !Path.GetFileName(f).ToLower().Contains("uninstall") &&
                                                  !Path.GetFileName(f).ToLower().Contains("setup") &&
                                                  !Path.GetFileName(f).ToLower().Contains("installer"))
                                       .OrderByDescending(f => new FileInfo(f).Length)
                                       .ToArray();

                if (exeFiles.Length > 0)
                {
                    return exeFiles[0];
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"VRTify: Could not find executable in {directory}: {e.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extracts icon from Windows Store app package
        /// </summary>
        private string ExtractStoreAppIcon(string packageDir)
        {
            try
            {
                var assetsDir = Path.Combine(packageDir, "Assets");
                if (Directory.Exists(assetsDir))
                {
                    var iconFiles = Directory.GetFiles(assetsDir, "*.png", SearchOption.AllDirectories)
                                           .Where(f => {
                                               var fileName = Path.GetFileName(f).ToLower();
                                               return fileName.Contains("logo") ||
                                                      fileName.Contains("icon") ||
                                                      fileName.Contains("square") ||
                                                      fileName.Contains("app");
                                           })
                                           .OrderByDescending(f => new FileInfo(f).Length)
                                           .ToArray();

                    if (iconFiles.Length > 0)
                    {
                        return iconFiles[0];
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"VRTify: Could not extract store app icon from {packageDir}: {e.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extracts icon from executable or image file and converts to Unity Texture2D
        /// </summary>
        public Texture2D ExtractIconAsTexture(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            try
            {
                var extension = Path.GetExtension(filePath).ToLower();

                if (extension == ".png" || extension == ".jpg" || extension == ".jpeg")
                {
                    // Direct image file
                    var imageBytes = File.ReadAllBytes(filePath);
                    var texture = new Texture2D(2, 2);
                    if (texture.LoadImage(imageBytes))
                    {
                        return texture;
                    }
                    Object.DestroyImmediate(texture);
                }
                else if (extension == ".exe" || extension == ".ico")
                {
                    // Extract icon from executable or ICO file
                    using (var icon = Icon.ExtractAssociatedIcon(filePath))
                    {
                        if (icon != null)
                        {
                            using (var bitmap = icon.ToBitmap())
                            {
                                return BitmapToTexture2D(bitmap);
                            }
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"VRTify: Could not extract icon from {filePath}: {e.Message}");
            }

            return null;
        }

        /// <summary>
        /// Converts System.Drawing.Bitmap to Unity Texture2D
        /// </summary>
        private Texture2D BitmapToTexture2D(Bitmap bitmap)
        {
            try
            {
                using (var memory = new MemoryStream())
                {
                    bitmap.Save(memory, ImageFormat.Png);
                    var pngBytes = memory.ToArray();

                    var texture = new Texture2D(2, 2);
                    if (texture.LoadImage(pngBytes))
                    {
                        return texture;
                    }
                    Object.DestroyImmediate(texture);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"VRTify: Could not convert bitmap to texture: {e.Message}");
            }

            return null;
        }

        /// <summary>
        /// Resizes a texture to the specified dimensions
        /// </summary>
        public Texture2D ResizeTexture(Texture2D source, int width, int height)
        {
            if (source == null) return null;

            var resized = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var sourcePixels = source.GetPixels();
            var resizedPixels = new UnityEngine.Color[width * height];

            float scaleX = (float)source.width / width;
            float scaleY = (float)source.height / height;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int sourceX = Mathf.FloorToInt(x * scaleX);
                    int sourceY = Mathf.FloorToInt(y * scaleY);

                    sourceX = Mathf.Clamp(sourceX, 0, source.width - 1);
                    sourceY = Mathf.Clamp(sourceY, 0, source.height - 1);

                    resizedPixels[y * width + x] = sourcePixels[sourceY * source.width + sourceX];
                }
            }

            resized.SetPixels(resizedPixels);
            resized.Apply();

            return resized;
        }

        /// <summary>
        /// Applies a tint color to a texture
        /// </summary>
        public void ApplyTintToTexture(Texture2D texture, UnityEngine.Color tint)
        {
            if (texture == null || tint == UnityEngine.Color.white) return;

            var pixels = texture.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] *= tint;
            }
            texture.SetPixels(pixels);
            texture.Apply();
        }

        /// <summary>
        /// Generates an icon atlas from a list of applications
        /// </summary>
        public IconAtlasData GenerateIconAtlas(List<NotificationApp> apps, int iconSize)
        {
            var validApps = apps.Where(a => !string.IsNullOrEmpty(a.iconPath)).ToList();
            if (validApps.Count == 0)
            {
                Debug.LogWarning("VRTify: No valid apps with icons found for atlas generation");
                return null;
            }

            var atlasSize = CalculateAtlasSize(validApps.Count, iconSize);
            var atlasData = new IconAtlasData(atlasSize, iconSize);
            var atlas = new Texture2D(atlasSize, atlasSize, TextureFormat.RGBA32, false);

            // Clear atlas with transparent pixels
            var clearPixels = new UnityEngine.Color[atlasSize * atlasSize];
            for (int i = 0; i < clearPixels.Length; i++)
                clearPixels[i] = UnityEngine.Color.clear;
            atlas.SetPixels(clearPixels);

            var iconsPerRow = atlasSize / iconSize;

            for (int i = 0; i < validApps.Count; i++)
            {
                var iconTexture = ExtractIconAsTexture(validApps[i].iconPath);
                if (iconTexture != null)
                {
                    var resizedIcon = ResizeTexture(iconTexture, iconSize, iconSize);

                    var x = (i % iconsPerRow) * iconSize;
                    var y = (i / iconsPerRow) * iconSize;

                    // Apply tint color
                    if (validApps[i].tintColor != UnityEngine.Color.white)
                    {
                        ApplyTintToTexture(resizedIcon, validApps[i].tintColor);
                    }

                    atlas.SetPixels(x, y, iconSize, iconSize, resizedIcon.GetPixels());

                    // Create atlas entry
                    var atlasPosition = new Vector2Int(x, y);
                    var entry = new IconAtlasEntry(validApps[i].appName, atlasPosition, iconSize, atlasSize);
                    atlasData.entries.Add(entry);

                    // Update app position for shader use
                    validApps[i].hudPosition = new Vector2(
                        (float)x / atlasSize,
                        (float)y / atlasSize
                    );

                    Object.DestroyImmediate(resizedIcon);
                    Object.DestroyImmediate(iconTexture);

                    Debug.Log($"VRTify: Added {validApps[i].displayName} to atlas at position ({x}, {y})");
                }
            }

            atlas.Apply();
            atlasData.atlasTexture = atlas;

            // Save atlas to disk
            var atlasPath = Path.Combine(VRTifyFileManager.TEXTURES_PATH, "Generated", "VRTify_IconAtlas.png");
            VRTifyFileManager.EnsureDirectoryExists(Path.GetDirectoryName(atlasPath));

            var pngBytes = atlas.EncodeToPNG();
            VRTifyFileManager.WriteBytesToFile(atlasPath, pngBytes);
            AssetDatabase.Refresh();

            // Load as asset and return
            var assetPath = VRTifyFileManager.GetRelativeUnityPath(atlasPath);
            atlasData.atlasTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);

            Debug.Log($"VRTify: Icon atlas generated successfully with {atlasData.entries.Count} icons");
            return atlasData;
        }

        /// <summary>
        /// Calculates optimal atlas size based on number of icons and icon size
        /// </summary>
        private int CalculateAtlasSize(int iconCount, int iconSize)
        {
            var iconsPerRow = Mathf.CeilToInt(Mathf.Sqrt(iconCount));
            var atlasSize = iconsPerRow * iconSize;

            // Round up to nearest power of 2 for better GPU performance
            atlasSize = Mathf.NextPowerOfTwo(atlasSize);

            // Clamp to reasonable limits
            atlasSize = Mathf.Clamp(atlasSize, 256, 2048);

            return atlasSize;
        }

        /// <summary>
        /// Gets extraction result with detailed information
        /// </summary>
        public IconExtractionResult GetIconExtractionResult(string filePath)
        {
            try
            {
                var texture = ExtractIconAsTexture(filePath);
                if (texture != null)
                {
                    var result = new IconExtractionResult(true, texture)
                    {
                        sourcePath = filePath
                    };
                    return result;
                }
                else
                {
                    return new IconExtractionResult(false, null, "Could not extract icon from file");
                }
            }
            catch (System.Exception e)
            {
                return new IconExtractionResult(false, null, e.Message);
            }
        }
    }
}