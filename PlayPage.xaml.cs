using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

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
            

            foreach (var installation in installationsConfig.Installations)
            {
                CheckInstallationStatus(installation);
            }
            
            InstallationsPanel.ItemsSource = installationsConfig.Installations;
        }

        private void CheckInstallationStatus(Installation installation)
        {
            string versionsDir = Path.Combine(installation.GameDirectory, "versions");
            
            if (installation.IsModded)
            {

                string moddedVersionDir = Path.Combine(versionsDir, installation.Version);
                string moddedVersionJson = Path.Combine(moddedVersionDir, $"{installation.Version}.json");
                
                string baseVersionDir = Path.Combine(versionsDir, installation.BaseVersion);
                string baseVersionJson = Path.Combine(baseVersionDir, $"{installation.BaseVersion}.json");
                string baseVersionJar = Path.Combine(baseVersionDir, $"{installation.BaseVersion}.jar");
                
                installation.IsInstalled = File.Exists(moddedVersionJson) && 
                                          File.Exists(baseVersionJson) && 
                                          File.Exists(baseVersionJar);
            }
            else
            {

                string versionDir = Path.Combine(versionsDir, installation.Version);
                string versionJson = Path.Combine(versionDir, $"{installation.Version}.json");
                string versionJar = Path.Combine(versionDir, $"{installation.Version}.jar");
                
                installation.IsInstalled = File.Exists(versionJson) && File.Exists(versionJar);
            }
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
            string baseVersion = minecraftVersion;
            
            if (minecraftVersion.Contains("fabric-loader") || minecraftVersion.Contains("forge") || minecraftVersion.Contains("quilt"))
            {
                var parts = minecraftVersion.Split('-');
                if (parts.Length >= 4)
                {
                    baseVersion = parts[parts.Length - 1]; // Get the last part
                    LogMessage($"Extracted Minecraft version '{baseVersion}' from '{minecraftVersion}'");
                }
                else
                {
                    var match = Regex.Match(minecraftVersion, @"(1\.\d+(?:\.\d+)?)");
                    if (match.Success)
                    {
                        baseVersion = match.Groups[1].Value;
                        LogMessage($"Fallback extracted Minecraft version '{baseVersion}' from '{minecraftVersion}'");
                    }
                }
            }
            
            var versionParts = baseVersion.Split('.');
            if (versionParts.Length >= 2)
            {
                if (int.TryParse(versionParts[1], out int minorVersion))
                {
                    if (minorVersion >= 21) return 21;
                    if (minorVersion >= 20) return 17;
                    if (minorVersion >= 18) return 17;
                    if (minorVersion >= 17) return 16;
                    if (minorVersion >= 12) return 8;
                }
            }
            
            return 8;
        }

        private async Task<string> FindBestJavaExecutableAsync(Installation installation, int requiredVersion = 8)
        {
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

            var javaExecutables = new List<string>();
            
            string customJava = Environment.GetEnvironmentVariable("CUSTOM_JAVA_PATH");
            if (!string.IsNullOrEmpty(customJava) && File.Exists(customJava))
            {
                javaExecutables.Add(customJava);
            }
            
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

            javaExecutables.Add("java");
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            
            foreach (var baseDir in new[] { programFiles, programFilesX86 })
            {
                try
                {
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
                }
            }

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
                }
            }

            return bestExecutable;
        }

        private async Task BuildClasspath(Installation installation, VersionInfo versionInfo, List<string> classpathEntries)
        {
            // For modded versions, we need to include parent version libraries
            var allLibraries = new List<Library>();
            
            // Add current version libraries (these are the mod loader libraries) - prioritize these
            if (versionInfo.Libraries != null)
            {
                LogMessage($"Adding {versionInfo.Libraries.Count} mod loader libraries");
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
                            LogMessage($"Adding {parentInfo.Libraries.Count} base game libraries");
                            allLibraries.AddRange(parentInfo.Libraries);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Failed to load parent version libraries: {ex.Message}");
                    }
                }
            }

            // Deduplicate libraries - prefer mod loader versions over base game versions
            allLibraries = DeduplicateLibraries(allLibraries);

            // Add all libraries to classpath
            string libDir = Path.Combine(installation.GameDirectory, "libraries");
            int addedLibraries = 0;
            int missingLibraries = 0;
            
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
                            addedLibraries++;
                        }
                        else
                        {
                            LogMessage($"Missing library: {library.Name} at {libraryPath}");
                            missingLibraries++;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Error processing library {library.Name}: {ex.Message}");
                    }
                }
                else
                {
                    // Try to construct download URL for Fabric libraries that don't have download artifacts
                    string constructedUrl = TryConstructFabricLibraryUrl(library.Name);
                    if (!string.IsNullOrEmpty(constructedUrl))
                    {
                        string libraryPath = Path.Combine(libDir, ConvertMavenToPath(library.Name));
                        try
                        {
                            if (!File.Exists(libraryPath))
                            {
                                await DownloadFileAsync(constructedUrl, libraryPath);
                                classpathEntries.Add(libraryPath);
                                addedLibraries++;
                                LogMessage($"Downloaded Fabric library: {library.Name}");
                            }
                            else
                            {
                                classpathEntries.Add(libraryPath);
                                addedLibraries++;
                            }
                        }
                        catch (Exception ex)
                        {
                            LogMessage($"Failed to download Fabric library {library.Name}: {ex.Message}");
                            missingLibraries++;
                        }
                    }
                    else
                    {
                        LogMessage($"Library {library.Name} has no download artifact and couldn't construct URL");
                    }
                }
            }
            
            LogMessage($"Added {addedLibraries} libraries to classpath, {missingLibraries} missing");

            // Add main jar (try current version first, then parent if inheriting)
            string versionDir = Path.Combine(installation.GameDirectory, "versions", installation.Version);
            string mainJarPath = Path.Combine(versionDir, $"{installation.Version}.jar");
            if (File.Exists(mainJarPath))
            {
                classpathEntries.Add(mainJarPath);
                LogMessage($"Using modded version jar: {mainJarPath}");
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
                else
                {
                    LogMessage($"Parent jar not found: {parentJarPath}");
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

        private async Task VerifyAndDownloadMissingLibraries(Installation installation, VersionInfo versionInfo)
        {
            try
            {
                var missingLibraries = new List<Library>();
                string libDir = Path.Combine(installation.GameDirectory, "libraries");

                // Check current version libraries
                if (versionInfo.Libraries != null)
                {
                    foreach (var library in versionInfo.Libraries)
                    {
                        if (library.Downloads?.Artifact != null && !string.IsNullOrEmpty(library.Downloads.Artifact.Path))
                        {
                            string libraryPath = Path.Combine(libDir, library.Downloads.Artifact.Path);
                            if (!File.Exists(libraryPath))
                            {
                                missingLibraries.Add(library);
                            }
                        }
                    }
                }

                // Check parent version libraries if applicable
                if (!string.IsNullOrEmpty(versionInfo.InheritsFrom))
                {
                    string parentJsonPath = Path.Combine(installation.GameDirectory, "versions", versionInfo.InheritsFrom, $"{versionInfo.InheritsFrom}.json");
                    if (File.Exists(parentJsonPath))
                    {
                        string parentJson = await File.ReadAllTextAsync(parentJsonPath);
                        var parentInfo = JsonSerializer.Deserialize<VersionInfo>(parentJson);
                        if (parentInfo.Libraries != null)
                        {
                            foreach (var library in parentInfo.Libraries)
                            {
                                if (library.Downloads?.Artifact != null && !string.IsNullOrEmpty(library.Downloads.Artifact.Path))
                                {
                                    string libraryPath = Path.Combine(libDir, library.Downloads.Artifact.Path);
                                    if (!File.Exists(libraryPath))
                                    {
                                        missingLibraries.Add(library);
                                    }
                                }
                            }
                        }
                    }
                }

                if (missingLibraries.Any())
                {
                    LogMessage($"Found {missingLibraries.Count} missing libraries, downloading...");
                    await DownloadLibrariesAsync(missingLibraries, installation.GameDirectory);
                }
                else
                {
                    LogMessage("All required libraries are present");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to verify libraries: {ex.Message}");
            }
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

                // Handle modded versions differently
                if (installation.IsModded)
                {
                    return await DownloadModdedVersion(installation);
                }

                // Find the vanilla version to download
                var versionToDownload = availableVersions?.FirstOrDefault(v => v.Id == installation.Version);
                if (versionToDownload == null)
                {
                    LogMessage($"Version {installation.Version} not found in available versions");
                    MessageBox.Show($"Version {installation.Version} not found in Mojang's version manifest.",
                        "Version Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                // Download the vanilla version
                bool downloadSuccess = await DownloadVanillaVersion(installation, versionToDownload);
                if (!downloadSuccess)
                {
                    return false;
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
                int downloadedCount = 0;
                int skippedCount = 0;
                int failedCount = 0;
                
                foreach (var library in libraries)
                {
                    if (library.Downloads?.Artifact != null && !string.IsNullOrEmpty(library.Downloads.Artifact.Path))
                    {
                        string libraryPath = Path.Combine(libDir, library.Downloads.Artifact.Path);
                        if (!File.Exists(libraryPath))
                        {
                            try
                            {
                                await DownloadFileAsync(library.Downloads.Artifact.Url, libraryPath);
                                downloadedCount++;
                            }
                            catch (Exception ex)
                            {
                                LogMessage($"Failed to download library {library.Name}: {ex.Message}");
                                failedCount++;
                            }
                        }
                        else
                        {
                            skippedCount++;
                        }
                    }
                    else
                    {
                        // Try to construct download URL for Fabric libraries
                        string constructedUrl = TryConstructFabricLibraryUrl(library.Name);
                        if (!string.IsNullOrEmpty(constructedUrl))
                        {
                            string libraryPath = Path.Combine(libDir, ConvertMavenToPath(library.Name));
                            try
                            {
                                if (!File.Exists(libraryPath))
                                {
                                    await DownloadFileAsync(constructedUrl, libraryPath);
                                    downloadedCount++;
                                    LogMessage($"Downloaded Fabric library: {library.Name}");
                                }
                                else
                                {
                                    skippedCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                LogMessage($"Failed to download Fabric library {library.Name}: {ex.Message}");
                                failedCount++;
                            }
                        }
                        else
                        {
                            LogMessage($"Library {library.Name} has no download information and couldn't construct URL");
                        }
                    }
                }
                
                LogMessage($"Libraries: {downloadedCount} downloaded, {skippedCount} already existed, {failedCount} failed");
                
                if (failedCount > 0)
                {
                    LogMessage($"Warning: {failedCount} libraries failed to download - this may cause launch issues");
                }
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

                // Track which DLLs we've already extracted to avoid conflicts
                var extractedDlls = new HashSet<string>();
                int extractedCount = 0;
                
                // First pass: Extract from modern classifier-based natives (preferred)
                foreach (var library in libraries)
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && library.Downloads?.Classifiers != null)
                    {
                        var classifiers = library.Downloads.Classifiers;
                        
                        // Try to find the best Windows native classifier for x64 systems
                        Artifact nativeDownload = null;
                        string classifierUsed = "none";
                        
                        // Priority order for 64-bit Windows: x64 specific, generic windows (but NOT x86)
                        if (classifiers.NativesWindowsX64 != null)
                        {
                            nativeDownload = classifiers.NativesWindowsX64;
                            classifierUsed = "natives-windows-x86_64";
                        }
                        else if (classifiers.NativesWindows != null)
                        {
                            nativeDownload = classifiers.NativesWindows;
                            classifierUsed = "natives-windows";
                        }
                        else if (classifiers.AdditionalClassifiers != null)
                        {
                            // Check for other 64-bit Windows classifier patterns, avoid 32-bit
                            foreach (var kvp in classifiers.AdditionalClassifiers)
                            {
                                string key = kvp.Key.ToLower();
                                if (key.Contains("windows") && !key.Contains("x86") && !key.Contains("32") && !key.Contains("arm"))
                                {
                                    try
                                    {
                                        var artifact = JsonSerializer.Deserialize<Artifact>(kvp.Value.GetRawText());
                                        if (artifact != null)
                                        {
                                            nativeDownload = artifact;
                                            classifierUsed = kvp.Key;
                                            break;
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                        
                        if (nativeDownload != null)
                        {
                            string nativePath = Path.Combine(libDir, nativeDownload.Path);
                            LogMessage($"Using classifier '{classifierUsed}' for {library.Name}");
                            
                            if (File.Exists(nativePath))
                            {
                                using (var archive = System.IO.Compression.ZipFile.OpenRead(nativePath))
                                {
                                    foreach (var entry in archive.Entries)
                                    {
                                        if (!string.IsNullOrEmpty(entry.Name) && entry.Name.EndsWith(".dll"))
                                        {
                                            string dllName = entry.Name.ToLower();
                                            
                                            // Skip if we've already extracted this DLL from a higher priority source
                                            if (extractedDlls.Contains(dllName))
                                            {
                                                LogMessage($"Skipping {entry.Name} - already extracted from higher priority source");
                                                continue;
                                            }
                                            
                                            string destinationPath = Path.Combine(nativesDir, entry.Name);
                                            using (var entryStream = entry.Open())
                                            using (var fileStream = File.Create(destinationPath))
                                            {
                                                await entryStream.CopyToAsync(fileStream);
                                            }
                                            extractedCount++;
                                            extractedDlls.Add(dllName);
                                            LogMessage($"Extracted native: {entry.Name} ({entry.Length} bytes) from {classifierUsed}");
                                        }
                                    }
                                }
                            }
                            else
                            {
                                LogMessage($"Native library file not found: {nativePath}");
                            }
                        }
                    }
                }

                // Second pass: Extract from old-style natives only if we haven't got the DLL yet
                foreach (var library in libraries)
                {
                    if (library.Downloads?.Artifact != null && !string.IsNullOrEmpty(library.Downloads.Artifact.Path) && 
                        library.Name.Contains("natives") && library.Name.Contains("windows") && !library.Name.Contains("x86"))
                    {
                        LogMessage($"Processing old-style native library: {library.Name}");
                        string nativePath = Path.Combine(libDir, library.Downloads.Artifact.Path);
                        
                        if (File.Exists(nativePath))
                        {
                            using (var archive = System.IO.Compression.ZipFile.OpenRead(nativePath))
                            {
                                foreach (var entry in archive.Entries)
                                {
                                    if (!string.IsNullOrEmpty(entry.Name) && entry.Name.EndsWith(".dll"))
                                    {
                                        string dllName = entry.Name.ToLower();
                                        
                                        // Only extract if we haven't already got this DLL
                                        if (!extractedDlls.Contains(dllName))
                                        {
                                            string destinationPath = Path.Combine(nativesDir, entry.Name);
                                            using (var entryStream = entry.Open())
                                            using (var fileStream = File.Create(destinationPath))
                                            {
                                                await entryStream.CopyToAsync(fileStream);
                                            }
                                            extractedCount++;
                                            extractedDlls.Add(dllName);
                                            LogMessage($"Extracted old-style native: {entry.Name} ({entry.Length} bytes)");
                                        }
                                        else
                                        {
                                            LogMessage($"Skipping {entry.Name} - already extracted");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                LogMessage($"Natives extracted successfully. {extractedCount} native files extracted.");
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to extract natives: {ex.Message}");
            }
        }

        private string GetOSName()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "windows";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "osx";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return "linux";
            else
                return "unknown";
        }

        private async Task DownloadAssetsAsync(AssetIndex assetIndex, string gameDirectory)
        {
            try
            {
                LogMessage($"Downloading assets for {assetIndex.Id}...");
                string assetsDir = Path.Combine(gameDirectory, "assets");
                string indexPath = Path.Combine(assetsDir, "indexes", $"{assetIndex.Id}.json");
                Directory.CreateDirectory(Path.GetDirectoryName(indexPath));

                // Download asset index if it doesn't exist
                if (!File.Exists(indexPath))
                {
                    LogMessage($"Downloading asset index from: {assetIndex.Url}");
                    string indexJson = await httpClient.GetStringAsync(assetIndex.Url);
                    await File.WriteAllTextAsync(indexPath, indexJson);
                    LogMessage($"Downloaded asset index: {assetIndex.Id}.json");
                }
                else
                {
                    LogMessage($"Asset index {assetIndex.Id}.json already exists");
                }

                // Read the asset index
                string existingIndexJson = await File.ReadAllTextAsync(indexPath);
                var assetManifest = JsonSerializer.Deserialize<AssetManifest>(existingIndexJson);
                
                if (assetManifest?.Objects != null)
                {
                    string objectsDir = Path.Combine(assetsDir, "objects");
                    Directory.CreateDirectory(objectsDir);

                    int downloadedCount = 0;
                    int skippedCount = 0;
                    int failedCount = 0;
                    int totalAssets = assetManifest.Objects.Count;
                    
                    LogMessage($"Processing {totalAssets} assets...");
                    
                    // Download all assets (not just first 100)
                    int processedCount = 0;
                    foreach (var asset in assetManifest.Objects)
                    {
                        processedCount++;
                        string hash = asset.Value.Hash;
                        string subDir = Path.Combine(objectsDir, hash.Substring(0, 2));
                        Directory.CreateDirectory(subDir);

                        string assetPath = Path.Combine(subDir, hash);
                        if (!File.Exists(assetPath))
                        {
                            try
                            {
                                string assetUrl = $"https://resources.download.minecraft.net/{hash.Substring(0, 2)}/{hash}";
                                await DownloadFileAsync(assetUrl, assetPath);
                                downloadedCount++;
                                
                                // Small delay to avoid overwhelming the server
                                if (downloadedCount % 10 == 0)
                                {
                                    await Task.Delay(100);
                                }
                                
                                if (downloadedCount % 100 == 0)
                                {
                                    LogMessage($"Downloaded {downloadedCount}/{totalAssets} assets... (processed {processedCount})");
                                }
                            }
                            catch (Exception ex)
                            {
                                LogMessage($"Failed to download asset {asset.Key} (hash: {hash}): {ex.Message}");
                                failedCount++;
                                
                                // Stop downloading if too many failures
                                if (failedCount > 50)
                                {
                                    LogMessage($"Too many asset download failures ({failedCount}), stopping asset download");
                                    break;
                                }
                            }
                        }
                        else
                        {
                            skippedCount++;
                        }
                        
                        // Log progress every 500 assets processed
                        if (processedCount % 500 == 0)
                        {
                            LogMessage($"Processed {processedCount}/{totalAssets} assets ({downloadedCount} downloaded, {skippedCount} skipped, {failedCount} failed)");
                        }
                    }
                    
                    LogMessage($"Assets complete: {downloadedCount} downloaded, {skippedCount} already existed, {failedCount} failed");
                    
                    if (failedCount > 0)
                    {
                        LogMessage($"Warning: {failedCount} assets failed to download - this may cause game crashes");
                    }
                }
                else
                {
                    LogMessage($"Error: Asset manifest is null or has no objects");
                }

                LogMessage($"Assets for {assetIndex.Id} processed successfully.");
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

                // Extract native libraries for all installations
                LogMessage("Extracting native libraries...");
                string currentVersionDir = Path.Combine(installation.GameDirectory, "versions", installation.Version);
                
                if (installation.IsModded)
                {
                    LogMessage("Verifying mod loader libraries...");
                    await VerifyAndDownloadMissingLibraries(installation, versionInfo);
                    
                    // Extract natives for modded versions
                    await ExtractNativesAsync(versionInfo.Libraries, currentVersionDir, installation.GameDirectory);
                    
                    // Also extract natives from parent version if needed
                    if (!string.IsNullOrEmpty(versionInfo.InheritsFrom))
                    {
                        string parentJsonPath = Path.Combine(installation.GameDirectory, "versions", versionInfo.InheritsFrom, $"{versionInfo.InheritsFrom}.json");
                        if (File.Exists(parentJsonPath))
                        {
                            string parentJson = await File.ReadAllTextAsync(parentJsonPath);
                            var parentInfo = JsonSerializer.Deserialize<VersionInfo>(parentJson);
                            if (parentInfo.Libraries != null)
                            {
                                await ExtractNativesAsync(parentInfo.Libraries, currentVersionDir, installation.GameDirectory);
                            }
                        }
                    }
                }
                else
                {
                    // Extract natives for vanilla versions
                    if (versionInfo.Libraries != null)
                    {
                        await ExtractNativesAsync(versionInfo.Libraries, currentVersionDir, installation.GameDirectory);
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
                string nativesPath = Path.Combine(versionDir, "natives");
                args.Add($"-Djava.library.path={nativesPath}");
                
                // Additional LWJGL debugging and library path options
                args.Add("-Dorg.lwjgl.util.Debug=true");
                args.Add("-Dorg.lwjgl.util.DebugLoader=true");
                args.Add($"-Dorg.lwjgl.librarypath={nativesPath}");
                
                LogMessage($"Native library path: {nativesPath}");
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
                // Get asset index (use parent's if current doesn't have one)
                string assetIndexId = versionInfo.AssetIndex?.Id;
                if (string.IsNullOrEmpty(assetIndexId) && !string.IsNullOrEmpty(versionInfo.InheritsFrom))
                {
                    string parentJsonPath = Path.Combine(installation.GameDirectory, "versions", versionInfo.InheritsFrom, $"{versionInfo.InheritsFrom}.json");
                    if (File.Exists(parentJsonPath))
                    {
                        try
                        {
                            string parentJson = await File.ReadAllTextAsync(parentJsonPath);
                            var parentInfo = JsonSerializer.Deserialize<VersionInfo>(parentJson);
                            assetIndexId = parentInfo.AssetIndex?.Id;
                        }
                        catch (Exception ex)
                        {
                            LogMessage($"Failed to get parent asset index: {ex.Message}");
                        }
                    }
                }
                
                args.Add("--assetIndex");
                args.Add(assetIndexId ?? "legacy");
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

        private async Task<bool> DownloadModdedVersion(Installation installation)
        {
            try
            {
                LogMessage($"Downloading modded version: {installation.ModLoader} {installation.ModLoaderVersion} for Minecraft {installation.BaseVersion}");
                
                // First, ensure the base Minecraft version is downloaded
                LogMessage($"Checking base Minecraft version: {installation.BaseVersion}");
                var baseVersionToDownload = availableVersions?.FirstOrDefault(v => v.Id == installation.BaseVersion);
                if (baseVersionToDownload == null)
                {
                    LogMessage($"Base version {installation.BaseVersion} not found in available versions");
                    MessageBox.Show($"Base Minecraft version {installation.BaseVersion} not found.",
                        "Base Version Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                // Download base version first
                string baseVersionDir = Path.Combine(installation.GameDirectory, "versions", installation.BaseVersion);
                string baseVersionJsonPath = Path.Combine(baseVersionDir, $"{installation.BaseVersion}.json");
                
                if (!File.Exists(baseVersionJsonPath))
                {
                    LogMessage($"Downloading base Minecraft version {installation.BaseVersion}...");
                    
                    // Create temporary installation for base version
                    var baseInstallation = new Installation
                    {
                        Version = installation.BaseVersion,
                        BaseVersion = installation.BaseVersion,
                        GameDirectory = installation.GameDirectory,
                        IsModded = false
                    };
                    
                    bool baseDownloaded = await DownloadVanillaVersion(baseInstallation, baseVersionToDownload);
                    if (!baseDownloaded)
                    {
                        LogMessage($"Failed to download base version {installation.BaseVersion}");
                        return false;
                    }
                }
                else
                {
                    LogMessage($"Base version {installation.BaseVersion} already exists");
                }

                Dispatcher.Invoke(() => ProgressBar.Value = 30);

                // Now download the mod loader profile
                LogMessage($"Downloading {installation.ModLoader} loader profile...");
                bool modLoaderDownloaded = await DownloadModLoaderProfile(installation);
                
                if (!modLoaderDownloaded)
                {
                    LogMessage($"Failed to download {installation.ModLoader} loader profile");
                    return false;
                }

                Dispatcher.Invoke(() => ProgressBar.Value = 100);
                LogMessage($"Successfully downloaded modded version {installation.Version}");
                
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to download modded version: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> DownloadVanillaVersion(Installation installation, MinecraftVersion versionToDownload)
        {
            string versionDir = Path.Combine(installation.GameDirectory, "versions", installation.Version);
            Directory.CreateDirectory(versionDir);

            // Download version JSON
            string versionJsonPath = Path.Combine(versionDir, $"{installation.Version}.json");
            string versionJson = await httpClient.GetStringAsync(versionToDownload.Url);
            await File.WriteAllTextAsync(versionJsonPath, versionJson);

            var versionInfo = JsonSerializer.Deserialize<VersionInfo>(versionJson);

            // Download client jar if available
            if (versionInfo.Downloads?.Client != null)
            {
                string jarPath = Path.Combine(versionDir, $"{installation.Version}.jar");
                await DownloadFileAsync(versionInfo.Downloads.Client.Url, jarPath);
                LogMessage($"Downloaded client jar: {installation.Version}.jar");
            }

            // Download libraries
            if (versionInfo.Libraries != null)
            {
                LogMessage($"Downloading {versionInfo.Libraries.Count} libraries...");
                await DownloadLibrariesAsync(versionInfo.Libraries, installation.GameDirectory);
                await ExtractNativesAsync(versionInfo.Libraries, versionDir, installation.GameDirectory);
            }

            // Download assets
            if (versionInfo.AssetIndex != null)
            {
                await DownloadAssetsAsync(versionInfo.AssetIndex, installation.GameDirectory);
            }

            return true;
        }

        private async Task<bool> DownloadModLoaderProfile(Installation installation)
        {
            try
            {
                string profileUrl = "";
                
                switch (installation.ModLoader?.ToLower())
                {
                    case "fabric":
                        profileUrl = $"https://meta.fabricmc.net/v2/versions/loader/{installation.BaseVersion}/{installation.ModLoaderVersion}/profile/json";
                        break;
                    case "quilt":
                        profileUrl = $"https://meta.quiltmc.org/v3/versions/loader/{installation.BaseVersion}/{installation.ModLoaderVersion}/profile/json";
                        break;
                    case "forge":
                        // Forge has a more complex API - this is simplified
                        LogMessage("Forge downloading not fully implemented yet");
                        return false;
                    default:
                        LogMessage($"Unknown mod loader: {installation.ModLoader}");
                        return false;
                }

                LogMessage($"Downloading mod loader profile from: {profileUrl}");
                
                // Download the mod loader profile JSON
                string profileJson = await httpClient.GetStringAsync(profileUrl);
                
                // Create the modded version directory
                string moddedVersionDir = Path.Combine(installation.GameDirectory, "versions", installation.Version);
                Directory.CreateDirectory(moddedVersionDir);
                
                // Save the profile JSON
                string moddedVersionJsonPath = Path.Combine(moddedVersionDir, $"{installation.Version}.json");
                await File.WriteAllTextAsync(moddedVersionJsonPath, profileJson);
                
                LogMessage($"Downloaded mod loader profile: {installation.Version}.json");
                
                // Parse and download any additional libraries required by the mod loader
                var moddedVersionInfo = JsonSerializer.Deserialize<VersionInfo>(profileJson);
                if (moddedVersionInfo.Libraries != null)
                {
                    LogMessage($"Downloading {moddedVersionInfo.Libraries.Count} mod loader libraries...");
                    await DownloadLibrariesAsync(moddedVersionInfo.Libraries, installation.GameDirectory);
                    
                    // Verify all libraries were downloaded
                    string libDir = Path.Combine(installation.GameDirectory, "libraries");
                    int missingCount = 0;
                    foreach (var library in moddedVersionInfo.Libraries)
                    {
                        if (library.Downloads?.Artifact != null && !string.IsNullOrEmpty(library.Downloads.Artifact.Path))
                        {
                            string libraryPath = Path.Combine(libDir, library.Downloads.Artifact.Path);
                            if (!File.Exists(libraryPath))
                            {
                                LogMessage($"Failed to download library: {library.Name}");
                                missingCount++;
                            }
                        }
                    }
                    
                    if (missingCount > 0)
                    {
                        LogMessage($"Warning: {missingCount} mod loader libraries failed to download");
                    }
                    else
                    {
                        LogMessage("All mod loader libraries downloaded successfully");
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to download mod loader profile: {ex.Message}");
                return false;
            }
        }

        private string TryConstructFabricLibraryUrl(string libraryName)
        {
            try
            {
                // Parse library name format: group:artifact:version
                var parts = libraryName.Split(':');
                if (parts.Length != 3) return null;

                string group = parts[0];
                string artifact = parts[1];
                string version = parts[2];

                // Handle different library types
                if (group == "net.fabricmc")
                {
                    // Fabric libraries from Fabric Maven
                    string baseUrl = "https://maven.fabricmc.net/";
                    string path = $"{group.Replace('.', '/')}/{artifact}/{version}/{artifact}-{version}.jar";
                    return baseUrl + path;
                }
                else if (group == "org.ow2.asm")
                {
                    // ASM libraries from Maven Central
                    string baseUrl = "https://repo1.maven.org/maven2/";
                    string path = $"{group.Replace('.', '/')}/{artifact}/{version}/{artifact}-{version}.jar";
                    return baseUrl + path;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private string ConvertMavenToPath(string libraryName)
        {
            try
            {
                // Convert Maven coordinates to file path
                // Example: net.fabricmc:fabric-loader:0.17.1 -> net/fabricmc/fabric-loader/0.17.1/fabric-loader-0.17.1.jar
                var parts = libraryName.Split(':');
                if (parts.Length != 3) return libraryName;

                string group = parts[0];
                string artifact = parts[1];
                string version = parts[2];

                return $"{group.Replace('.', '/')}/{artifact}/{version}/{artifact}-{version}.jar";
            }
            catch
            {
                return libraryName;
            }
        }

        private List<Library> DeduplicateLibraries(List<Library> libraries)
        {
            var deduplicatedLibraries = new List<Library>();
            var seenLibraries = new HashSet<string>();

            foreach (var library in libraries)
            {
                // Extract the base library name (group:artifact) without version
                string baseName = GetLibraryBaseName(library.Name);
                
                if (!seenLibraries.Contains(baseName))
                {
                    seenLibraries.Add(baseName);
                    deduplicatedLibraries.Add(library);
                }
                else
                {
                    LogMessage($"Skipping duplicate library: {library.Name}");
                }
            }

            LogMessage($"Deduplicated {libraries.Count} libraries to {deduplicatedLibraries.Count}");
            return deduplicatedLibraries;
        }

        private string GetLibraryBaseName(string libraryName)
        {
            if (string.IsNullOrEmpty(libraryName)) return "";
            
            var parts = libraryName.Split(':');
            if (parts.Length >= 2)
            {
                return $"{parts[0]}:{parts[1]}";
            }
            return libraryName;
        }
    }

    public class BoolToColorConverter : IValueConverter
    {
        public static BoolToColorConverter Instance { get; } = new BoolToColorConverter();

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool isInstalled)
            {
                return new System.Windows.Media.SolidColorBrush(
                    isInstalled ? System.Windows.Media.Colors.Green : System.Windows.Media.Colors.Orange);
            }
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToStatusConverter : IValueConverter
    {
        public static BoolToStatusConverter Instance { get; } = new BoolToStatusConverter();

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool isInstalled)
            {
                return isInstalled ? "INSTALLED" : "NOT INSTALLED";
            }
            return "UNKNOWN";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToModdedConverter : IValueConverter
    {
        public static BoolToModdedConverter Instance { get; } = new BoolToModdedConverter();

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool isModded)
            {
                return isModded ? "• MODDED" : "";
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}