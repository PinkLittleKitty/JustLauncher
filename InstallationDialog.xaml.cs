using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace JustLauncher
{
    public partial class InstallationDialog : Window
    {
        private readonly HttpClient httpClient;
        private List<MinecraftVersion> availableVersions;
        private Installation editingInstallation;
        
        public Installation Result { get; private set; }

        public InstallationDialog(Installation installation = null)
        {
            InitializeComponent();
            
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "JustLauncher/1.0");
            
            editingInstallation = installation;
            
            if (installation != null)
            {
                DialogTitle.Text = "Edit Installation";
                CreateButton.Content = "Save";
                LoadInstallationData(installation);
            }
            
            LoadDefaultGameDirectory();
            _ = LoadAvailableVersionsAsync();
        }

        private void LoadInstallationData(Installation installation)
        {
            NameTextBox.Text = installation.Name;
            GameDirectoryTextBox.Text = installation.GameDirectory;
            JavaArgsTextBox.Text = installation.JavaArgs;
            
            if (!string.IsNullOrEmpty(installation.JavaPath) && installation.JavaPath != "Use system default")
            {
                JavaPathTextBox.Text = installation.JavaPath;
            }
            
            // Set icon
            foreach (var item in IconComboBox.Items.Cast<System.Windows.Controls.ComboBoxItem>())
            {
                if (item.Tag?.ToString() == installation.Icon)
                {
                    IconComboBox.SelectedItem = item;
                    break;
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
                var filteredVersions = availableVersions
                    .Where(v => v.Type == "release" || (v.Type == "snapshot" && v.ReleaseTime > DateTime.Now.AddMonths(-6)))
                    .Take(50)
                    .ToList();

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
                    var currentVersion = VersionComboBox.Items.Cast<VersionItem>()
                        .FirstOrDefault(v => v.Version.Id == editingInstallation.Version);
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

        private void VersionComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Show mod loader options for newer versions
            if (VersionComboBox.SelectedItem is VersionItem versionItem)
            {
                var version = versionItem.Version.Id;
                var versionParts = version.Split('.');
                
                if (versionParts.Length >= 2 && int.TryParse(versionParts[1], out int minorVersion))
                {
                    // Show mod loader options for 1.14+ (when Fabric became popular)
                    ModLoaderPanel.Visibility = minorVersion >= 14 ? Visibility.Visible : Visibility.Collapsed;
                }
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
                
                if (IconComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem iconItem)
                {
                    installation.Icon = iconItem.Tag?.ToString() ?? "grass_block";
                }
                
                // Handle mod loader
                if (ModLoaderComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem modLoaderItem)
                {
                    var modLoader = modLoaderItem.Content.ToString();
                    if (modLoader != "None")
                    {
                        installation.IsModded = true;
                        installation.ModLoader = modLoader.ToLower();
                        // You would typically load mod loader versions here
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