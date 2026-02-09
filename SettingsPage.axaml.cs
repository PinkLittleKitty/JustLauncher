using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;

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

        var memLow = this.FindControl<Button>("MemLowBtn");
        if (memLow != null) memLow.Click += (s, e) => SetMemory(2);
        
        var memMed = this.FindControl<Button>("MemMedBtn");
        if (memMed != null) memMed.Click += (s, e) => SetMemory(4);
        
        var memHigh = this.FindControl<Button>("MemHighBtn");
        if (memHigh != null) memHigh.Click += (s, e) => SetMemory(8);
    }

    private void SetMemory(double gb)
    {
        var slider = this.FindControl<Slider>("MemorySlider");
        if (slider != null) slider.Value = gb;
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
            var neverStr = LocalizationService.Instance["Common_Never"];
            var timeStr = _settings.LastUpdateCheck == DateTime.MinValue 
                ? neverStr 
                : _settings.LastUpdateCheck.ToLocalTime().ToString("g");
            textBlock.Text = string.Format(template, timeStr);
        }
    }

    private async void LoadSettings()
    {
        _settings = await ConfigManager.LoadSettingsAsync();

        LoadJavaVersionsAsync();

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
    
    private async void OnLanguageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo && combo.SelectedItem is LanguageInfo langInfo)
        {
            LocalizationService.Instance.ChangeLanguage(langInfo.Code);
            _settings.Language = langInfo.Code;
            await ConfigManager.SaveSettingsAsync(_settings);
        }
    }

    private async void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        var javaCombo = this.FindControl<ComboBox>("JavaVersionComboBox");
        var slider = this.FindControl<Slider>("MemorySlider");
        var closeToggle = this.FindControl<ToggleSwitch>("CloseOnLaunchToggle");
        var separateDirToggle = this.FindControl<ToggleSwitch>("UseSeparateGameDirToggle");
        var sakiToggle = this.FindControl<ToggleSwitch>("SakiToggle");
        var sakiBox = this.FindControl<TextBox>("SakiSkinTextBox");
        var themeCombo = this.FindControl<ComboBox>("ThemeComboBox");
        var updateToggle = this.FindControl<ToggleSwitch>("CheckUpdatesToggle");

        if (javaCombo?.SelectedItem is JavaInfo info)
        {
            _settings.JavaPath = info.Path;
        }
        else if (javaCombo?.SelectedItem is string s && s == "Auto-detect")
        {
            _settings.JavaPath = "";
        }

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

        await ConfigManager.SaveSettingsAsync(_settings);
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

        Services.NotificationService.Instance.ShowSuccess(
            Services.LocalizationService.Instance["Message_SuccessTitle"],
            Services.LocalizationService.Instance["Message_SettingsSaved"]);
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
            var path = result[0].Path.LocalPath;
            var combo = this.FindControl<ComboBox>("JavaVersionComboBox");
            if (combo != null)
            {
                var items = combo.ItemsSource as List<object>;
                
                var info = new JavaInfo 
                { 
                    Path = path, 
                    Version = "Custom", 
                    MajorVersion = 0, 
                    IsJre = true 
                };
                
                var currentList = new List<object>();
                if (combo.ItemsSource is IEnumerable<object> existing) currentList.AddRange(existing);
                
                currentList.Add(info);
                combo.ItemsSource = currentList;
                combo.SelectedItem = info;
            }
        }
    }

    private async void LoadJavaVersionsAsync()
    {
        try
        {
            var combo = this.FindControl<ComboBox>("JavaVersionComboBox");
            if (combo == null) return;

            var manager = new JavaManager();
            var versions = await manager.GetInstalledJavaVersionsAsync();
            
            var items = new List<object>();
            items.Add("Auto-detect");
            
            foreach (var v in versions) items.Add(v);

            combo.ItemsSource = items;

            if (string.IsNullOrEmpty(_settings.JavaPath))
            {
                combo.SelectedIndex = 0;
            }
            else
            {
                var match = versions.FirstOrDefault(v => v.Path == _settings.JavaPath);
                if (match != null)
                {
                    combo.SelectedItem = match;
                }
                else
                {
                    var custom = new JavaInfo 
                    { 
                        Path = _settings.JavaPath, 
                        Version = "Unknown", 
                        MajorVersion = 0, 
                        DisplayName = $"Custom ({_settings.JavaPath})" 
                    };

                    items.Add(custom);
                    combo.SelectedItem = custom;
                }
            }
        }
        catch (Exception ex)
        {
             Console.WriteLine($"[SettingsPage] Error loading Java versions: {ex}");
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
            
            _settings = await ConfigManager.LoadSettingsAsync();
            UpdateLastCheckedText();

            if (info != null && info.IsNewer)
            {
                ConsoleService.Instance.Log($"[SettingsPage] Showing update dialog for version {info.Version}");
                var topLevel = TopLevel.GetTopLevel(this) as Window;
                if (topLevel != null)
                {
                    var dialog = new ChangelogDialog(info);
                    await dialog.ShowDialog(topLevel);
                }
                else
                {
                    ConsoleService.Instance.Log("[SettingsPage] Could not get top level window for dialog");
                }
            }
            else if (info != null)
            {
                ConsoleService.Instance.Log("[SettingsPage] Already on latest version");
                btn.Content = LocalizationService.Instance["Update_UpToDate"];
                await System.Threading.Tasks.Task.Delay(2000);
            }
            else
            {
                ConsoleService.Instance.Log("[SettingsPage] Update check returned null");
                btn.Content = LocalizationService.Instance["Update_CheckFailed"];
                await System.Threading.Tasks.Task.Delay(2000);
            }
        }
        catch (Exception ex)
        {
            ConsoleService.Instance.Log($"[SettingsPage] Error in update check: {ex.Message}");
            ConsoleService.Instance.Log($"[SettingsPage] Stack trace: {ex.StackTrace}");
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
