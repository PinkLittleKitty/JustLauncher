using System;
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

    public InstallationDialog()
    {
        InitializeComponent();
        var textBox = this.FindControl<TextBox>("GameDirectoryTextBox");
        if (textBox != null) textBox.Text = PlatformManager.GetMinecraftDirectory();
        
        var nameBox = this.FindControl<TextBox>("NameTextBox");
        if (nameBox != null)
        {
            nameBox.TextChanged += (s, e) =>
            {
                 var name = nameBox.Text;
                 var settings = ConfigManager.LoadSettings();
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
                        argsBox.Text = $"-Xmx{memoryGb}G -Xms{memoryGb}G";
                    }
                }
            };
        }

        _ = LoadVersionsAsync();
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
    }

    private void RemoveButton_Click(object? sender, RoutedEventArgs e)
    {
        DeleteRequested = true;
        OverlayService.Close(this);
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
        OverlayService.Close(this);
    }
}
