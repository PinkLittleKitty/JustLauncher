using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace JustLauncher;

public partial class InstallationDialog : Window
{
    public Installation? Result { get; private set; }
    public bool DeleteRequested { get; private set; }
    private readonly MinecraftService _minecraftService = new();
    private Installation? _existingInstallation;

    public InstallationDialog()
    {
        InitializeComponent();
        var textBox = this.FindControl<TextBox>("GameDirectoryTextBox");
        if (textBox != null) textBox.Text = PlatformManager.GetMinecraftDirectory();

        _ = LoadVersionsAsync();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        var closeBtn = this.FindControl<Button>("CloseButton");
        if (closeBtn != null) closeBtn.Click += CloseButton_Click;

        var browseBtn = this.FindControl<Button>("BrowseGameDirectoryButton");
        if (browseBtn != null) browseBtn.Click += BrowseGameDirectory_Click;

        var cancelBtn = this.FindControl<Button>("CancelButton");
        if (cancelBtn != null) cancelBtn.Click += CancelButton_Click;

        var createBtn = this.FindControl<Button>("CreateButton");
        if (createBtn != null) createBtn.Click += CreateButton_Click;

        var removeBtn = this.FindControl<Button>("RemoveButton");
        if (removeBtn != null) removeBtn.Click += RemoveButton_Click;

        var header = this.FindControl<Control>("DialogHeader");
        if (header != null) header.PointerPressed += (s, e) => BeginMoveDrag(e);
    }

    private void MemorySlider_PropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Avalonia.Controls.Primitives.RangeBase.ValueProperty && sender is Slider slider)
        {
            var memoryLabel = this.FindControl<TextBlock>("MemoryLabel");
            if (memoryLabel != null)
            {
                memoryLabel.Text = $"{(int)slider.Value} GB";
            }
            
            // Update JavaArgs to reflect memory allocation
            var argsBox = this.FindControl<TextBox>("JavaArgsTextBox");
            if (argsBox != null)
            {
                int memoryGb = (int)slider.Value;
                argsBox.Text = $"-Xmx{memoryGb}G -Xms{memoryGb}G";
            }
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
            
            // Pre-select existing version if editing
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
        if (button != null) button.Content = "Save Changes";
        
        // Set memory slider value from existing installation
        // Use Dispatcher to ensure UI is fully loaded
        Dispatcher.UIThread.Post(() =>
        {
            var memorySlider = this.FindControl<Slider>("MemorySlider");
            var memoryLabel = this.FindControl<TextBlock>("MemoryLabel");
            if (memorySlider != null)
            {
                memorySlider.Value = existing.MemoryAllocationGb;
                if (memoryLabel != null)
                {
                    memoryLabel.Text = $"{(int)existing.MemoryAllocationGb} GB";
                }
            }
        });
        
        // Note: Version combo will be pre-selected in LoadVersionsAsync
        // Mod loader combo will be handled similarly if it exists

        var removeBtn = this.FindControl<Button>("RemoveButton");
        if (removeBtn != null) removeBtn.IsVisible = true;
    }

    private void RemoveButton_Click(object? sender, RoutedEventArgs e)
    {
        DeleteRequested = true;
        Close(true);
    }

    private void CreateButton_Click(object? sender, RoutedEventArgs e)
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
            GameDirectory = dirBox?.Text ?? PlatformManager.GetMinecraftDirectory(),
            JavaArgs = argsBox?.Text ?? "-Xmx2G",
            MemoryAllocationGb = memorySlider?.Value ?? 4.0,
            Icon = "grass_block"
        };
        Close(true);
    }

    private async void BrowseGameDirectory_Click(object? sender, RoutedEventArgs e)
    {
        var dirBox = this.FindControl<TextBox>("GameDirectoryTextBox");
        var startPath = dirBox?.Text ?? PlatformManager.GetMinecraftDirectory();
        
        Uri folderUri;
        try 
        { 
            if (Path.IsPathRooted(startPath))
            {
                folderUri = new Uri("file://" + startPath.Replace("\\", "/"));
            }
            else
            {
                folderUri = new Uri(startPath, UriKind.RelativeOrAbsolute);
            }
        } 
        catch { folderUri = new Uri("file:///"); }

        var result = await this.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            Title = "Select Game Directory",
            SuggestedStartLocation = await this.StorageProvider.TryGetFolderFromPathAsync(folderUri)
        });

        if (result != null && result.Count > 0)
        {
            if (dirBox != null) dirBox.Text = result[0].Path.LocalPath;
        }
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e) => Close(false);
    private void CloseButton_Click(object? sender, RoutedEventArgs e) => Close(false);
}
