using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace JustLauncher
{
    public partial class PlayPage : UserControl
    {
        private readonly string username;
        private string minecraftDirectory;
        private string versionsDirectory;
        private string librariesDirectory;
        private string assetsDirectory;
        private readonly HttpClient httpClient;
        private List<MinecraftVersion> availableVersions;
        private List<MinecraftVersion> filteredVersions;

        public PlayPage(string username)
        {
            InitializeComponent();
            this.username = username;

            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "JustLauncher/1.0");

            InitializeDirectories();
            LoadVersions();
            _ = LoadAvailableVersionsAsync();
        }

        private void InitializeDirectories()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            minecraftDirectory = Path.Combine(appData, ".minecraft");
            versionsDirectory = Path.Combine(minecraftDirectory, "versions");
            librariesDirectory = Path.Combine(minecraftDirectory, "libraries");
            assetsDirectory = Path.Combine(minecraftDirectory, "assets");
        }

        private void LoadVersions()
        {
            VersionComboBox.Items.Clear();
            var allVersions = new List<string>();

            if (Directory.Exists(versionsDirectory))
            {
                var mainVersions = Directory.GetDirectories(versionsDirectory)
                    .Select(Path.GetFileName)
                    .Where(v => !string.IsNullOrEmpty(v))
                    .ToList();

                allVersions.AddRange(mainVersions);

                var subdirs = Directory.GetDirectories(versionsDirectory, "*", SearchOption.AllDirectories)
                    .Where(dir => File.Exists(Path.Combine(dir, Path.GetFileName(dir) + ".json")))
                    .Select(dir => Path.GetRelativePath(versionsDirectory, dir))
                    .Where(v => !string.IsNullOrEmpty(v) && v != "." && !allVersions.Contains(v));

                allVersions.AddRange(subdirs);
            }

            var sortedVersions = allVersions.OrderByDescending(v => v).ToList();

            foreach (var version in sortedVersions)
            {
                VersionComboBox.Items.Add(version);
            }

            if (VersionComboBox.Items.Count > 0 && VersionComboBox.SelectedIndex == -1)
            {
                VersionComboBox.SelectedIndex = 0;
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

            bool showReleases = true;
            bool showSnapshots = true;
            bool showBeta = true;

            filteredVersions = availableVersions.Where(v =>
            {
                return (showReleases && v.Type == "release") ||
                       (showSnapshots && v.Type == "snapshot") ||
                       (showBeta && (v.Type == "old_beta" || v.Type == "old_alpha"));
            }).Take(50).ToList();

            DownloadListBox.Items.Clear();
            foreach (var version in filteredVersions)
            {
                bool isInstalled = Directory.Exists(Path.Combine(versionsDirectory, version.Id));
                string displayText = $"{version.Id} ({version.Type})" + (isInstalled ? " ✓" : "");
                DownloadListBox.Items.Add(new VersionListItem
                {
                    Version = version,
                    DisplayText = displayText,
                    IsInstalled = isInstalled
                });
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

        private void DownloadListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
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

                LogMessage($"Downloading Minecraft {version.Id}...");

                string versionDir = Path.Combine(versionsDirectory, version.Id);
                Directory.CreateDirectory(versionDir);

                string versionJsonPath = Path.Combine(versionDir, $"{version.Id}.json");
                string versionJson = await httpClient.GetStringAsync(version.Url);
                await File.WriteAllTextAsync(versionJsonPath, versionJson);

                var versionInfo = JsonSerializer.Deserialize<VersionInfo>(versionJson);
                DownloadProgressBar.Value = 10;

                if (!string.IsNullOrEmpty(versionInfo.InheritsFrom))
                {
                    LogMessage($"This version inherits from {versionInfo.InheritsFrom}");

                    string parentVersionDir = Path.Combine(versionsDirectory, versionInfo.InheritsFrom);
                    if (!Directory.Exists(parentVersionDir))
                    {
                        LogMessage($"Parent version {versionInfo.InheritsFrom} not found. Attempting to download...");

                        if (availableVersions != null)
                        {
                            var parentVersion = availableVersions.FirstOrDefault(v => v.Id == versionInfo.InheritsFrom);
                            if (parentVersion != null)
                            {
                                LogMessage($"Found parent version, downloading {parentVersion.Id}...");
                                await DownloadVersionAsync(parentVersion);
                            }
                            else
                            {
                                LogMessage($"Parent version {versionInfo.InheritsFrom} not found in available versions list");
                            }
                        }
                    }
                    else
                    {
                        LogMessage($"Parent version {versionInfo.InheritsFrom} already exists");
                    }
                }

                if (versionInfo.Downloads?.Client != null)
                {
                    string jarPath = Path.Combine(versionDir, $"{version.Id}.jar");
                    await DownloadFileAsync(versionInfo.Downloads.Client.Url, jarPath);
                    LogMessage($"Downloaded client jar: {version.Id}.jar");
                    DownloadProgressBar.Value = 40;
                }
                else
                {
                    LogMessage("No client download found - this version uses the parent version's jar");
                    DownloadProgressBar.Value = 40;
                }

                if (versionInfo.Libraries != null)
                {
                    LogMessage($"Downloading {versionInfo.Libraries.Count} libraries...");
                    await DownloadLibrariesAsync(versionInfo.Libraries);
                    DownloadProgressBar.Value = 70;

                    await ExtractNativesAsync(versionInfo.Libraries, versionDir);
                    DownloadProgressBar.Value = 85;
                }

                var assetIndex = versionInfo.AssetIndex;
                if (assetIndex == null && !string.IsNullOrEmpty(versionInfo.InheritsFrom))
                {
                    string parentJsonPath = Path.Combine(versionsDirectory, versionInfo.InheritsFrom, $"{versionInfo.InheritsFrom}.json");
                    if (File.Exists(parentJsonPath))
                    {
                        string parentJson = await File.ReadAllTextAsync(parentJsonPath);
                        var parentInfo = JsonSerializer.Deserialize<VersionInfo>(parentJson);
                        assetIndex = parentInfo.AssetIndex;
                    }
                }

                if (assetIndex != null)
                {
                    await DownloadAssetsAsync(assetIndex);
                }

                DownloadProgressBar.Value = 100;

                LogMessage($"Successfully downloaded Minecraft {version.Id}");
                LoadVersions();
                ApplyVersionFilters();
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to download {version.Id}: {ex.Message}");
                LogMessage($"Stack trace: {ex.StackTrace}");
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
        private async Task DownloadAssetsAsync(AssetIndex assetIndex)
        {
            try
            {
                LogMessage($"Downloading assets for {assetIndex.Id}...");

                string indexPath = Path.Combine(assetsDirectory, "indexes", $"{assetIndex.Id}.json");
                Directory.CreateDirectory(Path.GetDirectoryName(indexPath));

                string indexJson = await httpClient.GetStringAsync(assetIndex.Url);
                await File.WriteAllTextAsync(indexPath, indexJson);

                var assetManifest = JsonSerializer.Deserialize<AssetManifest>(indexJson);
                if (assetManifest?.Objects != null)
                {
                    string objectsDir = Path.Combine(assetsDirectory, "objects");
                    Directory.CreateDirectory(objectsDir);

                    foreach (var asset in assetManifest.Objects)
                    {
                        string hash = asset.Value.Hash;
                        string subDir = Path.Combine(objectsDir, hash.Substring(0, 2));
                        Directory.CreateDirectory(subDir);

                        string assetPath = Path.Combine(subDir, hash);
                        if (!File.Exists(assetPath))
                        {
                            string assetUrl = $"https://resources.download.minecraft.net/{hash.Substring(0, 2)}/{hash}";
                            await DownloadFileAsync(assetUrl, assetPath);
                        }
                    }
                }

                LogMessage($"Assets for {assetIndex.Id} downloaded successfully.");
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to download assets for {assetIndex.Id}: {ex.Message}");
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

                LogMessage($"File downloaded successfully to {destinationPath}");
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to download file from {url}: {ex.Message}");
            }
        }
        private async Task DownloadLibrariesAsync(List<Library> libraries)
        {
            try
            {
                foreach (var library in libraries)
                {
                    if (library.Downloads?.Artifact != null)
                    {
                        string libraryPath = Path.Combine(librariesDirectory, library.Downloads.Artifact.Path);
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
            }
        }
        private Task ExtractNativesAsync(List<Library> libraries, string versionDir)
        {
            try
            {
                string nativesDir = Path.Combine(versionDir, "natives");
                Directory.CreateDirectory(nativesDir);

                foreach (var library in libraries)
                {
                    if (library.Downloads?.Artifact != null && library.Name.Contains("natives"))
                    {
                        string nativePath = Path.Combine(librariesDirectory, library.Downloads.Artifact.Path);
                        if (File.Exists(nativePath))
                        {
                            using (var archive = System.IO.Compression.ZipFile.OpenRead(nativePath))
                            {
                                foreach (var entry in archive.Entries)
                                {
                                    if (!string.IsNullOrEmpty(entry.Name)) // Skip directories
                                    {
                                        string destinationPath = Path.Combine(nativesDir, entry.FullName);
                                        entry.ExtractToFile(destinationPath, overwrite: true);
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
            
            return Task.CompletedTask;
        }

        private async void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            if (VersionComboBox.SelectedItem == null)
            {
                LogMessage("Please select a version to launch.");
                return;
            }

            string selectedVersion = VersionComboBox.SelectedItem.ToString();
            LogMessage($"Launching Minecraft {selectedVersion} for user {username}...");

            try
            {
                string versionDir = Path.Combine(versionsDirectory, selectedVersion);
                string versionJsonPath = Path.Combine(versionDir, $"{selectedVersion}.json");

                if (!File.Exists(versionJsonPath))
                {
                    LogMessage($"Version {selectedVersion} is not installed. Please download it first.");
                    return;
                }

                string versionJson = await File.ReadAllTextAsync(versionJsonPath);
                var versionInfo = JsonSerializer.Deserialize<VersionInfo>(versionJson);

                // Build classpath
                var classpathEntries = new List<string>();

                // Add libraries
                if (versionInfo.Libraries != null)
                {
                    foreach (var library in versionInfo.Libraries)
                    {
                        if (library.Downloads?.Artifact != null)
                        {
                            string libraryPath = Path.Combine(librariesDirectory, library.Downloads.Artifact.Path);
                            if (File.Exists(libraryPath))
                            {
                                classpathEntries.Add(libraryPath);
                            }
                        }
                    }
                }

                // Add main jar
                string mainJarPath = Path.Combine(versionDir, $"{selectedVersion}.jar");
                if (File.Exists(mainJarPath))
                {
                    classpathEntries.Add(mainJarPath);
                }

                string classpath = string.Join(";", classpathEntries);

                // Build launch arguments
                var args = new List<string>();
                args.Add("-Xmx2G"); // Max memory
                args.Add("-Xms1G"); // Min memory
                args.Add($"-Djava.library.path={Path.Combine(versionDir, "natives")}");
                args.Add("-cp");
                args.Add($"\"{classpath}\"");
                args.Add(versionInfo.MainClass);

                // Game arguments
                args.Add("--username");
                args.Add(username);
                args.Add("--version");
                args.Add(selectedVersion);
                args.Add("--gameDir");
                args.Add(minecraftDirectory);
                args.Add("--assetsDir");
                args.Add(assetsDirectory);
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
                    FileName = "java",
                    Arguments = string.Join(" ", args),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                LogMessage($"Launching with command: java {startInfo.Arguments}");

                var process = Process.Start(startInfo);
                if (process != null)
                {
                    LogMessage("Minecraft launched successfully!");
                    
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
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to launch Minecraft: {ex.Message}");
            }
        }
    }

    public class AssetManifest
    {
        [JsonPropertyName("objects")]
        public Dictionary<string, AssetObject> Objects { get; set; }
    }

}