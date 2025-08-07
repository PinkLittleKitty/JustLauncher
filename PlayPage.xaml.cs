using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace JustLauncher
{
    public partial class PlayPage : UserControl
    {
        private readonly string username;
        private readonly HttpClient httpClient;
        private InstallationsConfig installationsConfig;
        private string installationsConfigPath;
        private string minecraftDirectory;
        private string versionsDirectory;
        private string librariesDirectory;
        private string assetsDirectory;
        private List<MinecraftVersion> availableVersions;

        public PlayPage(string username)
        {
            InitializeComponent();
            this.username = username;

            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "JustLauncher/1.0");

            InitializeDirectories();
            LoadInstallationsConfig();
            RefreshInstallations();
            _ = CheckJavaAndShowCompatibleVersions();
        }

        private void InitializeDirectories()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            minecraftDirectory = Path.Combine(appData, ".minecraft");
            versionsDirectory = Path.Combine(minecraftDirectory, "versions");
            librariesDirectory = Path.Combine(minecraftDirectory, "libraries");
            assetsDirectory = Path.Combine(minecraftDirectory, "assets");
            installationsConfigPath = Path.Combine(minecraftDirectory, "launcher_profiles.json");
            
            Directory.CreateDirectory(minecraftDirectory);
            Directory.CreateDirectory(versionsDirectory);
            Directory.CreateDirectory(librariesDirectory);
            Directory.CreateDirectory(assetsDirectory);
        }

        private void LoadInstallationsConfig()
        {
            try
            {
                if (File.Exists(installationsConfigPath))
                {
                    string json = File.ReadAllText(installationsConfigPath);
                    installationsConfig = JsonSerializer.Deserialize<InstallationsConfig>(json) ?? new InstallationsConfig();
                }
                else
                {
                    installationsConfig = new InstallationsConfig();
                    CreateDefaultInstallations();
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to load installations config: {ex.Message}");
                installationsConfig = new InstallationsConfig();
                CreateDefaultInstallations();
            }
        }

        private void CreateDefaultInstallations()
        {
            // Create a default "Latest Release" installation
            var defaultInstallation = new Installation
            {
                Name = "Latest Release",
                Version = "1.21.1", // This would be dynamically determined
                GameDirectory = minecraftDirectory,
                Icon = "grass_block"
            };
            
            installationsConfig.Installations.Add(defaultInstallation);
            installationsConfig.SelectedInstallationId = defaultInstallation.Id;
            SaveInstallationsConfig();
        }

        private void SaveInstallationsConfig()
        {
            try
            {
                string json = JsonSerializer.Serialize(installationsConfig, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(installationsConfigPath, json);
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to save installations config: {ex.Message}");
            }
        }

        private void RefreshInstallations()
        {
            InstallationsPanel.ItemsSource = null;
            
            // Check which installations are actually installed
            foreach (var installation in installationsConfig.Installations)
            {
                CheckInstallationStatus(installation);
            }
            
            InstallationsPanel.ItemsSource = installationsConfig.Installations;
        }

        private void CheckInstallationStatus(Installation installation)
        {
            string versionsDir = Path.Combine(installation.GameDirectory, "versions");
            string versionDir = Path.Combine(versionsDir, installation.Version);
            string versionJson = Path.Combine(versionDir, $"{installation.Version}.json");
            string versionJar = Path.Combine(versionDir, $"{installation.Version}.jar");
            
            installation.IsInstalled = File.Exists(versionJson) && (File.Exists(versionJar) || installation.IsModded);
        }

        private async Task CheckJavaAndShowCompatibleVersions()
        {
            try
            {
                string javaVersion = await GetJavaVersionAsync();
                if (javaVersion != null)
                {
                    int currentJavaVersion = GetJavaMajorVersion(javaVersion);
                    
                    Dispatcher.Invoke(() =>
                    {
                        JavaVersionText.Text = $"Java: {currentJavaVersion} ({javaVersion})";
                        if (currentJavaVersion < 17)
                        {
                            JavaVersionText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Orange);
                            DownloadJavaButton.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            JavaVersionText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.LightGreen);
                        }
                    });
                    
                    LogMessage($"Detected Java {currentJavaVersion} ({javaVersion})");
                }
                else
                {
                    LogMessage("Java not detected. Please install Java to run Minecraft.");
                    Dispatcher.Invoke(() =>
                    {
                        JavaVersionText.Text = "Java: Not Found";
                        JavaVersionText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                        DownloadJavaButton.Visibility = Visibility.Visible;
                    });
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to check Java compatibility: {ex.Message}");
            }
        }

        private async Task<string> GetJavaVersionAsync(string javaPath = "java")
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = javaPath,
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    string output = await process.StandardError.ReadToEndAsync();
                    string stdOutput = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode == 0)
                    {
                        string fullOutput = output + stdOutput;
                        var lines = fullOutput.Split('\n');
                        foreach (var line in lines)
                        {
                            if (line.Contains("version"))
                            {
                                var match = Regex.Match(line, @"version ""([^""]+)""");
                                if (match.Success)
                                {
                                    return match.Groups[1].Value;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to detect Java version for {javaPath}: {ex.Message}");
            }
            return null;
        }

        private int GetJavaMajorVersion(string versionString)
        {
            if (string.IsNullOrEmpty(versionString)) return 0;
            
            if (versionString.StartsWith("1."))
            {
                var parts = versionString.Split('.');
                if (parts.Length >= 2 && int.TryParse(parts[1], out int version))
                {
                    return version;
                }
            }
            else
            {
                var parts = versionString.Split('.');
                if (parts.Length >= 1 && int.TryParse(parts[0], out int version))
                {
                    return version;
                }
            }
            return 0;
        }

        private int GetRequiredJavaVersion(string minecraftVersion)
        {
            // Extract base Minecraft version from modded versions
            string baseVersion = minecraftVersion;
            
            // Handle modded versions like "fabric-loader-0.16.9-1.21.1" or "forge-1.20.1-47.2.0"
            if (minecraftVersion.Contains("fabric-loader") || minecraftVersion.Contains("forge") || minecraftVersion.Contains("quilt"))
            {
                // Extract the Minecraft version part (look for pattern like -1.21.1 or -1.20.1)
                var match = Regex.Match(minecraftVersion, @"-(\d+\.\d+(?:\.\d+)?)(?:-|$)");
                if (match.Success)
                {
                    baseVersion = match.Groups[1].Value;
                    LogMessage($"Extracted Minecraft version '{baseVersion}' from '{minecraftVersion}'");
                }
                else
                {
                    // Fallback: try to find any version that looks like Minecraft (1.x.x)
                    match = Regex.Match(minecraftVersion, @"(1\.\d+(?:\.\d+)?)");
                    if (match.Success)
                    {
                        baseVersion = match.Groups[1].Value;
                        LogMessage($"Fallback extracted Minecraft version '{baseVersion}' from '{minecraftVersion}'");
                    }
                }
            }
            
            // Map Minecraft versions to required Java versions
            var versionParts = baseVersion.Split('.');
            if (versionParts.Length >= 2)
            {
                if (int.TryParse(versionParts[1], out int minorVersion))
                {
                    // Minecraft version mapping to Java requirements
                    if (minorVersion >= 21) return 21; // MC 1.21+ requires Java 21+
                    if (minorVersion >= 20) return 17; // MC 1.20+ requires Java 17+
                    if (minorVersion >= 18) return 17; // MC 1.18+ requires Java 17+
                    if (minorVersion >= 17) return 16; // MC 1.17 requires Java 16+
                    if (minorVersion >= 12) return 8;  // MC 1.12+ requires Java 8+
                }
            }
            
            return 8; // Default to Java 8 for older versions
        }

        private async Task<string> FindBestJavaExecutableAsync(Installation installation, int requiredVersion = 8)
        {
            // Check if installation has custom Java path
            if (!string.IsNullOrEmpty(installation.JavaPath) && installation.JavaPath != "Use system default")
            {
                if (File.Exists(installation.JavaPath))
                {
                    string customVersion = await GetJavaVersionAsync(installation.JavaPath);
                    if (customVersion != null)
                    {
                        int customJavaVersion = GetJavaMajorVersion(customVersion);
                        if (customJavaVersion >= requiredVersion)
                        {
                            LogMessage($"Using custom Java from installation: {installation.JavaPath}");
                            return installation.JavaPath;
                        }
                        else
                        {
                            LogMessage($"Custom Java {customJavaVersion} is too old, need {requiredVersion}+");
                        }
                    }
                }
            }

            // Try to find the best Java executable for the required version
            var javaExecutables = new List<string>();
            
            // Check custom Java path first
            string customJava = Environment.GetEnvironmentVariable("CUSTOM_JAVA_PATH");
            if (!string.IsNullOrEmpty(customJava) && File.Exists(customJava))
            {
                javaExecutables.Add(customJava);
            }
            
            // Check JAVA_HOME
            string javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(javaHome))
            {
                var javaExe = Path.Combine(javaHome, "bin", "java.exe");
                if (File.Exists(javaExe))
                {
                    javaExecutables.Add(javaExe);
                }
                javaExe = Path.Combine(javaHome, "bin", "java");
                if (File.Exists(javaExe))
                {
                    javaExecutables.Add(javaExe);
                }
            }

            // Add PATH java
            javaExecutables.Add("java");

            // Check common installation directories
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            
            foreach (var baseDir in new[] { programFiles, programFilesX86 })
            {
                try
                {
                    // Check Java directory
                    var javaDir = Path.Combine(baseDir, "Java");
                    if (Directory.Exists(javaDir))
                    {
                        var javaDirs = Directory.GetDirectories(javaDir)
                            .Where(d => Path.GetFileName(d).StartsWith("jdk") || Path.GetFileName(d).StartsWith("jre"))
                            .OrderByDescending(d => d);

                        foreach (var dir in javaDirs)
                        {
                            var javaExe = Path.Combine(dir, "bin", "java.exe");
                            if (File.Exists(javaExe))
                            {
                                javaExecutables.Add(javaExe);
                            }
                        }
                    }

                    // Check Eclipse Adoptium directory
                    var adoptiumDir = Path.Combine(baseDir, "Eclipse Adoptium");
                    if (Directory.Exists(adoptiumDir))
                    {
                        var adoptiumDirs = Directory.GetDirectories(adoptiumDir)
                            .OrderByDescending(d => d);

                        foreach (var dir in adoptiumDirs)
                        {
                            var javaExe = Path.Combine(dir, "bin", "java.exe");
                            if (File.Exists(javaExe))
                            {
                                javaExecutables.Add(javaExe);
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore directory access errors
                }
            }

            // Test each executable and find the best match
            string bestExecutable = null;
            int bestVersion = 0;

            foreach (var executable in javaExecutables.Distinct())
            {
                try
                {
                    string version = await GetJavaVersionAsync(executable);
                    if (version != null)
                    {
                        int majorVersion = GetJavaMajorVersion(version);
                        
                        if (majorVersion >= requiredVersion)
                        {
                            if (bestExecutable == null || majorVersion == requiredVersion || 
                                (bestVersion < requiredVersion && majorVersion > bestVersion) ||
                                (bestVersion > requiredVersion && majorVersion < bestVersion && majorVersion >= requiredVersion))
                            {
                                bestExecutable = executable;
                                bestVersion = majorVersion;
                            }
                        }
                    }
                }
                catch
                {
                    // Continue with next executable
                }
            }

            return bestExecutable;
        }

        private async Task BuildClasspath(Installation installation, VersionInfo versionInfo, List<string> classpathEntries)
        {
            // For modded versions, we need to include parent version libraries
            var allLibraries = new List<Library>();
            
            // Add current version libraries
            if (versionInfo.Libraries != null)
            {
                allLibraries.AddRange(versionInfo.Libraries);
            }

            // If this version inherits from another, add parent libraries
            if (!string.IsNullOrEmpty(versionInfo.InheritsFrom))
            {
                string parentJsonPath = Path.Combine(installation.GameDirectory, "versions", versionInfo.InheritsFrom, $"{versionInfo.InheritsFrom}.json");
                if (File.Exists(parentJsonPath))
                {
                    try
                    {
                        string parentJson = await File.ReadAllTextAsync(parentJsonPath);
                        var parentInfo = JsonSerializer.Deserialize<VersionInfo>(parentJson);
                        if (parentInfo.Libraries != null)
                        {
                            allLibraries.AddRange(parentInfo.Libraries);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Failed to load parent version libraries: {ex.Message}");
                    }
                }
            }

            // Add all libraries to classpath
            string libDir = Path.Combine(installation.GameDirectory, "libraries");
            foreach (var library in allLibraries)
            {
                if (library.Downloads?.Artifact != null && !string.IsNullOrEmpty(library.Downloads.Artifact.Path))
                {
                    try
                    {
                        string libraryPath = Path.Combine(libDir, library.Downloads.Artifact.Path);
                        if (File.Exists(libraryPath))
                        {
                            classpathEntries.Add(libraryPath);
                        }
                        else
                        {
                            LogMessage($"Missing library: {libraryPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Error processing library {library.Name}: {ex.Message}");
                    }
                }
            }

            // Add main jar (try current version first, then parent if inheriting)
            string versionDir = Path.Combine(installation.GameDirectory, "versions", installation.Version);
            string mainJarPath = Path.Combine(versionDir, $"{installation.Version}.jar");
            if (File.Exists(mainJarPath))
            {
                classpathEntries.Add(mainJarPath);
            }
            else if (!string.IsNullOrEmpty(versionInfo.InheritsFrom))
            {
                // For modded versions, use the parent jar
                string parentJarPath = Path.Combine(installation.GameDirectory, "versions", versionInfo.InheritsFrom, $"{versionInfo.InheritsFrom}.jar");
                if (File.Exists(parentJarPath))
                {
                    classpathEntries.Add(parentJarPath);
                    LogMessage($"Using parent jar: {parentJarPath}");
                }
            }
        }

        private async Task<string> GetMainClass(Installation installation, VersionInfo versionInfo)
        {
            // Get main class (use parent's if current doesn't have one)
            string mainClass = versionInfo.MainClass;
            if (string.IsNullOrEmpty(mainClass) && !string.IsNullOrEmpty(versionInfo.InheritsFrom))
            {
                string parentJsonPath = Path.Combine(installation.GameDirectory, "versions", versionInfo.InheritsFrom, $"{versionInfo.InheritsFrom}.json");
                if (File.Exists(parentJsonPath))
                {
                    try
                    {
                        string parentJson = await File.ReadAllTextAsync(parentJsonPath);
                        var parentInfo = JsonSerializer.Deserialize<VersionInfo>(parentJson);
                        mainClass = parentInfo.MainClass;
                        LogMessage($"Using parent main class: {mainClass}");
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Failed to get parent main class: {ex.Message}");
                    }
                }
            }
            
            return mainClass;
        }

        private async Task<bool> DownloadVersion(Installation installation)
        {
            try
            {
                LogMessage($"Downloading Minecraft {installation.Version}...");
                
                // Show progress
                Dispatcher.Invoke(() =>
                {
                    ProgressBar.Visibility = Visibility.Visible;
                    ProgressBar.Value = 0;
                });

                // Load available versions if not already loaded
                if (availableVersions == null)
                {
                    await LoadAvailableVersionsAsync();
                }

                // Find the version to download
                var versionToDownload = availableVersions?.FirstOrDefault(v => v.Id == installation.Version);
                if (versionToDownload == null)
                {
                    LogMessage($"Version {installation.Version} not found in available versions");
                    MessageBox.Show($"Version {installation.Version} not found in Mojang's version manifest.",
                        "Version Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                string versionDir = Path.Combine(installation.GameDirectory, "versions", installation.Version);
                Directory.CreateDirectory(versionDir);

                // Download version JSON
                string versionJsonPath = Path.Combine(versionDir, $"{installation.Version}.json");
                string versionJson = await httpClient.GetStringAsync(versionToDownload.Url);
                await File.WriteAllTextAsync(versionJsonPath, versionJson);

                var versionInfo = JsonSerializer.Deserialize<VersionInfo>(versionJson);
                Dispatcher.Invoke(() => ProgressBar.Value = 10);

                // Handle parent version for modded versions
                if (!string.IsNullOrEmpty(versionInfo.InheritsFrom))
                {
                    LogMessage($"This version inherits from {versionInfo.InheritsFrom}");
                    
                    string parentVersionDir = Path.Combine(installation.GameDirectory, "versions", versionInfo.InheritsFrom);
                    if (!Directory.Exists(parentVersionDir))
                    {
                        LogMessage($"Parent version {versionInfo.InheritsFrom} not found. Attempting to download...");
                        
                        var parentVersion = availableVersions?.FirstOrDefault(v => v.Id == versionInfo.InheritsFrom);
                        if (parentVersion != null)
                        {
                            // Create temporary installation for parent version
                            var parentInstallation = new Installation
                            {
                                Version = versionInfo.InheritsFrom,
                                GameDirectory = installation.GameDirectory
                            };
                            
                            bool parentDownloaded = await DownloadVersion(parentInstallation);
                            if (!parentDownloaded)
                            {
                                LogMessage($"Failed to download parent version {versionInfo.InheritsFrom}");
                                return false;
                            }
                        }
                        else
                        {
                            LogMessage($"Parent version {versionInfo.InheritsFrom} not found in available versions list");
                            return false;
                        }
                    }
                }

                // Download client jar if available
                if (versionInfo.Downloads?.Client != null)
                {
                    string jarPath = Path.Combine(versionDir, $"{installation.Version}.jar");
                    await DownloadFileAsync(versionInfo.Downloads.Client.Url, jarPath);
                    LogMessage($"Downloaded client jar: {installation.Version}.jar");
                    Dispatcher.Invoke(() => ProgressBar.Value = 40);
                }
                else
                {
                    LogMessage("No client download found - this version uses the parent version's jar");
                    Dispatcher.Invoke(() => ProgressBar.Value = 40);
                }

                // Download libraries
                if (versionInfo.Libraries != null)
                {
                    LogMessage($"Downloading {versionInfo.Libraries.Count} libraries...");
                    await DownloadLibrariesAsync(versionInfo.Libraries, installation.GameDirectory);
                    Dispatcher.Invoke(() => ProgressBar.Value = 70);

                    await ExtractNativesAsync(versionInfo.Libraries, versionDir, installation.GameDirectory);
                    Dispatcher.Invoke(() => ProgressBar.Value = 85);
                }

                // Download assets
                var assetIndex = versionInfo.AssetIndex;
                if (assetIndex == null && !string.IsNullOrEmpty(versionInfo.InheritsFrom))
                {
                    string parentJsonPath = Path.Combine(installation.GameDirectory, "versions", versionInfo.InheritsFrom, $"{versionInfo.InheritsFrom}.json");
                    if (File.Exists(parentJsonPath))
                    {
                        string parentJson = await File.ReadAllTextAsync(parentJsonPath);
                        var parentInfo = JsonSerializer.Deserialize<VersionInfo>(parentJson);
                        assetIndex = parentInfo.AssetIndex;
                    }
                }

                if (assetIndex != null)
                {
                    await DownloadAssetsAsync(assetIndex, installation.GameDirectory);
                }

                Dispatcher.Invoke(() => ProgressBar.Value = 100);
                LogMessage($"Successfully downloaded Minecraft {installation.Version}");
                
                // Hide progress bar after a short delay
                _ = Task.Delay(2000).ContinueWith(_ => 
                    Dispatcher.Invoke(() => ProgressBar.Visibility = Visibility.Collapsed));
                
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to download {installation.Version}: {ex.Message}");
                MessageBox.Show($"Failed to download {installation.Version}: {ex.Message}",
                    "Download Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            finally
            {
                Dispatcher.Invoke(() => ProgressBar.Visibility = Visibility.Collapsed);
            }
        }

        private async Task LoadAvailableVersionsAsync()
        {
            try
            {
                LogMessage("Loading available versions from Mojang...");
                string manifestUrl = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
                string manifestJson = await httpClient.GetStringAsync(manifestUrl);

                var manifest = JsonSerializer.Deserialize<VersionManifest>(manifestJson);
                availableVersions = manifest.Versions ?? new List<MinecraftVersion>();

                LogMessage($"Loaded {availableVersions.Count} available versions");
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to load available versions: {ex.Message}");
            }
        }

        private async Task DownloadFileAsync(string url, string destinationPath)
        {
            try
            {
                using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));

                using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
                using var contentStream = await response.Content.ReadAsStreamAsync();
                await contentStream.CopyToAsync(fileStream);

                LogMessage($"Downloaded: {Path.GetFileName(destinationPath)}");
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to download file from {url}: {ex.Message}");
                throw;
            }
        }

        private async Task DownloadLibrariesAsync(List<Library> libraries, string gameDirectory)
        {
            try
            {
                string libDir = Path.Combine(gameDirectory, "libraries");
                foreach (var library in libraries)
                {
                    if (library.Downloads?.Artifact != null && !string.IsNullOrEmpty(library.Downloads.Artifact.Path))
                    {
                        string libraryPath = Path.Combine(libDir, library.Downloads.Artifact.Path);
                        if (!File.Exists(libraryPath))
                        {
                            await DownloadFileAsync(library.Downloads.Artifact.Url, libraryPath);
                        }
                    }
                }
                LogMessage("Libraries downloaded successfully.");
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to download libraries: {ex.Message}");
                throw;
            }
        }

        private async Task ExtractNativesAsync(List<Library> libraries, string versionDir, string gameDirectory)
        {
            try
            {
                string nativesDir = Path.Combine(versionDir, "natives");
                Directory.CreateDirectory(nativesDir);
                string libDir = Path.Combine(gameDirectory, "libraries");

                foreach (var library in libraries)
                {
                    if (library.Downloads?.Artifact != null && !string.IsNullOrEmpty(library.Downloads.Artifact.Path) && library.Name.Contains("natives"))
                    {
                        string nativePath = Path.Combine(libDir, library.Downloads.Artifact.Path);
                        if (File.Exists(nativePath))
                        {
                            using (var archive = System.IO.Compression.ZipFile.OpenRead(nativePath))
                            {
                                foreach (var entry in archive.Entries)
                                {
                                    if (!string.IsNullOrEmpty(entry.Name)) // Skip directories
                                    {
                                        string destinationPath = Path.Combine(nativesDir, entry.FullName);
                                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                                        using (var entryStream = entry.Open())
                                        using (var fileStream = File.Create(destinationPath))
                                        {
                                            await entryStream.CopyToAsync(fileStream);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                LogMessage("Natives extracted successfully.");
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to extract natives: {ex.Message}");
            }
            

        }

        private async Task DownloadAssetsAsync(AssetIndex assetIndex, string gameDirectory)
        {
            try
            {
                LogMessage($"Downloading assets for {assetIndex.Id}...");
                string assetsDir = Path.Combine(gameDirectory, "assets");
                string indexPath = Path.Combine(assetsDir, "indexes", $"{assetIndex.Id}.json");
                Directory.CreateDirectory(Path.GetDirectoryName(indexPath));

                string indexJson = await httpClient.GetStringAsync(assetIndex.Url);
                await File.WriteAllTextAsync(indexPath, indexJson);

                var assetManifest = JsonSerializer.Deserialize<AssetManifest>(indexJson);
                if (assetManifest?.Objects != null)
                {
                    string objectsDir = Path.Combine(assetsDir, "objects");
                    Directory.CreateDirectory(objectsDir);

                    int downloadedCount = 0;
                    foreach (var asset in assetManifest.Objects.Take(100)) // Limit for demo
                    {
                        string hash = asset.Value.Hash;
                        string subDir = Path.Combine(objectsDir, hash.Substring(0, 2));
                        Directory.CreateDirectory(subDir);

                        string assetPath = Path.Combine(subDir, hash);
                        if (!File.Exists(assetPath))
                        {
                            string assetUrl = $"https://resources.download.minecraft.net/{hash.Substring(0, 2)}/{hash}";
                            await DownloadFileAsync(assetUrl, assetPath);
                            downloadedCount++;
                        }
                    }
                    
                    LogMessage($"Downloaded {downloadedCount} assets for {assetIndex.Id}");
                }

                LogMessage($"Assets for {assetIndex.Id} downloaded successfully.");
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to download assets for {assetIndex.Id}: {ex.Message}");
                throw;
            }
        }

        private void LogMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                LogTextBox.ScrollToEnd();
            });
            Debug.WriteLine(message);
        }

        // Event Handlers
        private void NewInstallationButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InstallationDialog();
            if (dialog.ShowDialog() == true)
            {
                installationsConfig.Installations.Add(dialog.Result);
                SaveInstallationsConfig();
                RefreshInstallations();
                LogMessage($"Created new installation: {dialog.Result.Name}");
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshInstallations();
            _ = CheckJavaAndShowCompatibleVersions();
            LogMessage("Refreshed installations");
        }

        private async void PlayInstallation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Installation installation)
            {
                await LaunchInstallation(installation);
            }
        }

        private void EditInstallation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Installation installation)
            {
                var dialog = new InstallationDialog(installation);
                if (dialog.ShowDialog() == true)
                {
                    var index = installationsConfig.Installations.IndexOf(installation);
                    if (index >= 0)
                    {
                        installationsConfig.Installations[index] = dialog.Result;
                        SaveInstallationsConfig();
                        RefreshInstallations();
                        LogMessage($"Updated installation: {dialog.Result.Name}");
                    }
                }
            }
        }

        private void DeleteInstallation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Installation installation)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete the installation '{installation.Name}'?\n\nThis will not delete the game files, only remove it from the launcher.",
                    "Delete Installation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    installationsConfig.Installations.Remove(installation);
                    SaveInstallationsConfig();
                    RefreshInstallations();
                    LogMessage($"Deleted installation: {installation.Name}");
                }
            }
        }

        private async Task LaunchInstallation(Installation installation)
        {
            try
            {
                LogMessage($"Launching {installation.Name} ({installation.Version})...");
                
                if (!installation.IsInstalled)
                {
                    LogMessage($"Installation {installation.Name} is not installed. Attempting to download...");
                    
                    var result = MessageBox.Show($"Installation '{installation.Name}' is not installed.\n\nWould you like to download it now?",
                        "Installation Not Found", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        bool downloadSuccess = await DownloadVersion(installation);
                        if (!downloadSuccess)
                        {
                            LogMessage("Download failed. Cannot launch.");
                            return;
                        }
                        
                        // Refresh installation status
                        CheckInstallationStatus(installation);
                        RefreshInstallations();
                    }
                    else
                    {
                        return;
                    }
                }

                // Update last played time
                installation.LastPlayed = DateTime.Now;
                SaveInstallationsConfig();

                // Launch the game (simplified version)
                await LaunchMinecraft(installation);
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to launch {installation.Name}: {ex.Message}");
                MessageBox.Show($"Failed to launch {installation.Name}: {ex.Message}",
                    "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LaunchMinecraft(Installation installation)
        {
            try
            {
                LogMessage($"Starting Minecraft {installation.Version} for user {username}...");
                
                // Check Java version compatibility
                int requiredJavaVersion = GetRequiredJavaVersion(installation.Version);
                string javaExecutable = await FindBestJavaExecutableAsync(installation, requiredJavaVersion);
                
                if (javaExecutable == null)
                {
                    LogMessage("Java not found! Please install Java and make sure it's in your PATH.");
                    MessageBox.Show("Java not found! Please install Java and make sure it's in your PATH.", 
                        "Java Required", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string javaVersion = await GetJavaVersionAsync(javaExecutable);
                int currentJavaVersion = GetJavaMajorVersion(javaVersion);
                
                LogMessage($"Using Java executable: {javaExecutable}");
                LogMessage($"Detected Java version: {javaVersion} (Java {currentJavaVersion})");
                LogMessage($"Required Java version for Minecraft {installation.Version}: Java {requiredJavaVersion}+");

                if (currentJavaVersion < requiredJavaVersion)
                {
                    string message = $"Minecraft {installation.Version} requires Java {requiredJavaVersion} or higher, but you have Java {currentJavaVersion}.\n\n" +
                                   $"Please install Java {requiredJavaVersion}+ from: https://adoptium.net/";
                    
                    LogMessage($"Java version incompatible! Need Java {requiredJavaVersion}+, have Java {currentJavaVersion}");
                    MessageBox.Show(message, "Java Version Incompatible", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Load version info
                string versionDir = Path.Combine(installation.GameDirectory, "versions", installation.Version);
                string versionJsonPath = Path.Combine(versionDir, $"{installation.Version}.json");

                if (!File.Exists(versionJsonPath))
                {
                    LogMessage($"Version {installation.Version} is not installed. Please download it first.");
                    MessageBox.Show($"Version {installation.Version} is not installed.\n\nPlease download it first using the installation manager.", 
                        "Version Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string versionJson = await File.ReadAllTextAsync(versionJsonPath);
                var versionInfo = JsonSerializer.Deserialize<VersionInfo>(versionJson);

                // Check if this is a modded version that needs a parent version
                if (!string.IsNullOrEmpty(versionInfo.InheritsFrom))
                {
                    string parentVersionDir = Path.Combine(installation.GameDirectory, "versions", versionInfo.InheritsFrom);
                    string parentJsonPath = Path.Combine(parentVersionDir, $"{versionInfo.InheritsFrom}.json");
                    
                    if (!File.Exists(parentJsonPath))
                    {
                        LogMessage($"This modded version requires Minecraft {versionInfo.InheritsFrom} to be installed first.");
                        MessageBox.Show($"This modded version requires Minecraft {versionInfo.InheritsFrom} to be installed first.\n\nPlease download Minecraft {versionInfo.InheritsFrom} before launching this version.", 
                            "Parent Version Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    else
                    {
                        LogMessage($"Found parent version: {versionInfo.InheritsFrom}");
                    }
                }

                // Build classpath
                var classpathEntries = new List<string>();
                await BuildClasspath(installation, versionInfo, classpathEntries);

                string classpath = string.Join(";", classpathEntries);
                LogMessage($"Classpath contains {classpathEntries.Count} entries");
                
                if (classpathEntries.Count == 0)
                {
                    LogMessage("Warning: Empty classpath! This will likely cause launch failure.");
                    MessageBox.Show("No libraries found for this version. The installation may be incomplete.", 
                        "Incomplete Installation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Get main class
                string mainClass = await GetMainClass(installation, versionInfo);
                if (string.IsNullOrEmpty(mainClass))
                {
                    LogMessage("Error: No main class found!");
                    MessageBox.Show("No main class found for this version. The version may be corrupted.", 
                        "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Build launch arguments
                var args = new List<string>();
                
                // Parse Java arguments
                var javaArgs = installation.JavaArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                args.AddRange(javaArgs);
                
                // Add library path for natives
                args.Add($"-Djava.library.path={Path.Combine(versionDir, "natives")}");
                args.Add("-cp");
                args.Add($"\"{classpath}\"");
                args.Add(mainClass);

                // Game arguments
                args.Add("--username");
                args.Add(username);
                args.Add("--version");
                args.Add(installation.Version);
                args.Add("--gameDir");
                args.Add(installation.GameDirectory);
                args.Add("--assetsDir");
                args.Add(Path.Combine(installation.GameDirectory, "assets"));
                args.Add("--assetIndex");
                args.Add(versionInfo.AssetIndex?.Id ?? "legacy");
                args.Add("--uuid");
                args.Add(Guid.NewGuid().ToString().Replace("-", ""));
                args.Add("--accessToken");
                args.Add("null");
                args.Add("--userType");
                args.Add("legacy");

                // Launch process
                var startInfo = new ProcessStartInfo
                {
                    FileName = javaExecutable,
                    Arguments = string.Join(" ", args),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = installation.GameDirectory
                };

                LogMessage($"Launching with command: {Path.GetFileName(javaExecutable)} {startInfo.Arguments}");

                var process = Process.Start(startInfo);
                if (process != null)
                {
                    LogMessage("Minecraft launched successfully!");
                    
                    // Track play time
                    var playTimeStart = DateTime.Now;
                    
                    // Read output asynchronously
                    process.OutputDataReceived += (s, e) => {
                        if (!string.IsNullOrEmpty(e.Data))
                            Dispatcher.Invoke(() => LogMessage($"[MC] {e.Data}"));
                    };
                    process.ErrorDataReceived += (s, e) => {
                        if (!string.IsNullOrEmpty(e.Data))
                            Dispatcher.Invoke(() => LogMessage($"[MC ERROR] {e.Data}"));
                    };
                    
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    
                    // Wait for process to exit and update play time
                    _ = Task.Run(async () =>
                    {
                        await process.WaitForExitAsync();
                        var playTime = (int)(DateTime.Now - playTimeStart).TotalMinutes;
                        installation.PlayTime += playTime;
                        SaveInstallationsConfig();
                        Dispatcher.Invoke(() => LogMessage($"Minecraft closed. Session time: {playTime} minutes"));
                    });
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to launch Minecraft: {ex.Message}");
                MessageBox.Show($"Failed to launch Minecraft: {ex.Message}",
                    "Launch Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DownloadJavaButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://adoptium.net/temurin/releases/",
                    UseShellExecute = true
                });
                LogMessage("Opened Java download page.");
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to open Java download page: {ex.Message}");
            }
        }

        private async void SelectJavaButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Select Java Executable",
                    Filter = "Java Executable (java.exe)|java.exe|All Files (*.*)|*.*",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    string selectedJava = openFileDialog.FileName;
                    LogMessage($"Selected Java: {selectedJava}");
                    
                    // Test the selected Java
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = selectedJava,
                        Arguments = "-version",
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(startInfo);
                    if (process != null)
                    {
                        string output = await process.StandardError.ReadToEndAsync();
                        await process.WaitForExitAsync();
                        
                        if (process.ExitCode == 0)
                        {
                            LogMessage($"Java executable is valid: {selectedJava}");
                            MessageBox.Show($"Java executable selected successfully!\nPath: {selectedJava}",
                                "Java Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            LogMessage($"Invalid Java executable: {selectedJava}");
                            MessageBox.Show("The selected file is not a valid Java executable.",
                                "Invalid Java", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to select Java: {ex.Message}");
            }
        }
    }
}