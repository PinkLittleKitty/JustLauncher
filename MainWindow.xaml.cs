using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace JustLauncher
{
    public partial class MainWindow : Window
    {
        private string minecraftDirectory;
        private string versionsDirectory;
        private string librariesDirectory;
        private string assetsDirectory;
        private readonly HttpClient httpClient;
        private List<MinecraftVersion> availableVersions;
        private List<MinecraftVersion> filteredVersions;
        private List<GameProfile> gameProfiles;
        private GameProfile selectedProfile;
        private string profilesConfigPath;

        public MainWindow()
        {
            InitializeComponent();
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "JustLauncher/1.0");
            InitializeDirectories();
            LoadProfiles();
            LoadVersions();
            _ = LoadAvailableVersionsAsync();
            
            _ = TestJavaDetectionAsync();
        }

        private async Task TestJavaDetectionAsync()
        {
            await Task.Run(() =>
            {
                LogMessage("Testing Java detection...");
                var javaInstallations = FindAllJavaInstallations();
                
                if (javaInstallations.Count == 0)
                {
                    LogMessage("No Java installations found!");
                    LogMessage("Please install Java from: https://adoptium.net/");
                }
                else
                {
                    LogMessage($"Found {javaInstallations.Count} Java installation(s):");
                    foreach (var java in javaInstallations)
                    {
                        LogMessage($"  Java {java.version} at: {java.path}");
                    }
                }
            });
        }

        private void InitializeDirectories()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            minecraftDirectory = Path.Combine(appData, ".minecraft");
            versionsDirectory = Path.Combine(minecraftDirectory, "versions");
            librariesDirectory = Path.Combine(minecraftDirectory, "libraries");
            assetsDirectory = Path.Combine(minecraftDirectory, "assets");
            profilesConfigPath = Path.Combine(minecraftDirectory, "justlauncher_profiles.json");

            Directory.CreateDirectory(minecraftDirectory);
            Directory.CreateDirectory(versionsDirectory);
            Directory.CreateDirectory(librariesDirectory);
            Directory.CreateDirectory(assetsDirectory);

            GameDirTextBox.Text = minecraftDirectory;
        }

        private void LoadProfiles()
        {
            try
            {
                gameProfiles = new List<GameProfile>();
                
                if (File.Exists(profilesConfigPath))
                {
                    string profilesJson = File.ReadAllText(profilesConfigPath);
                    var profilesData = JsonSerializer.Deserialize<ProfilesConfig>(profilesJson);
                    gameProfiles = profilesData?.profiles ?? new List<GameProfile>();
                }

                // Add default profile if none exist
                if (gameProfiles.Count == 0)
                {
                    gameProfiles.Add(new GameProfile
                    {
                        Name = "Default",
                        GameDirectory = minecraftDirectory,
                        JavaArgs = "-Xmx4096M -Xms4096M"
                    });
                    SaveProfiles();
                }

                // Update UI
                ProfileComboBox.Items.Clear();
                foreach (var profile in gameProfiles)
                {
                    ProfileComboBox.Items.Add(profile);
                }

                // Select first profile
                var defaultProfile = gameProfiles.First();
                ProfileComboBox.SelectedItem = defaultProfile;
                OnProfileSelected(defaultProfile);

                LogMessage($"Loaded {gameProfiles.Count} profiles");
            }
            catch (Exception ex)
            {
                LogMessage($"Error loading profiles: {ex.Message}");
                // Create default profile on error
                gameProfiles = new List<GameProfile>
                {
                    new GameProfile
                    {
                        Name = "Default",
                        GameDirectory = minecraftDirectory,
                        JavaArgs = "-Xmx4096M -Xms4096M"
                    }
                };
                SaveProfiles();
            }
        }

        private void SaveProfiles()
        {
            try
            {
                var profilesConfig = new ProfilesConfig { profiles = gameProfiles };
                string profilesJson = JsonSerializer.Serialize(profilesConfig, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(profilesConfigPath, profilesJson);
            }
            catch (Exception ex)
            {
                LogMessage($"Error saving profiles: {ex.Message}");
            }
        }

        private void OnProfileSelected(GameProfile profile)
        {
            selectedProfile = profile;
            
            // Update directories based on selected profile
            minecraftDirectory = profile.GameDirectory;
            versionsDirectory = Path.Combine(minecraftDirectory, "versions");
            librariesDirectory = Path.Combine(minecraftDirectory, "libraries");
            assetsDirectory = Path.Combine(minecraftDirectory, "assets");
            
            // Update UI
            GameDirTextBox.Text = minecraftDirectory;
            UsernameTextBox.Text = profile.LastUsedUsername ?? "Player";
            
            // Parse memory from Java args
            var memoryMatch = Regex.Match(profile.JavaArgs ?? "", @"-Xmx(\d+)M");
            if (memoryMatch.Success)
            {
                MemoryTextBox.Text = memoryMatch.Groups[1].Value;
            }
            
            // Reload versions for this profile
            LoadVersions();
            
            // Select the profile's last used version
            if (!string.IsNullOrEmpty(profile.LastUsedVersion))
            {
                for (int i = 0; i < VersionComboBox.Items.Count; i++)
                {
                    if (VersionComboBox.Items[i].ToString() == profile.LastUsedVersion)
                    {
                        VersionComboBox.SelectedIndex = i;
                        break;
                    }
                }
            }
            
            LogMessage($"Switched to profile: {profile.Name}");
        }

        private void LoadVersions()
        {
            try
            {
                VersionComboBox.Items.Clear();
                var allVersions = new List<string>();
                
                if (Directory.Exists(versionsDirectory))
                {
                    // Get versions from main versions directory
                    var mainVersions = Directory.GetDirectories(versionsDirectory)
                        .Select(Path.GetFileName)
                        .Where(v => !string.IsNullOrEmpty(v))
                        .ToList();
                    
                    allVersions.AddRange(mainVersions);
                    
                    // Also check for versions in subdirectories (like modpack instances)
                    var subdirs = Directory.GetDirectories(versionsDirectory, "*", SearchOption.AllDirectories)
                        .Where(dir => File.Exists(Path.Combine(dir, Path.GetFileName(dir) + ".json")))
                        .Select(dir => Path.GetRelativePath(versionsDirectory, dir))
                        .Where(v => !string.IsNullOrEmpty(v) && v != "." && !allVersions.Contains(v));
                    
                    allVersions.AddRange(subdirs);
                }

                // Sort versions
                var sortedVersions = allVersions.OrderByDescending(v => v).ToList();

                foreach (var version in sortedVersions)
                {
                    VersionComboBox.Items.Add(version);
                }

                if (VersionComboBox.Items.Count > 0 && VersionComboBox.SelectedIndex == -1)
                {
                    VersionComboBox.SelectedIndex = 0;
                }

                LogMessage($"Found {VersionComboBox.Items.Count} Minecraft versions in profile '{selectedProfile?.Name}'");
            }
            catch (Exception ex)
            {
                LogMessage($"Error loading versions: {ex.Message}");
            }
        }

        private void ProfileComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ProfileComboBox.SelectedItem is GameProfile profile)
            {
                OnProfileSelected(profile);
            }
        }

        private void NewProfileButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ProfileDialog();
            if (dialog.ShowDialog() == true)
            {
                var newProfile = new GameProfile
                {
                    Name = dialog.ProfileName,
                    GameDirectory = dialog.GameDirectory,
                    JavaArgs = $"-Xmx{dialog.Memory}M -Xms{dialog.Memory}M",
                    LastUsedUsername = dialog.Username
                };

                gameProfiles.Add(newProfile);
                SaveProfiles();
                
                ProfileComboBox.Items.Add(newProfile);
                ProfileComboBox.SelectedItem = newProfile;
                
                LogMessage($"Created new profile: {newProfile.Name}");
            }
        }

        private void EditProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedProfile == null) return;

            var dialog = new ProfileDialog(selectedProfile);
            if (dialog.ShowDialog() == true)
            {
                selectedProfile.Name = dialog.ProfileName;
                selectedProfile.GameDirectory = dialog.GameDirectory;
                selectedProfile.JavaArgs = $"-Xmx{dialog.Memory}M -Xms{dialog.Memory}M";
                selectedProfile.LastUsedUsername = dialog.Username;

                SaveProfiles();
                
                // Refresh profile combo box
                int selectedIndex = ProfileComboBox.SelectedIndex;
                ProfileComboBox.Items.Clear();
                foreach (var profile in gameProfiles)
                {
                    ProfileComboBox.Items.Add(profile);
                }
                ProfileComboBox.SelectedIndex = selectedIndex;
                
                OnProfileSelected(selectedProfile);
                LogMessage($"Updated profile: {selectedProfile.Name}");
            }
        }

        private void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedProfile == null || gameProfiles.Count <= 1) return;

            var result = MessageBox.Show($"Are you sure you want to delete profile '{selectedProfile.Name}'?", 
                                       "Delete Profile", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                gameProfiles.Remove(selectedProfile);
                SaveProfiles();
                
                ProfileComboBox.Items.Remove(selectedProfile);
                ProfileComboBox.SelectedIndex = 0;
                
                LogMessage($"Deleted profile: {selectedProfile.Name}");
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
                availableVersions = manifest.versions ?? new List<MinecraftVersion>();
                
                ApplyVersionFilters();
                
                LogMessage($"Loaded {availableVersions.Count} available versions");
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to load available versions: {ex.Message}");
            }
        }

        private void ApplyVersionFilters()
        {
            if (availableVersions == null) return;

            filteredVersions = availableVersions.Where(v =>
            {
                bool showReleases = ShowReleasesCheckBox.IsChecked ?? false;
                bool showSnapshots = ShowSnapshotsCheckBox.IsChecked ?? false;
                bool showBeta = ShowBetaCheckBox.IsChecked ?? false;

                return (showReleases && v.type == "release") ||
                       (showSnapshots && v.type == "snapshot") ||
                       (showBeta && (v.type == "old_beta" || v.type == "old_alpha"));
            }).Take(50).ToList();

            Dispatcher.Invoke(() =>
            {
                DownloadListBox.Items.Clear();
                foreach (var version in filteredVersions)
                {
                    bool isInstalled = Directory.Exists(Path.Combine(versionsDirectory, version.id));
                    string displayText = $"{version.id} ({version.type})" + (isInstalled ? " ✓" : "");
                    DownloadListBox.Items.Add(new VersionListItem 
                    { 
                        Version = version, 
                        DisplayText = displayText, 
                        IsInstalled = isInstalled 
                    });
                }
            });
        }

        private void FilterCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            ApplyVersionFilters();
        }

        private void LogMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                LogTextBox.ScrollToEnd();
            });
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Minecraft Directory",
                InitialDirectory = minecraftDirectory
            };

            if (dialog.ShowDialog() == true)
            {
                minecraftDirectory = dialog.FolderName;
                versionsDirectory = Path.Combine(minecraftDirectory, "versions");
                librariesDirectory = Path.Combine(minecraftDirectory, "libraries");
                assetsDirectory = Path.Combine(minecraftDirectory, "assets");
                GameDirTextBox.Text = minecraftDirectory;
                LoadVersions();
            }
        }

        private void RefreshVersionsButton_Click(object sender, RoutedEventArgs e)
        {
            LoadVersions();
            _ = LoadAvailableVersionsAsync();
        }

        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadAvailableVersionsAsync();
        }

        private void DownloadListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            DownloadSelectedButton.IsEnabled = DownloadListBox.SelectedItem != null;
        }

        private async void DownloadSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            if (DownloadListBox.SelectedItem is VersionListItem selectedItem)
            {
                await DownloadVersionAsync(selectedItem.Version);
            }
        }

        private async Task DownloadVersionAsync(MinecraftVersion version)
        {
            try
            {
                DownloadSelectedButton.IsEnabled = false;
                DownloadProgressBar.Visibility = Visibility.Visible;
                DownloadProgressBar.Value = 0;

                LogMessage($"Downloading Minecraft {version.id}...");

                string versionDir = Path.Combine(versionsDirectory, version.id);
                Directory.CreateDirectory(versionDir);

                // Download version JSON
                string versionJsonPath = Path.Combine(versionDir, $"{version.id}.json");
                string versionJson = await httpClient.GetStringAsync(version.url);
                await File.WriteAllTextAsync(versionJsonPath, versionJson);

                var versionInfo = JsonSerializer.Deserialize<VersionInfo>(versionJson);
                DownloadProgressBar.Value = 10;

                // Check if this is an inherits version (like Fabric)
                if (!string.IsNullOrEmpty(versionInfo.inheritsFrom))
                {
                    LogMessage($"This version inherits from {versionInfo.inheritsFrom}");
                    
                    // Check if parent version exists, if not, try to download it
                    string parentVersionDir = Path.Combine(versionsDirectory, versionInfo.inheritsFrom);
                    if (!Directory.Exists(parentVersionDir))
                    {
                        LogMessage($"Parent version {versionInfo.inheritsFrom} not found. Attempting to download...");
                        
                        // Try to find and download the parent version
                        if (availableVersions != null)
                        {
                            var parentVersion = availableVersions.FirstOrDefault(v => v.id == versionInfo.inheritsFrom);
                            if (parentVersion != null)
                            {
                                LogMessage($"Found parent version, downloading {parentVersion.id}...");
                                await DownloadVersionAsync(parentVersion);
                            }
                            else
                            {
                                LogMessage($"Parent version {versionInfo.inheritsFrom} not found in available versions list");
                            }
                        }
                    }
                    else
                    {
                        LogMessage($"Parent version {versionInfo.inheritsFrom} already exists");
                    }
                }

                // Download client jar (if available)
                if (versionInfo.downloads?.client != null)
                {
                    string jarPath = Path.Combine(versionDir, $"{version.id}.jar");
                    await DownloadFileAsync(versionInfo.downloads.client.url, jarPath);
                    LogMessage($"Downloaded client jar: {version.id}.jar");
                    DownloadProgressBar.Value = 40;
                }
                else
                {
                    LogMessage("No client download found - this version uses the parent version's jar");
                    DownloadProgressBar.Value = 40;
                }

                // Download libraries
                if (versionInfo.libraries != null)
                {
                    LogMessage($"Downloading {versionInfo.libraries.Count} libraries...");
                    await DownloadLibrariesAsync(versionInfo.libraries);
                    DownloadProgressBar.Value = 70;

                    // Extract natives
                    await ExtractNativesAsync(versionInfo.libraries, versionDir);
                    DownloadProgressBar.Value = 85;
                }

                // Download assets (use parent version's asset index if not specified)
                var assetIndex = versionInfo.assetIndex;
                if (assetIndex == null && !string.IsNullOrEmpty(versionInfo.inheritsFrom))
                {
                    // Try to get asset index from parent version
                    string parentJsonPath = Path.Combine(versionsDirectory, versionInfo.inheritsFrom, $"{versionInfo.inheritsFrom}.json");
                    if (File.Exists(parentJsonPath))
                    {
                        string parentJson = await File.ReadAllTextAsync(parentJsonPath);
                        var parentInfo = JsonSerializer.Deserialize<VersionInfo>(parentJson);
                        assetIndex = parentInfo.assetIndex;
                    }
                }

                if (assetIndex != null)
                {
                    await DownloadAssetsAsync(assetIndex);
                }

                DownloadProgressBar.Value = 100;

                LogMessage($"Successfully downloaded Minecraft {version.id}");
                LoadVersions();
                ApplyVersionFilters();
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to download {version.id}: {ex.Message}");
                LogMessage($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                DownloadSelectedButton.IsEnabled = true;
                DownloadProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private async Task DownloadFileAsync(string url, string filePath)
        {
            using var response = await httpClient.GetAsync(url);
            using var fileStream = new FileStream(filePath, FileMode.Create);
            await response.Content.CopyToAsync(fileStream);
        }

        private async Task DownloadLibrariesAsync(List<Library> libraries)
        {
            foreach (var library in libraries)
            {
                if (library.downloads?.artifact != null)
                {
                    string libraryPath = Path.Combine(librariesDirectory, library.downloads.artifact.path);
                    Directory.CreateDirectory(Path.GetDirectoryName(libraryPath));
                    
                    if (!File.Exists(libraryPath))
                    {
                        try
                        {
                            await DownloadFileAsync(library.downloads.artifact.url, libraryPath);
                        }
                        catch (Exception ex)
                        {
                            LogMessage($"Failed to download library {library.downloads.artifact.path}: {ex.Message}");
                        }
                    }
                }
            }
        }

        private async Task ExtractNativesAsync(List<Library> libraries, string versionDir)
        {
            string nativesDir = Path.Combine(versionDir, "natives");
            Directory.CreateDirectory(nativesDir);

            foreach (var library in libraries)
            {
                if (library.downloads?.classifiers?.natives_windows != null)
                {
                    string nativePath = Path.Combine(librariesDirectory, library.downloads.classifiers.natives_windows.path);
                    
                    if (!File.Exists(nativePath))
                    {
                        try
                        {
                            await DownloadFileAsync(library.downloads.classifiers.natives_windows.url, nativePath);
                        }
                        catch (Exception ex)
                        {
                            LogMessage($"Failed to download native {library.downloads.classifiers.natives_windows.path}: {ex.Message}");
                            continue;
                        }
                    }

                    try
                    {
                        using (var archive = ZipFile.OpenRead(nativePath))
                        {
                            foreach (var entry in archive.Entries)
                            {
                                if (entry.Name.EndsWith(".dll") || entry.Name.EndsWith(".so") || entry.Name.EndsWith(".dylib"))
                                {
                                    string extractPath = Path.Combine(nativesDir, entry.Name);
                                    if (!File.Exists(extractPath))
                                    {
                                        entry.ExtractToFile(extractPath, true);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Failed to extract natives from {nativePath}: {ex.Message}");
                    }
                }
            }
        }

        private async Task DownloadAssetsAsync(AssetIndex assetIndex)
        {
            if (assetIndex == null) return;

            string assetIndexPath = Path.Combine(assetsDirectory, "indexes", $"{assetIndex.id}.json");
            Directory.CreateDirectory(Path.GetDirectoryName(assetIndexPath));
            
            if (!File.Exists(assetIndexPath))
            {
                await DownloadFileAsync(assetIndex.url, assetIndexPath);
            }

            string assetIndexJson = await File.ReadAllTextAsync(assetIndexPath);
            var assets = JsonSerializer.Deserialize<AssetIndexInfo>(assetIndexJson);

            if (assets?.objects == null) return;

            string objectsDir = Path.Combine(assetsDirectory, "objects");
            Directory.CreateDirectory(objectsDir);

            // Download MORE assets - sounds and music are critical
            int downloadedCount = 0;
            int totalAssets = Math.Min(assets.objects.Count, 1000); // Increased from 200
            
            LogMessage($"Downloading {totalAssets} assets for sounds and textures...");
            
            foreach (var asset in assets.objects.Take(totalAssets))
            {
                string hash = asset.Value.hash;
                string assetDir = Path.Combine(objectsDir, hash.Substring(0, 2));
                string assetPath = Path.Combine(assetDir, hash);
                
                Directory.CreateDirectory(assetDir);
                
                if (!File.Exists(assetPath))
                {
                    try
                    {
                        string assetUrl = $"https://resources.download.minecraft.net/{hash.Substring(0, 2)}/{hash}";
                        await DownloadFileAsync(assetUrl, assetPath);
                        downloadedCount++;
                        
                        if (downloadedCount % 50 == 0)
                        {
                            LogMessage($"Downloaded {downloadedCount}/{totalAssets} assets...");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Failed to download asset {hash}: {ex.Message}");
                    }
                }
            }
            
            LogMessage($"Asset download complete: {downloadedCount} assets downloaded");
        }

        private async void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
            {
                LogMessage("Please enter a username");
                return;
            }

            if (VersionComboBox.SelectedItem == null)
            {
                LogMessage("Please select a Minecraft version");
                return;
            }

            if (selectedProfile == null)
            {
                LogMessage("No profile selected");
                return;
            }

            LaunchButton.IsEnabled = false;
            LaunchButton.Content = "🚀 Launching...";

            try
            {
                await LaunchMinecraft();
            }
            catch (Exception ex)
            {
                LogMessage($"Launch failed: {ex.Message}");
            }
            finally
            {
                LaunchButton.IsEnabled = true;
                LaunchButton.Content = "🚀 Launch Minecraft";
            }
        }

        private async Task LaunchMinecraft()
        {
            string version = VersionComboBox.SelectedItem.ToString();
            string username = UsernameTextBox.Text;
            string memory = MemoryTextBox.Text;
            bool offlineMode = OfflineModeCheckBox.IsChecked ?? true;

            LogMessage($"Launching Minecraft {version} for {username} using profile '{selectedProfile.Name}'");

            string versionDir = Path.Combine(versionsDirectory, version);
            string jarFile = Path.Combine(versionDir, $"{version}.jar");
            string jsonFile = Path.Combine(versionDir, $"{version}.json");

            if (!File.Exists(jarFile))
            {
                LogMessage($"Minecraft jar not found: {jarFile}");
                LogMessage("Please download this version first using the Download panel.");
                return;
            }

            if (!File.Exists(jsonFile))
            {
                LogMessage($"Version JSON not found: {jsonFile}");
                LogMessage("Please re-download this version to get the complete installation.");
                return;
            }

            int requiredJavaVersion = GetRequiredJavaVersion(version);
            string javaPath = FindJavaPath(requiredJavaVersion);
            
            if (string.IsNullOrEmpty(javaPath))
            {
                LogMessage($"Java {requiredJavaVersion}+ not found.");
                LogMessage("Searching for any Java installation...");
                
                var allJava = FindAllJavaInstallations();
                if (allJava.Count > 0)
                {
                    LogMessage("Found Java installations but none meet the minimum version requirement:");
                    foreach (var java in allJava)
                    {
                        LogMessage($"  Java {java.version} at: {java.path}");
                    }
                    LogMessage($"Minecraft {version} requires Java {requiredJavaVersion}+");
                }
                else
                {
                    LogMessage("No Java installations found at all!");
                }
                
                LogMessage("Download Java from: https://adoptium.net/");
                return;
            }

            LogMessage($"Using Java: {javaPath}");
            LogMessage($"Using game directory: {selectedProfile.GameDirectory}");

            string uuid = offlineMode ? GenerateOfflineUUID(username) : Guid.NewGuid().ToString("N");
            string accessToken = offlineMode ? "offline" : "invalid";

            string arguments = await BuildLaunchArgumentsAsync(memory, jarFile, jsonFile, username, uuid, accessToken, version);

            LogMessage($"Launch arguments: {arguments}");
            LogMessage("Starting Minecraft process...");

            var processInfo = new ProcessStartInfo
            {
                FileName = javaPath,
                Arguments = arguments,
                WorkingDirectory = selectedProfile.GameDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // Update profile with current settings
            selectedProfile.LastUsedUsername = username;
            selectedProfile.LastUsedVersion = version;
            selectedProfile.JavaArgs = $"-Xmx{memory}M -Xms{memory}M";
            SaveProfiles();

            try
            {
                var process = Process.Start(processInfo);
                if (process != null)
                {
                    process.OutputDataReceived += (s, args) => {
                        if (!string.IsNullOrEmpty(args.Data))
                            LogMessage($"MC: {args.Data}");
                    };
                    
                    process.ErrorDataReceived += (s, args) => {
                        if (!string.IsNullOrEmpty(args.Data))
                            LogMessage($"MC Error: {args.Data}");
                    };

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    LogMessage($"Minecraft started successfully! Process ID: {process.Id}");
                    
                    _ = Task.Run(async () =>
                    {
                        await Task.Run(() => process.WaitForExit());
                        LogMessage($"Minecraft process ended with exit code: {process.ExitCode}");
                        
                        // Check if process exited immediately (likely an error)
                        if (process.ExitCode != 0)
                        {
                            LogMessage($"Minecraft exited with error code {process.ExitCode}");
                        }
                    });
                }
                else
                {
                    LogMessage("Failed to start process - Process.Start returned null");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to start Minecraft: {ex.Message}");
            }
        }

        private int GetRequiredJavaVersion(string version)
        {
            if (version.Contains("1.21") || version.Contains("1.20.5") || version.Contains("1.20.6"))
                return 21;
            if (version.Contains("1.20") || version.Contains("1.19") || version.Contains("1.18"))
                return 17;
            if (version.Contains("1.17"))
                return 16;
            
            return 8;
        }

        private List<(string path, int version)> FindAllJavaInstallations()
        {
            var javaInstallations = new List<(string path, int version)>();

            // Check common installation directories
            string[] possibleBasePaths = {
                @"C:\Program Files\Java",
                @"C:\Program Files (x86)\Java",
                @"C:\Program Files\Eclipse Adoptium",
                @"C:\Program Files\Microsoft\jdk",
                @"C:\Program Files\Amazon Corretto",
                @"C:\Program Files\BellSoft\LibericaJDK",
                @"C:\Program Files\Zulu\zulu-*",
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Programs\Eclipse Adoptium"
            };

            foreach (string basePath in possibleBasePaths)
            {
                try
                {
                    if (basePath.Contains("*"))
                    {
                        string parentDir = Path.GetDirectoryName(basePath);
                        if (Directory.Exists(parentDir))
                        {
                            var matchingDirs = Directory.GetDirectories(parentDir, Path.GetFileName(basePath));
                            foreach (string matchingDir in matchingDirs)
                            {
                                CheckJavaInDirectory(matchingDir, javaInstallations);
                            }
                        }
                    }
                    else if (Directory.Exists(basePath))
                    {
                        CheckJavaInDirectory(basePath, javaInstallations);
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"Error checking Java directory {basePath}: {ex.Message}");
                }
            }

            // Check system PATH
            try
            {
                int systemJavaVersion = GetJavaVersionDetailed("java");
                if (systemJavaVersion > 0)
                {
                    javaInstallations.Add(("java", systemJavaVersion));
                }
            }
            catch { }

            // Check JAVA_HOME
            try
            {
                string javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
                if (!string.IsNullOrEmpty(javaHome))
                {
                    string javaExe = Path.Combine(javaHome, "bin", "java.exe");
                    if (File.Exists(javaExe))
                    {
                        int version = GetJavaVersionDetailed(javaExe);
                        if (version > 0)
                        {
                            javaInstallations.Add((javaExe, version));
                        }
                    }
                }
            }
            catch { }

            // Remove duplicates and sort by version
            return javaInstallations
                .GroupBy(j => j.version)
                .Select(g => g.First())
                .OrderByDescending(j => j.version)
                .ToList();
        }

        private void CheckJavaInDirectory(string basePath, List<(string path, int version)> javaInstallations)
        {
            try
            {
                var javaDirs = Directory.GetDirectories(basePath, "*", SearchOption.TopDirectoryOnly);

                foreach (string javaDir in javaDirs)
                {
                    string javaExe = Path.Combine(javaDir, "bin", "java.exe");
                    if (File.Exists(javaExe))
                    {
                        int version = GetJavaVersionDetailed(javaExe);
                        if (version > 0)
                        {
                            javaInstallations.Add((javaExe, version));
                        }
                    }
                }
            }
            catch { }
        }

        private string FindJavaPath(int minVersion = 8)
        {
            var javaInstallations = FindAllJavaInstallations();

            var compatibleJava = javaInstallations
                .Where(j => j.version >= minVersion)
                .OrderByDescending(j => j.version)
                .FirstOrDefault();

            if (compatibleJava.path != null)
            {
                LogMessage($"Found compatible Java {compatibleJava.version} at: {compatibleJava.path}");
                return compatibleJava.path;
            }

            return null;
        }

        private int GetJavaVersionDetailed(string javaPath)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = javaPath,
                        Arguments = "-version",
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string errorOutput = process.StandardError.ReadToEnd();
                string standardOutput = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                string output = errorOutput + standardOutput;

                // Try multiple patterns to extract version
                var patterns = new[]
                {
                    @"version ""(\d+)\.(\d+)\.(\d+)",           // Modern format: "17.0.1"
                    @"version ""(\d+)""",                       // Simple format: "17"
                    @"version ""1\.(\d+)\.(\d+)",              // Legacy format: "1.8.0"
                    @"openjdk version ""(\d+)\.(\d+)\.(\d+)",   // OpenJDK format
                    @"java version ""(\d+)\.(\d+)\.(\d+)",      // Oracle format
                };

                foreach (string pattern in patterns)
                {
                    var match = Regex.Match(output, pattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        if (match.Groups.Count >= 2)
                        {
                            if (int.TryParse(match.Groups[1].Value, out int majorVersion))
                            {
                                // Handle legacy versioning (1.8 = Java 8)
                                if (majorVersion == 1 && match.Groups.Count >= 3)
                                {
                                    if (int.TryParse(match.Groups[2].Value, out int minorVersion))
                                    {
                                        return minorVersion;
                                    }
                                }
                                else
                                {
                                    return majorVersion;
                                }
                            }
                        }
                    }
                }

                // Fallback: look for any number after "version"
                var fallbackMatch = Regex.Match(output, @"version.*?(\d+)", RegexOptions.IgnoreCase);
                if (fallbackMatch.Success && int.TryParse(fallbackMatch.Groups[1].Value, out int fallbackVersion))
                {
                    return fallbackVersion >= 100 ? fallbackVersion / 10 : fallbackVersion; // Handle cases like "180" -> 18
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error getting Java version from {javaPath}: {ex.Message}");
            }

            return 0;
        }

        private string GenerateOfflineUUID(string username)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes($"OfflinePlayer:{username}"));
                hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
                hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        private List<string> RemoveDuplicateArguments(List<string> args)
        {
            var result = new List<string>();
            var seenFlags = new HashSet<string>();
            
            for (int i = 0; i < args.Count; i++)
            {
                string arg = args[i];
                
                // Check if this is a flag (starts with --)
                if (arg.StartsWith("--"))
                {
                    // Special handling for quick play arguments - only allow one
                    if (arg.StartsWith("--quickPlay"))
                    {
                        if (seenFlags.Any(f => f.StartsWith("--quickPlay")))
                        {
                            // Skip this quick play argument and its value
                            if (i + 1 < args.Count && !args[i + 1].StartsWith("-"))
                                i++; // Skip the value too
                            continue;
                        }
                    }
                    
                    if (!seenFlags.Contains(arg))
                    {
                        seenFlags.Add(arg);
                        result.Add(arg);
                        
                        // Add the value if it exists and doesn't start with -
                        if (i + 1 < args.Count && !args[i + 1].StartsWith("-"))
                        {
                            i++;
                            result.Add(args[i]);
                        }
                    }
                    else
                    {
                        // Skip duplicate flag and its value
                        if (i + 1 < args.Count && !args[i + 1].StartsWith("-"))
                            i++; // Skip the value too
                    }
                }
                else
                {
                    // Not a flag, just add it
                    result.Add(arg);
                }
            }
            
            return result;
        }

        private async Task<string> BuildLaunchArgumentsAsync(string memory, string jarFile, string jsonFile, string username, string uuid, string accessToken, string version)
        {
            var args = new List<string>();

            try
            {
                string versionJson = await File.ReadAllTextAsync(jsonFile);
                var versionInfo = JsonSerializer.Deserialize<VersionInfo>(versionJson);

                LogMessage($"Processing version info for {version}");

                // JVM Arguments
                args.Add($"-Xmx{memory}M");
                args.Add($"-Xms{memory}M");
                args.Add("-Dminecraft.launcher.brand=JustLauncher");
                args.Add("-Dminecraft.launcher.version=1.0");
                
                // Critical: Ensure proper asset directory configuration
                args.Add($"-Dminecraft.client.jar=\"{jarFile}\"");
                args.Add($"-Djava.awt.headless=false");

                // Build classpath
                var classpath = new List<string>();
                
                // For inherited versions (like Fabric)
                if (!string.IsNullOrEmpty(versionInfo.inheritsFrom))
                {
                    LogMessage($"Loading parent version libraries from {versionInfo.inheritsFrom}");
                    await AddParentLibraries(classpath, versionInfo.inheritsFrom);
                }

                // Add current version libraries
                if (versionInfo.libraries != null)
                {
                    int addedLibraries = 0;
                    
                    foreach (var library in versionInfo.libraries)
                    {
                        if (ShouldIncludeLibrary(library))
                        {
                            if (library.downloads?.artifact != null)
                            {
                                string libraryPath = Path.Combine(librariesDirectory, library.downloads.artifact.path);
                                if (File.Exists(libraryPath))
                                {
                                    classpath.Add(libraryPath);
                                    addedLibraries++;
                                }
                                else
                                {
                                    try
                                    {
                                        Directory.CreateDirectory(Path.GetDirectoryName(libraryPath));
                                        await DownloadFileAsync(library.downloads.artifact.url, libraryPath);
                                        classpath.Add(libraryPath);
                                        addedLibraries++;
                                    }
                                    catch (Exception ex)
                                    {
                                        LogMessage($"Failed to download library: {ex.Message}");
                                    }
                                }
                            }
                        }
                    }
                    
                    LogMessage($"Added {addedLibraries} libraries to classpath");
                }

                // Add jar file
                if (!string.IsNullOrEmpty(versionInfo.inheritsFrom))
                {
                    string parentJarFile = Path.Combine(versionsDirectory, versionInfo.inheritsFrom, $"{versionInfo.inheritsFrom}.jar");
                    if (File.Exists(parentJarFile))
                    {
                        classpath.Add(parentJarFile);
                    }
                }
                else
                {
                    classpath.Add(jarFile);
                }

                args.Add("-cp");
                args.Add($"\"{string.Join(Path.PathSeparator, classpath)}\"");

                // Natives directory
                string nativesDir = Path.Combine(versionsDirectory, version, "natives");
                if (!Directory.Exists(nativesDir) && !string.IsNullOrEmpty(versionInfo.inheritsFrom))
                {
                    nativesDir = Path.Combine(versionsDirectory, versionInfo.inheritsFrom, "natives");
                }
                
                if (Directory.Exists(nativesDir))
                {
                    args.Add($"-Djava.library.path=\"{nativesDir}\"");
                }

                // Main class
                string mainClass = versionInfo.mainClass ?? "net.minecraft.client.main.Main";
                args.Add(mainClass);

                // Get correct asset index
                string assetIndexName = GetAssetIndexName(versionInfo, version);
                LogMessage($"Using asset index: {assetIndexName}");

                // Game arguments with proper asset configuration
                if (versionInfo.arguments != null)
                {
                    await AddModernGameArguments(args, versionInfo.arguments, username, version, uuid, accessToken, assetIndexName);
                }
                else if (!string.IsNullOrEmpty(versionInfo.minecraftArguments))
                {
                    AddLegacyGameArguments(args, versionInfo.minecraftArguments, username, version, uuid, accessToken, assetIndexName);
                }
                else
                {
                    AddFallbackGameArguments(args, username, version, uuid, accessToken, assetIndexName);
                }

                // Ensure critical asset arguments are present
                await EnsureAssetArgumentsAsync(args, assetIndexName, versionInfo);

                // Remove duplicate arguments to prevent conflicts
                args = RemoveDemoModeArguments(RemoveDuplicateArguments(args));
            }
            catch (Exception ex)
            {
                LogMessage($"Error building launch arguments: {ex.Message}");
                return BuildBasicLaunchArguments(memory, jarFile, username, uuid, accessToken, version);
            }

            return string.Join(" ", args);
        }

        private List<string> RemoveDemoModeArguments(List<string> args)
        {
            // Remove any demo-related arguments
            var filtered = new List<string>();
            for (int i = 0; i < args.Count; i++)
            {
                string arg = args[i];
                // Check for demo flags
                if (arg.Equals("--demo", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("--demoMode", StringComparison.OrdinalIgnoreCase))
                {
                    // Skip this argument and its value if it has one
                    if (i + 1 < args.Count && !args[i + 1].StartsWith("-"))
                        i++;
                    continue;
                }
                filtered.Add(arg);
            }
            return filtered;
        }

        private string GetAssetIndexName(VersionInfo versionInfo, string version)
        {
            // Try to get asset index from version info
            if (versionInfo?.assetIndex?.id != null)
            {
                return versionInfo.assetIndex.id;
            }

            // For inherited versions, try to get from parent
            if (!string.IsNullOrEmpty(versionInfo?.inheritsFrom))
            {
                try
                {
                    string parentJsonPath = Path.Combine(versionsDirectory, versionInfo.inheritsFrom, $"{versionInfo.inheritsFrom}.json");
                    if (File.Exists(parentJsonPath))
                    {
                        string parentJson = File.ReadAllText(parentJsonPath);
                        var parentInfo = JsonSerializer.Deserialize<VersionInfo>(parentJson);
                        if (parentInfo?.assetIndex?.id != null)
                        {
                            return parentInfo.assetIndex.id;
                        }
                    }
                }
                catch { }
            }

            // Fallback to version name
            return version;
        }

        private async Task EnsureAssetArgumentsAsync(List<string> args, string assetIndexName, VersionInfo versionInfo)
        {
            // Check if assets are properly configured
            bool hasAssetsDir = false;
            bool hasAssetIndex = false;
            
            for (int i = 0; i < args.Count - 1; i++)
            {
                if (args[i] == "--assetsDir" || args[i] == "--assets")
                    hasAssetsDir = true;
                if (args[i] == "--assetIndex")
                    hasAssetIndex = true;
            }
            
            // Add missing asset arguments
            if (!hasAssetsDir)
            {
                args.Add("--assetsDir");
                args.Add($"\"{assetsDirectory}\"");
            }
            
            if (!hasAssetIndex)
            {
                args.Add("--assetIndex");
                args.Add(assetIndexName);
            }

            // Ensure asset index file exists
            string assetIndexPath = Path.Combine(assetsDirectory, "indexes", $"{assetIndexName}.json");
            if (!File.Exists(assetIndexPath))
            {
                LogMessage($"Asset index missing: {assetIndexName}");
                
                // Try to download asset index
                if (versionInfo?.assetIndex?.url != null)
                {
                    try
                    {
                        LogMessage($"Downloading asset index: {assetIndexName}");
                        Directory.CreateDirectory(Path.GetDirectoryName(assetIndexPath));
                        await DownloadFileAsync(versionInfo.assetIndex.url, assetIndexPath);
                        LogMessage($"Downloaded asset index: {assetIndexName}");
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Failed to download asset index: {ex.Message}");
                    }
                }
            }
            
            LogMessage($"Assets directory: {assetsDirectory}");
            LogMessage($"Asset index: {assetIndexName}");
        }

        private async Task AddModernGameArguments(List<string> args, Arguments arguments, string username, string version, string uuid, string accessToken, string assetIndexName)
        {
            if (arguments.game != null)
            {
                foreach (var arg in arguments.game)
                {
                    if (arg is JsonElement element)
                    {
                        if (element.ValueKind == JsonValueKind.String)
                        {
                            string argValue = element.GetString();
                            args.Add(ReplaceArgumentVariables(argValue, username, version, uuid, accessToken, assetIndexName));
                        }
                        else if (element.ValueKind == JsonValueKind.Object)
                        {
                            var argObj = JsonSerializer.Deserialize<ConditionalArgument>(element.GetRawText());
                            if (ShouldIncludeConditionalArgument(argObj))
                            {
                                if (argObj.value != null)
                                {
                                    if (argObj.value is JsonElement valueElement)
                                    {
                                        if (valueElement.ValueKind == JsonValueKind.Array)
                                        {
                                            foreach (JsonElement item in valueElement.EnumerateArray())
                                            {
                                                if (item.ValueKind == JsonValueKind.String)
                                                {
                                                    args.Add(ReplaceArgumentVariables(item.GetString(), username, version, uuid, accessToken, assetIndexName));
                                                }
                                            }
                                        }
                                        else if (valueElement.ValueKind == JsonValueKind.String)
                                        {
                                            args.Add(ReplaceArgumentVariables(valueElement.GetString(), username, version, uuid, accessToken, assetIndexName));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                AddFallbackGameArguments(args, username, version, uuid, accessToken, assetIndexName);
            }
        }

        private void AddLegacyGameArguments(List<string> args, string minecraftArguments, string username, string version, string uuid, string accessToken, string assetIndexName)
        {
            string processedArgs = ReplaceArgumentVariables(minecraftArguments, username, version, uuid, accessToken, assetIndexName);
            var argParts = processedArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            args.AddRange(argParts);
        }

        private void AddFallbackGameArguments(List<string> args, string username, string version, string uuid, string accessToken, string assetIndexName)
        {
            var gameArgs = new List<string>
            {
                "--username", username,
                "--version", version,
                "--gameDir", $"\"{selectedProfile.GameDirectory}\"",
                "--assetsDir", $"\"{assetsDirectory}\"",
                "--assetIndex", assetIndexName,
                "--uuid", uuid,
                "--accessToken", accessToken,
                "--userType", "legacy"
            };

            args.AddRange(gameArgs);
        }

        private string ReplaceArgumentVariables(string argument, string username, string version, string uuid, string accessToken, string assetIndexName)
        {
            return argument
                .Replace("${auth_player_name}", username)
                .Replace("${version_name}", version)
                .Replace("${game_directory}", selectedProfile.GameDirectory)
                .Replace("${assets_root}", assetsDirectory)
                .Replace("${assets_index_name}", assetIndexName)
                .Replace("${auth_uuid}", uuid)
                .Replace("${auth_access_token}", accessToken)
                .Replace("${user_type}", "legacy")
                .Replace("${version_type}", "release")
                .Replace("${resolution_width}", "854")
                .Replace("${resolution_height}", "480")
                .Replace("${game_assets}", assetsDirectory)
                .Replace("${auth_session}", $"token:{accessToken}:{uuid}")
                .Replace("${user_properties}", "{}")
                .Replace("${launcher_name}", "JustLauncher")
                .Replace("${launcher_version}", "1.0")
                .Replace("${clientid}", uuid)
                .Replace("${auth_xuid}", uuid)
                .Replace("${classpath}", "");
        }

        private async Task AddParentLibraries(List<string> classpath, string parentVersion)
        {
            try
            {
                string parentJsonPath = Path.Combine(versionsDirectory, parentVersion, $"{parentVersion}.json");
                if (!File.Exists(parentJsonPath))
                {
                    LogMessage($"Parent version JSON not found: {parentJsonPath}");
                    return;
                }

                string parentJson = await File.ReadAllTextAsync(parentJsonPath);
                var parentInfo = JsonSerializer.Deserialize<VersionInfo>(parentJson);

                if (parentInfo?.libraries != null)
                {
                    LogMessage($"Adding {parentInfo.libraries.Count} parent libraries to classpath");
                    
                    foreach (var library in parentInfo.libraries)
                    {
                        if (ShouldIncludeLibrary(library))
                        {
                            if (library.downloads?.artifact != null)
                            {
                                string libraryPath = Path.Combine(librariesDirectory, library.downloads.artifact.path);
                                if (File.Exists(libraryPath))
                                {
                                    classpath.Add(libraryPath);
                                }
                                else
                                {
                                    try
                                    {
                                        Directory.CreateDirectory(Path.GetDirectoryName(libraryPath));
                                        await DownloadFileAsync(library.downloads.artifact.url, libraryPath);
                                        classpath.Add(libraryPath);
                                    }
                                    catch (Exception ex)
                                    {
                                        LogMessage($"Failed to download parent library: {ex.Message}");
                                    }
                                }
                            }
                        }
                    }
                }

                // If parent also inherits from another version, recursively add those libraries too
                if (!string.IsNullOrEmpty(parentInfo?.inheritsFrom))
                {
                    await AddParentLibraries(classpath, parentInfo.inheritsFrom);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error loading parent libraries from {parentVersion}: {ex.Message}");
            }
        }

        private bool ShouldIncludeConditionalArgument(ConditionalArgument arg)
        {
            if (arg.rules == null) return true;

            bool allowed = false;
            foreach (var rule in arg.rules)
            {
                if (rule.action == "allow")
                {
                    if (rule.os == null || rule.os.name == "windows")
                        allowed = true;
                }
                else if (rule.action == "disallow")
                {
                    if (rule.os == null || rule.os.name == "windows")
                        allowed = false;
                }
            }

            return allowed;
        }

        private bool ShouldIncludeLibrary(Library library)
        {
            if (library.rules == null || library.rules.Count == 0)
                return true;

            bool allowed = false;
            foreach (var rule in library.rules)
            {
                if (rule.action == "allow")
                {
                    if (rule.os == null || rule.os.name == "windows")
                        allowed = true;
                }
                else if (rule.action == "disallow")
                {
                    if (rule.os == null || rule.os.name == "windows")
                        allowed = false;
                }
            }

            return allowed;
        }

        private string BuildBasicLaunchArguments(string memory, string jarFile, string username, string uuid, string accessToken, string version)
        {
            var args = new List<string>
            {
                $"-Xmx{memory}M",
                $"-Xms{memory}M",
                "-cp", $"\"{jarFile}\"",
                "net.minecraft.client.main.Main",
                "--username", username,
                "--version", version,
                "--gameDir", $"\"{minecraftDirectory}\"",
                "--assetsDir", $"\"{assetsDirectory}\"",
                "--assetIndex", version,
                "--uuid", uuid,
                "--accessToken", accessToken,
                "--userType", "legacy"
            };

            return string.Join(" ", args);
        }

        protected override void OnClosed(EventArgs e)
        {
            httpClient?.Dispose();
            base.OnClosed(e);
        }
    }

   
    public class GameProfile
    {
        public string Name { get; set; }
        public string GameDirectory { get; set; }
        public string JavaArgs { get; set; }
        public string LastUsedVersion { get; set; }
        public string LastUsedUsername { get; set; }
        public bool IsDefault { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }
}
