using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace JustLauncher;

public partial class SettingsPage : UserControl
{
    private LauncherSettings _settings = new();

    public SettingsPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        var saveBtn = this.FindControl<Button>("SaveButton");
        if (saveBtn != null) saveBtn.Click += SaveButton_Click;

        var browseBtn = this.FindControl<Button>("BrowseJavaButton");
        if (browseBtn != null) browseBtn.Click += BrowseJavaButton_Click;
    }

    private void LoadSettings()
    {
        _settings = ConfigManager.LoadSettings();

        var pathBox = this.FindControl<TextBox>("JavaPathTextBox");
        if (pathBox != null) pathBox.Text = _settings.JavaPath;

        var slider = this.FindControl<Slider>("MemorySlider");
        if (slider != null) slider.Value = _settings.MemoryAllocationGb;

        var closeCheck = this.FindControl<CheckBox>("CloseOnLaunchCheckBox");
        if (closeCheck != null) closeCheck.IsChecked = _settings.CloseOnLaunch;
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        var pathBox = this.FindControl<TextBox>("JavaPathTextBox");
        var slider = this.FindControl<Slider>("MemorySlider");
        var closeCheck = this.FindControl<CheckBox>("CloseOnLaunchCheckBox");

        _settings.JavaPath = pathBox?.Text ?? "";
        _settings.MemoryAllocationGb = slider?.Value ?? 2.0;
        _settings.CloseOnLaunch = closeCheck?.IsChecked ?? false;

        ConfigManager.SaveSettings(_settings);
    }

    private async void BrowseJavaButton_Click(object? sender, RoutedEventArgs e)
    {
        var result = await (VisualRoot as Window)!.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Select Java Executable",
            AllowMultiple = false
        });

        if (result != null && result.Count > 0)
        {
            var pathBox = this.FindControl<TextBox>("JavaPathTextBox");
            if (pathBox != null) pathBox.Text = result[0].Path.LocalPath;
        }
    }
}
