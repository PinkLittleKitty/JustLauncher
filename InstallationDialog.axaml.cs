using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using JustLauncher.Services;

namespace JustLauncher;

public partial class InstallationDialog : UserControl
{
    public Installation? Result { get; private set; }
    public bool DeleteRequested { get; private set; }
    private readonly MinecraftService _minecraftService = new();
    private Installation? _existingInstallation;
    private readonly FabricService _fabricService = new();
    private readonly ForgeService _forgeService = new();
    private readonly ModManagerService _modManager = new();

    public InstallationDialog()
    {
        InitializeComponent();
        var textBox = this.FindControl<TextBox>("GameDirectoryTextBox");
        if (textBox != null) textBox.Text = PlatformManager.GetMinecraftDirectory();
        
        var nameBox = this.FindControl<TextBox>("NameTextBox");
        if (nameBox != null)
        {
            nameBox.TextChanged += async (s, e) =>
            {
                 var name = nameBox.Text;
                 var settings = await ConfigManager.LoadSettingsAsync();
                 if (settings.UseSeparateGameDir && !string.IsNullOrWhiteSpace(name))
                 {
                     var dirBox = this.FindControl<TextBox>("GameDirectoryTextBox");
                     if (dirBox != null)
                     {
                         var safeName = string.Join("_", name.Split(System.IO.Path.GetInvalidFileNameChars()));
                         var basePath = PlatformManager.GetMinecraftDirectory();
                         
                         if (string.IsNullOrWhiteSpace(dirBox.Text) || dirBox.Text.StartsWith(basePath))
                         {
                             Dispatcher.UIThread.Post(() => 
                             {
                                 dirBox.Text = System.IO.Path.Combine(basePath, "instances", safeName);
                             });
                         }
                     }
                 }
            };
        }

        var memorySlider = this.FindControl<Slider>("MemorySlider");
        if (memorySlider != null)
        {
            memorySlider.PropertyChanged += (s, e) => {
                if (e.Property == Avalonia.Controls.Primitives.RangeBase.ValueProperty)
                {
                    var memoryLabel = this.FindControl<TextBlock>("MemoryLabel");
                    if (memoryLabel != null) memoryLabel.Text = $"{(int)memorySlider.Value} GB";
                    
                    var argsBox = this.FindControl<TextBox>("JavaArgsTextBox");
                    if (argsBox != null)
                    {
                        int memoryGb = (int)memorySlider.Value;
                        if (memoryGb > 0)
                            argsBox.Text = $"-Xmx{memoryGb}G -Xms{memoryGb}G";
                        else
                            argsBox.Text = "";
                    }
                }
            };
        }

        _ = LoadDataAsync();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        var closeBtn = this.FindControl<Button>("CloseButton");
        if (closeBtn != null) closeBtn.Click += (s, e) => OverlayService.Close(null);

        var cancelBtn = this.FindControl<Button>("CancelButton");
        if (cancelBtn != null) cancelBtn.Click += (s, e) => OverlayService.Close(null);

        var createBtn = this.FindControl<Button>("CreateButton");
        if (createBtn != null) createBtn.Click += CreateButton_Click;

        var removeBtn = this.FindControl<Button>("RemoveButton");
        if (removeBtn != null) removeBtn.Click += RemoveButton_Click;

        var browseJavaBtn = this.FindControl<Button>("BrowseJavaButton");
        if (browseJavaBtn != null) browseJavaBtn.Click += BrowseJavaButton_Click;

        var memLow = this.FindControl<Button>("MemLowBtn");
        if (memLow != null) memLow.Click += (s, e) => SetMemory(2);
        
        var memMed = this.FindControl<Button>("MemMedBtn");
        if (memMed != null) memMed.Click += (s, e) => SetMemory(4);
        
        var memHigh = this.FindControl<Button>("MemHighBtn");
        if (memHigh != null) memHigh.Click += (s, e) => SetMemory(8);

        var loaderCombo = this.FindControl<ComboBox>("ModLoaderComboBox");
        if (loaderCombo != null) loaderCombo.SelectionChanged += LoaderCombo_SelectionChanged;

        var versionCombo = this.FindControl<ComboBox>("VersionComboBox");
        if (versionCombo != null) versionCombo.SelectionChanged += async (s, e) => await UpdateLoaderVersions();

        
    }

    private void SetMemory(double gb)
    {
        var slider = this.FindControl<Slider>("MemorySlider");
        if (slider != null) slider.Value = gb;
    }

    private async void BrowseJavaButton_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var result = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Select Java Executable",
            AllowMultiple = false
        });

        if (result != null && result.Count > 0)
        {
            var path = result[0].Path.LocalPath;
            var combo = this.FindControl<ComboBox>("JavaVersionComboBox");
            if (combo != null)
            {
                 var items = new List<object>();
                 if (combo.ItemsSource is IEnumerable<object> existing) items.AddRange(existing);
                 
                 var existingItem = items.OfType<JavaInfo>().FirstOrDefault(j => j.Path == path);
                 if (existingItem != null)
                 {
                     combo.SelectedItem = existingItem;
                 }
                 else
                 {
                     var info = new JavaInfo { Path = path, Version = "Custom", MajorVersion = 0, IsJre = true };
                     items.Add(info);
                     combo.ItemsSource = items;
                     combo.SelectedItem = info;
                 }
            }
        }
    }

    private async Task LoadDataAsync()
    {
        await LoadVersionsAsync();
        await LoadJavaVersionsAsync();
        await UpdateLoaderVersions();
    }


    private async Task LoadJavaVersionsAsync()
    {
        try
        {
            var combo = this.FindControl<ComboBox>("JavaVersionComboBox");
            if (combo == null) return;

            var manager = new JavaManager();
            var versions = await manager.GetInstalledJavaVersionsAsync();
            
            var items = new List<object>();
            items.Add("Use Global Setting");
            
            foreach (var v in versions) items.Add(v);
            combo.ItemsSource = items;

            if (_existingInstallation != null && !string.IsNullOrEmpty(_existingInstallation.JavaPath))
            {
                var match = versions.FirstOrDefault(v => v.Path == _existingInstallation.JavaPath);
                if (match != null)
                {
                    combo.SelectedItem = match;
                }
                else
                {
                    var custom = new JavaInfo { Path = _existingInstallation.JavaPath, Version = "Custom", MajorVersion = 0, DisplayName = $"Custom ({_existingInstallation.JavaPath})" };
                    items.Add(custom);
                    combo.ItemsSource = items;
                    combo.SelectedItem = custom;
                }
            }
            else
            {
                combo.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[InstallationDialog] Error loading Java versions: {ex}");
        }
    }
    
    private async Task LoadVersionsAsync()
    {
        var versionCombo = this.FindControl<ComboBox>("VersionComboBox");
        if (versionCombo == null) return;

        try
        {
            var manifest = await _minecraftService.GetVersionManifestAsync();
            var versions = manifest.Versions.Select(v => v.Id).ToList();
            versionCombo.ItemsSource = versions;
            
            if (_existingInstallation != null && !string.IsNullOrEmpty(_existingInstallation.Version))
            {
                int index = versions.IndexOf(_existingInstallation.Version);
                versionCombo.SelectedIndex = index >= 0 ? index : 0;
            }
            else
            {
                versionCombo.SelectedIndex = 0;
            }
        }
        catch
        {
            versionCombo.ItemsSource = new[] { "1.21.1", "1.20.1", "1.19.4" };
            versionCombo.SelectedIndex = 0;
        }
    }

    public InstallationDialog(Installation existing) : this()
    {
        _existingInstallation = existing;
        
        var nameBox = this.FindControl<TextBox>("NameTextBox");
        var dirBox = this.FindControl<TextBox>("GameDirectoryTextBox");
        var argsBox = this.FindControl<TextBox>("JavaArgsTextBox");
        var title = this.FindControl<TextBlock>("DialogTitle");
        var button = this.FindControl<Button>("CreateButton");

        if (nameBox != null) nameBox.Text = existing.Name;
        if (dirBox != null) dirBox.Text = existing.GameDirectory;
        if (argsBox != null) argsBox.Text = existing.JavaArgs;
        if (title != null) title.Text = "Edit Installation";
        if (button != null) button.Content = "SAVE CHANGES";
        
        var memorySlider = this.FindControl<Slider>("MemorySlider");
        if (memorySlider != null) memorySlider.Value = existing.MemoryAllocationGb;
        
        var removeBtn = this.FindControl<Button>("RemoveButton");
        if (removeBtn != null) removeBtn.IsVisible = true;

        var loaderCombo = this.FindControl<ComboBox>("ModLoaderComboBox");
        if (loaderCombo != null)
        {
            loaderCombo.SelectedIndex = existing.LoaderType switch
            {
                ModLoaderType.Fabric => 1,
                ModLoaderType.Forge => 2,
                _ => 0
            };
        }
    }

    private void RemoveButton_Click(object? sender, RoutedEventArgs e)
    {
        DeleteRequested = true;
        OverlayService.Close(this);
    }

    private async void CreateButton_Click(object? sender, RoutedEventArgs e)
    {
        var nameBox = this.FindControl<TextBox>("NameTextBox");
        var versionCombo = this.FindControl<ComboBox>("VersionComboBox");
        var dirBox = this.FindControl<TextBox>("GameDirectoryTextBox");
        var argsBox = this.FindControl<TextBox>("JavaArgsTextBox");
        var memorySlider = this.FindControl<Slider>("MemorySlider");

        Result = new Installation
        {
            Id = _existingInstallation?.Id ?? Guid.NewGuid().ToString(),
            Name = string.IsNullOrWhiteSpace(nameBox?.Text) ? "New Installation" : nameBox.Text,
            Version = versionCombo?.SelectedItem?.ToString() ?? "1.21.1",
            BaseVersion = versionCombo?.SelectedItem?.ToString() ?? "1.21.1",
            GameDirectory = dirBox?.Text ?? PlatformManager.GetMinecraftDirectory(),
            JavaArgs = argsBox?.Text ?? "",
            MemoryAllocationGb = memorySlider?.Value ?? 0,
            Icon = "grass_block",
            JavaPath = GetSelectedJavaPath(),
            LoaderType = GetSelectedLoaderType(),
            ModLoaderVersion = GetSelectedLoaderVersion()
        };
        
        if (Result.LoaderType == ModLoaderType.Forge && !string.IsNullOrEmpty(Result.ModLoaderVersion))
        {
             var btn = this.FindControl<Button>("CreateButton");
             if (btn != null)
             {
                 btn.IsEnabled = false;
                 btn.Content = "Installing Forge...";
             }
             
             if (!Directory.Exists(Result.GameDirectory)) Directory.CreateDirectory(Result.GameDirectory);
             
             var versionStr = Result.ModLoaderVersion;
             if (versionStr.Contains("(") && versionStr.Contains(")"))
             {
                 var start = versionStr.IndexOf('(') + 1;
                 var end = versionStr.IndexOf(')');
                 Result.ModLoaderVersion = versionStr.Substring(start, end - start);
             }
             
             await _forgeService.InstallForgeAsync(Result.Version, Result.ModLoaderVersion, Result.GameDirectory);
        }

        OverlayService.Close(this);
    }
    
    private string GetSelectedJavaPath()
    {
        var combo = this.FindControl<ComboBox>("JavaVersionComboBox");
        if (combo?.SelectedItem is JavaInfo info) return info.Path;
        return "";
    }

    private ModLoaderType GetSelectedLoaderType()
    {
        var combo = this.FindControl<ComboBox>("ModLoaderComboBox");
        var item = combo?.SelectedItem as ComboBoxItem;
        var tag = item?.Tag?.ToString();
        
        return tag switch
        {
            "Fabric" => ModLoaderType.Fabric,
            "Forge" => ModLoaderType.Forge,
            _ => ModLoaderType.Vanilla
        };
    }

    private string? GetSelectedLoaderVersion()
    {
        if (GetSelectedLoaderType() == ModLoaderType.Vanilla) return null;
        var combo = this.FindControl<ComboBox>("ModLoaderVersionComboBox");
        return combo?.SelectedItem?.ToString();
    }

    private async void LoaderCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        await UpdateLoaderVersions();
    }

    private async Task UpdateLoaderVersions()
    {
        var loaderCombo = this.FindControl<ComboBox>("ModLoaderComboBox");
        var versionCombo = this.FindControl<ComboBox>("VersionComboBox");
        var loaderVersionPanel = this.FindControl<StackPanel>("ModLoaderVersionPanel");
        var loaderVersionCombo = this.FindControl<ComboBox>("ModLoaderVersionComboBox");

        if (loaderCombo == null || versionCombo == null || loaderVersionPanel == null || loaderVersionCombo == null) return;

        var type = GetSelectedLoaderType();
        loaderVersionPanel.IsVisible = type != ModLoaderType.Vanilla;

        if (type == ModLoaderType.Fabric)
        {
            var gameVersion = versionCombo.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(gameVersion)) return;

            loaderVersionCombo.ItemsSource = new[] { "Loading..." };
            loaderVersionCombo.SelectedIndex = 0;
            loaderVersionCombo.IsEnabled = false;

            try
            {
                var versions = await _fabricService.GetLoaderVersionsAsync(gameVersion);
                var items = versions.Select(v => v.Loader.Version).ToList();
                
                loaderVersionCombo.ItemsSource = items;
                loaderVersionCombo.IsEnabled = true;

                if (_existingInstallation != null && _existingInstallation.LoaderType == ModLoaderType.Fabric && !string.IsNullOrEmpty(_existingInstallation.ModLoaderVersion))
                {
                    var existing = items.FirstOrDefault(v => v == _existingInstallation.ModLoaderVersion);
                    if (existing != null) loaderVersionCombo.SelectedItem = existing;
                    else if (items.Any()) loaderVersionCombo.SelectedIndex = 0;
                }
                else if (items.Any())
                {
                    loaderVersionCombo.SelectedIndex = 0;
                }
            }
            catch
            {
                loaderVersionCombo.ItemsSource = new[] { "Error loading versions" };
            }
        }
        else if (type == ModLoaderType.Forge)
        {
            var gameVersion = versionCombo.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(gameVersion)) return;

            loaderVersionCombo.ItemsSource = new[] { "Loading..." };
            loaderVersionCombo.SelectedIndex = 0;
            loaderVersionCombo.IsEnabled = false;

            try
            {
                var versions = await _forgeService.GetForgeVersionsAsync(gameVersion);
                loaderVersionCombo.ItemsSource = versions;
                loaderVersionCombo.IsEnabled = true;

                if (_existingInstallation != null && _existingInstallation.LoaderType == ModLoaderType.Forge && !string.IsNullOrEmpty(_existingInstallation.ModLoaderVersion))
                {
                    var existing = versions.FirstOrDefault(v => v.ForgeVersionStr == _existingInstallation.ModLoaderVersion);
                    if (existing != null) loaderVersionCombo.SelectedItem = existing;
                    else if (versions.Any()) loaderVersionCombo.SelectedIndex = 0;
                }
                else if (versions.Any())
                {
                     loaderVersionCombo.SelectedIndex = 0;
                }
            }
            catch
            {
                loaderVersionCombo.ItemsSource = new[] { "Error loading versions" };
            }
        }
        
        var modsTab = this.FindControl<TabItem>("ModsTab");
        if (modsTab != null)
        {
             modsTab.IsEnabled = type != ModLoaderType.Vanilla;
             if (modsTab.IsEnabled)
             {
                 var modsControl = this.FindControl<Controls.ModsControl>("ModsControl");
                 if (modsControl != null)
                 {
                      var dirBox = this.FindControl<TextBox>("GameDirectoryTextBox");
                      string gameDir = dirBox?.Text ?? PlatformManager.GetMinecraftDirectory();
                      if (_existingInstallation != null && !string.IsNullOrEmpty(_existingInstallation.GameDirectory))
                      {
                          gameDir = _existingInstallation.GameDirectory;
                      }

                      string selectedVersion = versionCombo?.SelectedItem?.ToString() ?? "";
                      if (string.IsNullOrEmpty(selectedVersion) && versionCombo?.SelectedIndex >= 0)
                      {
                          var items = versionCombo.ItemsSource as System.Collections.IList;
                          if (items != null && versionCombo.SelectedIndex < items.Count)
                              selectedVersion = items[versionCombo.SelectedIndex]?.ToString() ?? "";
                      }
                      
                      var tempInstallation = _existingInstallation ?? new Installation 
                      { 
                          GameDirectory = gameDir,
                          LoaderType = type,
                          BaseVersion = selectedVersion,
                          Version = selectedVersion
                      };
                      
                      ConsoleService.Instance.Log($"[Mods] Passing Installation: {tempInstallation.Name} (Version: {tempInstallation.BaseVersion})");
                      modsControl.Initialize(tempInstallation);
                 }
             }
        }
    }
}
