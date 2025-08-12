using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace JustLauncher
{
    public partial class InstallationDialog : Window
    {
        private readonly HttpClient httpClient;
        private List<MinecraftVersion> availableVersions;
        private Installation editingInstallation;
        private Dictionary<string, List<ModLoaderVersion>> modLoaderVersions;
        
        public Installation Result { get; private set; }

        public InstallationDialog(Installation installation = null)
        {
            InitializeComponent();
            
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "JustLauncher/1.0");
            
            editingInstallation = installation;
            modLoaderVersions = new Dictionary<string, List<ModLoaderVersion>>();
            
            LoadDefaultGameDirectory();
            
            if (installation != null)
            {
                DialogTitle.Text = "Edit Installation";
                CreateButton.Content = "Save Changes";
                _ = LoadAvailableVersionsAsync().ContinueWith(_ => 
                    Dispatcher.Invoke(() => LoadInstallationData(installation)));
            }
            else
            {
                _ = LoadAvailableVersionsAsync();
            }
            
            Dispatcher.BeginInvoke(new Action(() => UpdateMemoryFromSlider()), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void LoadInstallationData(Installation installation)
        {
            NameTextBox.Text = installation.Name;
            GameDirectoryTextBox.Text = installation.GameDirectory;
            JavaArgsTextBox.Text = installation.JavaArgs ?? "-Xmx2G -Xms1G";
            
            if (!string.IsNullOrEmpty(installation.JavaPath) && installation.JavaPath != "Use system default")
            {
                JavaPathTextBox.Text = installation.JavaPath;
            }
            
            var memoryMatch = System.Text.RegularExpressions.Regex.Match(installation.JavaArgs ?? "", @"-Xmx(\d+)G");
            if (memoryMatch.Success && int.TryParse(memoryMatch.Groups[1].Value, out int memory))
            {
                MemorySlider.Value = Math.Min(Math.Max(memory, 1), 8);
            }
            
            foreach (var item in IconComboBox.Items.Cast<ComboBoxItem>())
            {
                if (item.Tag?.ToString() == installation.Icon)
                {
                    IconComboBox.SelectedItem = item;
                    break;
                }
            }
            
            if (installation.IsModded && !string.IsNullOrEmpty(installation.ModLoader))
            {
                foreach (var item in ModLoaderComboBox.Items.Cast<ComboBoxItem>())
                {
                    if (item.Tag?.ToString() == installation.ModLoader)
                    {
                        ModLoaderComboBox.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        private void LoadDefaultGameDirectory()
        {
            if (string.IsNullOrEmpty(GameDirectoryTextBox.Text))
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                GameDirectoryTextBox.Text = Path.Combine(appData, ".minecraft");
            }
        }

        private async Task LoadAvailableVersionsAsync()
        {
            try
            {
                string manifestUrl = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
                string manifestJson = await httpClient.GetStringAsync(manifestUrl);

                var manifest = JsonSerializer.Deserialize<VersionManifest>(manifestJson);
                availableVersions = manifest.Versions ?? new List<MinecraftVersion>();

                // Filter to show only releases and recent snapshots
                var filteredVersions = FilterVersions(availableVersions);

                VersionComboBox.Items.Clear();
                foreach (var version in filteredVersions)
                {
                    VersionComboBox.Items.Add(new VersionItem 
                    { 
                        Version = version, 
                        DisplayText = $"{version.Id} ({version.Type})" 
                    });
                }

                // Select current version if editing
                if (editingInstallation != null)
                {
                    // For modded installations, use the base version
                    string versionToFind = editingInstallation.IsModded ? editingInstallation.BaseVersion : editingInstallation.Version;
                    
                    var currentVersion = VersionComboBox.Items.Cast<VersionItem>()
                        .FirstOrDefault(v => v.Version.Id == versionToFind);
                    if (currentVersion != null)
                    {
                        VersionComboBox.SelectedItem = currentVersion;
                    }
                }
                else if (VersionComboBox.Items.Count > 0)
                {
                    VersionComboBox.SelectedIndex = 0; // Select latest version
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load versions: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void VersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Reset mod loader when version changes
            if (ModLoaderComboBox != null)
            {
                ModLoaderComboBox.SelectedIndex = 0; // Select "None"
            }
            
            if (ModLoaderVersionPanel != null)
            {
                ModLoaderVersionPanel.Visibility = Visibility.Collapsed;
            }
        }

        private async void ModLoaderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ModLoaderComboBox.SelectedItem is ComboBoxItem selectedItem && ModLoaderVersionPanel != null)
            {
                string modLoader = selectedItem.Tag?.ToString();
                
                if (modLoader == "none")
                {
                    ModLoaderVersionPanel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ModLoaderVersionPanel.Visibility = Visibility.Visible;
                    await LoadModLoaderVersionsAsync(modLoader);
                }
            }
        }

        private void MemorySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateMemoryFromSlider();
        }

        private void UpdateMemoryFromSlider()
        {
            if (MemorySlider != null && MemoryLabel != null && JavaArgsTextBox != null)
            {
                int memoryGB = (int)MemorySlider.Value;
                MemoryLabel.Text = $"Allocated Memory: {memoryGB}GB";
                
                // Update Java arguments
                string currentArgs = JavaArgsTextBox.Text ?? "-Xmx2G -Xms1G";
                
                // Replace existing -Xmx and -Xms arguments
                currentArgs = System.Text.RegularExpressions.Regex.Replace(currentArgs, @"-Xmx\d+[GMK]?", $"-Xmx{memoryGB}G");
                currentArgs = System.Text.RegularExpressions.Regex.Replace(currentArgs, @"-Xms\d+[GMK]?", $"-Xms{Math.Min(memoryGB, 1)}G");
                
                // If no memory arguments found, add them
                if (!currentArgs.Contains("-Xmx"))
                {
                    currentArgs = $"-Xmx{memoryGB}G -Xms{Math.Min(memoryGB, 1)}G " + currentArgs.Trim();
                }
                
                JavaArgsTextBox.Text = currentArgs.Trim();
            }
        }

        private void RefreshVersionsButton_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadAvailableVersionsAsync();
        }

        private void BrowseGameDirectory_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Game Directory",
                InitialDirectory = GameDirectoryTextBox.Text
            };

            if (dialog.ShowDialog() == true)
            {
                GameDirectoryTextBox.Text = dialog.FolderName;
            }
        }

        private void BrowseJavaPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Java Executable",
                Filter = "Java Executable (java.exe)|java.exe|All Files (*.*)|*.*",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
            };

            if (dialog.ShowDialog() == true)
            {
                JavaPathTextBox.Text = dialog.FileName;
            }
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateInput())
            {
                var installation = editingInstallation ?? new Installation();
                
                installation.Name = NameTextBox.Text.Trim();
                installation.GameDirectory = GameDirectoryTextBox.Text.Trim();
                installation.JavaArgs = JavaArgsTextBox.Text.Trim();
                
                if (JavaPathTextBox.Text != "Use system default")
                {
                    installation.JavaPath = JavaPathTextBox.Text.Trim();
                }
                
                if (VersionComboBox.SelectedItem is VersionItem versionItem)
                {
                    installation.Version = versionItem.Version.Id;
                    installation.BaseVersion = versionItem.Version.Id;
                }
                
                if (IconComboBox.SelectedItem is ComboBoxItem iconItem)
                {
                    installation.Icon = iconItem.Tag?.ToString() ?? "grass_block";
                }
                
                // Handle mod loader
                if (ModLoaderComboBox.SelectedItem is ComboBoxItem modLoaderItem)
                {
                    var modLoader = modLoaderItem.Tag?.ToString();
                    if (modLoader != "none" && !string.IsNullOrEmpty(modLoader))
                    {
                        installation.IsModded = true;
                        installation.ModLoader = modLoader;
                        installation.BaseVersion = installation.Version; // Store base MC version
                        
                        // Get mod loader version
                        if (ModLoaderVersionComboBox?.SelectedItem is ModLoaderVersionItem modLoaderVersionItem)
                        {
                            installation.ModLoaderVersion = modLoaderVersionItem.Version.Version;
                            
                            // Create modded version ID (e.g., "fabric-loader-0.15.11-1.21.1")
                            installation.Version = $"{modLoader}-loader-{installation.ModLoaderVersion}-{installation.BaseVersion}";
                        }
                        else
                        {
                            // If no specific version selected, don't create modded installation
                            MessageBox.Show("Please select a mod loader version.", "Validation Error", 
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }
                    else
                    {
                        installation.IsModded = false;
                        installation.ModLoader = null;
                        installation.ModLoaderVersion = null;
                        installation.BaseVersion = installation.Version;
                    }
                }
                
                if (editingInstallation == null)
                {
                    installation.Created = DateTime.Now;
                }
                
                Result = installation;
                DialogResult = true;
                Close();
            }
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                MessageBox.Show("Please enter an installation name.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            
            if (VersionComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please select a Minecraft version.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            
            if (string.IsNullOrWhiteSpace(GameDirectoryTextBox.Text))
            {
                MessageBox.Show("Please select a game directory.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            
            return true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void VersionFilter_Changed(object sender, RoutedEventArgs e)
        {
            if (availableVersions != null && VersionComboBox != null)
            {
                var filteredVersions = FilterVersions(availableVersions);
                
                VersionComboBox.Items.Clear();
                foreach (var version in filteredVersions)
                {
                    VersionComboBox.Items.Add(new VersionItem 
                    { 
                        Version = version, 
                        DisplayText = $"{version.Id} ({version.Type})" 
                    });
                }

                if (VersionComboBox.Items.Count > 0)
                {
                    VersionComboBox.SelectedIndex = 0;
                }
            }
        }

        private List<MinecraftVersion> FilterVersions(List<MinecraftVersion> versions)
        {
            var filtered = versions.AsEnumerable();

            // Apply filters based on checkboxes
            var includeReleases = ShowReleasesCheckBox?.IsChecked == true;
            var includeSnapshots = ShowSnapshotsCheckBox?.IsChecked == true;
            var includeBetas = ShowBetasCheckBox?.IsChecked == true;

            if (!includeReleases && !includeSnapshots && !includeBetas)
            {
                // If nothing is selected, default to releases
                includeReleases = true;
                if (ShowReleasesCheckBox != null)
                    ShowReleasesCheckBox.IsChecked = true;
            }

            filtered = filtered.Where(v =>
            {
                return v.Type switch
                {
                    "release" => includeReleases,
                    "snapshot" => includeSnapshots,
                    "old_beta" or "old_alpha" => includeBetas,
                    _ => false
                };
            });

            // Limit snapshots to recent ones if included
            if (includeSnapshots)
            {
                var recentDate = DateTime.Now.AddMonths(-6);
                filtered = filtered.Where(v => 
                    v.Type != "snapshot" || v.ReleaseTime > recentDate);
            }

            // Take top 100 versions to avoid overwhelming the UI
            return filtered.Take(100).ToList();
        }

        private async Task LoadModLoaderVersionsAsync(string modLoader)
        {
            try
            {
                if (VersionComboBox.SelectedItem is not VersionItem versionItem || ModLoaderVersionComboBox == null)
                    return;

                string minecraftVersion = versionItem.Version.Id;
                
                ModLoaderVersionComboBox.Items.Clear();
                ModLoaderVersionComboBox.IsEnabled = false;
                
                if (LoadingVersionsText != null)
                {
                    LoadingVersionsText.Visibility = Visibility.Visible;
                }

                List<ModLoaderVersion> versions = new List<ModLoaderVersion>();

                switch (modLoader.ToLower())
                {
                    case "fabric":
                        versions = await LoadFabricVersionsAsync(minecraftVersion);
                        break;
                    case "forge":
                        versions = await LoadForgeVersionsAsync(minecraftVersion);
                        break;
                    case "quilt":
                        versions = await LoadQuiltVersionsAsync(minecraftVersion);
                        break;
                }

                ModLoaderVersionComboBox.Items.Clear();
                ModLoaderVersionComboBox.IsEnabled = true;
                
                if (LoadingVersionsText != null)
                {
                    LoadingVersionsText.Visibility = Visibility.Collapsed;
                }
                
                if (versions.Any())
                {
                    foreach (var version in versions.Take(20)) // Limit to 20 most recent
                    {
                        ModLoaderVersionComboBox.Items.Add(new ModLoaderVersionItem
                        {
                            Version = version,
                            DisplayText = $"{version.Version} ({version.Type})"
                        });
                    }
                    ModLoaderVersionComboBox.SelectedIndex = 0;
                }
                else
                {
                    var noVersionsItem = new ModLoaderVersionItem
                    {
                        Version = new ModLoaderVersion { Version = "none", Type = "none", Url = "" },
                        DisplayText = "No versions available"
                    };
                    ModLoaderVersionComboBox.Items.Add(noVersionsItem);
                    ModLoaderVersionComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                ModLoaderVersionComboBox.Items.Clear();
                ModLoaderVersionComboBox.IsEnabled = true;
                
                if (LoadingVersionsText != null)
                {
                    LoadingVersionsText.Visibility = Visibility.Collapsed;
                }
                
                var errorItem = new ModLoaderVersionItem
                {
                    Version = new ModLoaderVersion { Version = "error", Type = "error", Url = "" },
                    DisplayText = $"Error: {ex.Message}"
                };
                ModLoaderVersionComboBox.Items.Add(errorItem);
                ModLoaderVersionComboBox.SelectedIndex = 0;
            }
        }

        private async Task<List<ModLoaderVersion>> LoadFabricVersionsAsync(string minecraftVersion)
        {
            try
            {
                // Fabric Loader versions
                string loaderUrl = "https://meta.fabricmc.net/v2/versions/loader";
                string loaderJson = await httpClient.GetStringAsync(loaderUrl);
                var loaderVersions = JsonSerializer.Deserialize<List<FabricLoaderVersion>>(loaderJson);

                var versions = new List<ModLoaderVersion>();
                foreach (var loader in loaderVersions.Take(10)) // Top 10 loader versions
                {
                    versions.Add(new ModLoaderVersion
                    {
                        Version = loader.Version,
                        Type = loader.Stable ? "stable" : "beta",
                        Url = $"https://meta.fabricmc.net/v2/versions/loader/{minecraftVersion}/{loader.Version}/profile/json"
                    });
                }

                return versions;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load Fabric versions: {ex.Message}");
                return new List<ModLoaderVersion>();
            }
        }

        private async Task<List<ModLoaderVersion>> LoadForgeVersionsAsync(string minecraftVersion)
        {
            try
            {
                // Forge versions (simplified - would need proper Forge API integration)
                string forgeUrl = $"https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json";
                string forgeJson = await httpClient.GetStringAsync(forgeUrl);
                
                // This is a simplified implementation - real Forge API is more complex
                var versions = new List<ModLoaderVersion>
                {
                    new ModLoaderVersion { Version = "Latest", Type = "recommended", Url = "" },
                    new ModLoaderVersion { Version = "Recommended", Type = "stable", Url = "" }
                };

                return versions;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load Forge versions: {ex.Message}");
                return new List<ModLoaderVersion>();
            }
        }

        private async Task<List<ModLoaderVersion>> LoadQuiltVersionsAsync(string minecraftVersion)
        {
            try
            {
                // Quilt versions (similar to Fabric)
                string quiltUrl = "https://meta.quiltmc.org/v3/versions/loader";
                string quiltJson = await httpClient.GetStringAsync(quiltUrl);
                var loaderVersions = JsonSerializer.Deserialize<List<QuiltLoaderVersion>>(quiltJson);

                var versions = new List<ModLoaderVersion>();
                foreach (var loader in loaderVersions.Take(10))
                {
                    versions.Add(new ModLoaderVersion
                    {
                        Version = loader.Version,
                        Type = "stable",
                        Url = $"https://meta.quiltmc.org/v3/versions/loader/{minecraftVersion}/{loader.Version}/profile/json"
                    });
                }

                return versions;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load Quilt versions: {ex.Message}");
                return new List<ModLoaderVersion>();
            }
        }
    }

    public class ModLoaderVersion
    {
        public string Version { get; set; }
        public string Type { get; set; }
        public string Url { get; set; }
    }

    public class ModLoaderVersionItem
    {
        public ModLoaderVersion Version { get; set; }
        public string DisplayText { get; set; }
        
        public override string ToString()
        {
            return DisplayText;
        }
    }

    public class FabricLoaderVersion
    {
        [JsonPropertyName("version")]
        public string Version { get; set; }
        
        [JsonPropertyName("stable")]
        public bool Stable { get; set; }
    }

    public class QuiltLoaderVersion
    {
        [JsonPropertyName("version")]
        public string Version { get; set; }
    }

    public class VersionItem
    {
        public MinecraftVersion Version { get; set; }
        public string DisplayText { get; set; }
        
        public override string ToString()
        {
            return DisplayText;
        }
    }
}