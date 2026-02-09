using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;

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

    private void MemorySlider_PropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Avalonia.Controls.Primitives.RangeBase.ValueProperty && sender is Slider slider)
        {
            _settings.MemoryAllocationGb = slider.Value;
            ConfigManager.SaveSettings(_settings);
            
            var valueText = this.FindControl<TextBlock>("MemoryValueText");
            if (valueText != null)
            {
                valueText.Text = $"{(int)slider.Value} GB";
            }
        }
    }

    private void MemorySlider_ValueChanged(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
    {
        if (sender is Slider slider)
        {
            _settings.MemoryAllocationGb = slider.Value;
            ConfigManager.SaveSettings(_settings);
            
            var valueText = this.FindControl<TextBlock>("MemoryValueText");
            if (valueText != null)
            {
                valueText.Text = $"{(int)slider.Value} GB";
            }
        }
    }

    private void LoadSettings()
    {
        _settings = ConfigManager.LoadSettings();

        var pathBox = this.FindControl<TextBox>("JavaPathTextBox");
        if (pathBox != null) pathBox.Text = _settings.JavaPath;

        var separateDirBox = this.FindControl<CheckBox>("UseSeparateGameDirCheckBox");
        if (separateDirBox != null) separateDirBox.IsChecked = _settings.UseSeparateGameDir;

        var slider = this.FindControl<Slider>("MemorySlider");
        if (slider != null) slider.Value = _settings.MemoryAllocationGb;

        var valueText = this.FindControl<TextBlock>("MemoryValueText");
        if (valueText != null) valueText.Text = $"{(int)_settings.MemoryAllocationGb} GB";

        var closeCheck = this.FindControl<CheckBox>("CloseOnLaunchCheckBox");
        if (closeCheck != null) closeCheck.IsChecked = _settings.CloseOnLaunch;
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        var pathBox = this.FindControl<TextBox>("JavaPathTextBox");
        var slider = this.FindControl<Slider>("MemorySlider");
        var closeCheck = this.FindControl<CheckBox>("CloseOnLaunchCheckBox");
        var separateDirBox = this.FindControl<CheckBox>("UseSeparateGameDirCheckBox");

        _settings.JavaPath = pathBox?.Text ?? "";
        _settings.MemoryAllocationGb = slider?.Value ?? 2.0;
        _settings.CloseOnLaunch = closeCheck?.IsChecked ?? false;
        _settings.UseSeparateGameDir = separateDirBox?.IsChecked ?? false;

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
