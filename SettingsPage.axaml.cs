using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.Linq;
using Avalonia.Platform.Storage;
using JustLauncher.Services;
using JustLauncher.Models;
using Avalonia.Threading;

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

        var slider = this.FindControl<Slider>("MemorySlider");
        if (slider != null)
        {
            slider.PropertyChanged += MemorySlider_PropertyChanged;
        }

        var checkUpdatesBtn = this.FindControl<Button>("CheckUpdatesButton");
        if (checkUpdatesBtn != null) checkUpdatesBtn.Click += CheckUpdatesButton_Click;
    }

    private void MemorySlider_PropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Avalonia.Controls.Primitives.RangeBase.ValueProperty && sender is Slider slider)
        {
            UpdateMemoryText(slider.Value);
        }
    }

    private void UpdateMemoryText(double value)
    {
        var valueText = this.FindControl<TextBlock>("MemoryValueText");
        if (valueText != null)
        {
            valueText.Text = $"{(int)value} GB";
        }
    }

    private void UpdateLastCheckedText()
    {
        var textBlock = this.FindControl<TextBlock>("LastCheckedText");
        if (textBlock != null)
        {
            var template = LocalizationService.Instance["Update_LastChecked"];
            var timeStr = _settings.LastUpdateCheck == DateTime.MinValue 
                ? "Never" 
                : _settings.LastUpdateCheck.ToLocalTime().ToString("g");
            textBlock.Text = string.Format(template, timeStr);
        }
    }

    private void LoadSettings()
    {
        _settings = ConfigManager.LoadSettings();

        var pathBox = this.FindControl<TextBox>("JavaPathTextBox");
        if (pathBox != null) pathBox.Text = _settings.JavaPath;

        var separateDirToggle = this.FindControl<ToggleSwitch>("UseSeparateGameDirToggle");
        if (separateDirToggle != null) separateDirToggle.IsChecked = _settings.UseSeparateGameDir;

        var slider = this.FindControl<Slider>("MemorySlider");
        if (slider != null) slider.Value = _settings.MemoryAllocationGb;

        UpdateMemoryText(_settings.MemoryAllocationGb);

        var closeToggle = this.FindControl<ToggleSwitch>("CloseOnLaunchToggle");
        if (closeToggle != null) closeToggle.IsChecked = _settings.CloseOnLaunch;

        var sakiToggle = this.FindControl<ToggleSwitch>("SakiToggle");
        if (sakiToggle != null) sakiToggle.IsChecked = _settings.IsSakiEnabled;

        var sakiBox = this.FindControl<TextBox>("SakiSkinTextBox");
        if (sakiBox != null) sakiBox.Text = _settings.SakiSkin;
        
        var themeCombo = this.FindControl<ComboBox>("ThemeComboBox");
        if (themeCombo != null)
        {
            switch (_settings.Theme)
            {
                case "Light": themeCombo.SelectedIndex = 1; break;
                case "Dark": themeCombo.SelectedIndex = 2; break;
                default: themeCombo.SelectedIndex = 0; break;
            }
        }
        
        var langCombo = this.FindControl<ComboBox>("LanguageComboBox");
        if (langCombo != null)
        {
            langCombo.ItemsSource = LocalizationService.Instance.AvailableLanguages;
            langCombo.DisplayMemberBinding = new Avalonia.Data.Binding("DisplayName");
            
            var currentLang = LocalizationService.Instance.AvailableLanguages
                .FirstOrDefault(l => l.Code == _settings.Language);
            if (currentLang != null)
            {
                langCombo.SelectedItem = currentLang;
            }
        }

        var updateToggle = this.FindControl<ToggleSwitch>("CheckUpdatesToggle");
        if (updateToggle != null) updateToggle.IsChecked = _settings.CheckForUpdatesOnStartup;

        UpdateLastCheckedText();
    }
    
    private void OnLanguageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo && combo.SelectedItem is LanguageInfo langInfo)
        {
            LocalizationService.Instance.ChangeLanguage(langInfo.Code);
            _settings.Language = langInfo.Code;
            ConfigManager.SaveSettings(_settings);
        }
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        var pathBox = this.FindControl<TextBox>("JavaPathTextBox");
        var slider = this.FindControl<Slider>("MemorySlider");
        var closeToggle = this.FindControl<ToggleSwitch>("CloseOnLaunchToggle");
        var separateDirToggle = this.FindControl<ToggleSwitch>("UseSeparateGameDirToggle");
        var sakiToggle = this.FindControl<ToggleSwitch>("SakiToggle");
        var sakiBox = this.FindControl<TextBox>("SakiSkinTextBox");
        var themeCombo = this.FindControl<ComboBox>("ThemeComboBox");
        var updateToggle = this.FindControl<ToggleSwitch>("CheckUpdatesToggle");

        _settings.JavaPath = pathBox?.Text ?? "";
        _settings.MemoryAllocationGb = slider?.Value ?? 2.0;
        _settings.CloseOnLaunch = closeToggle?.IsChecked ?? false;
        _settings.UseSeparateGameDir = separateDirToggle?.IsChecked ?? false;
        _settings.IsSakiEnabled = sakiToggle?.IsChecked ?? false;
        _settings.SakiSkin = sakiBox?.Text ?? "Steve";
        _settings.CheckForUpdatesOnStartup = updateToggle?.IsChecked ?? true;
        
        if (themeCombo != null)
        {
            switch (themeCombo.SelectedIndex)
            {
                case 1: _settings.Theme = "Light"; break;
                case 2: _settings.Theme = "Dark"; break;
                default: _settings.Theme = "System"; break;
            }
        }

        ConfigManager.SaveSettings(_settings);
        MainWindow.NotifySakiSettingsChanged();
        
        if (Avalonia.Application.Current != null)
        {
            switch (_settings.Theme)
            {
                case "Light":
                    Avalonia.Application.Current.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
                    break;
                case "Dark":
                    Avalonia.Application.Current.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
                    break;
                default:
                    Avalonia.Application.Current.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Default;
                    break;
            }
        }
    }

    private async void BrowseJavaButton_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var result = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
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

    private async void CheckUpdatesButton_Click(object? sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        if (btn == null) return;

        var originalContent = btn.Content;
        btn.IsEnabled = false;
        btn.Content = LocalizationService.Instance["Update_Checking"];

        try
        {
            var service = new UpdateService();
            var info = await service.CheckForUpdatesAsync(true);
            
            _settings = ConfigManager.LoadSettings();
            UpdateLastCheckedText();

            if (info != null && info.IsNewer)
            {
                var topLevel = TopLevel.GetTopLevel(this) as Window;
                if (topLevel != null)
                {
                    var dialog = new ChangelogDialog(info);
                    await dialog.ShowDialog(topLevel);
                }
            }
            else
            {
                btn.Content = LocalizationService.Instance["Update_UpToDate"];
                await System.Threading.Tasks.Task.Delay(2000);
            }
        }
        catch
        {
            btn.Content = LocalizationService.Instance["Update_CheckFailed"];
            await System.Threading.Tasks.Task.Delay(2000);
        }
        finally
        {
            btn.Content = LocalizationService.Instance["Update_CheckNow"];
            btn.IsEnabled = true;
        }
    }
}
