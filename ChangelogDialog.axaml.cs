using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using JustLauncher.Models;

namespace JustLauncher;

public partial class ChangelogDialog : Window
{
    private UpdateInfo? _updateInfo;

    public ChangelogDialog()
    {
        InitializeComponent();
    }

    public ChangelogDialog(UpdateInfo updateInfo) : this()
    {
        _updateInfo = updateInfo;
        LoadChangelog();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        var closeBtn = this.FindControl<Button>("CloseButton");
        if (closeBtn != null) closeBtn.Click += (s, e) => Close();

        var skipBtn = this.FindControl<Button>("SkipVersionButton");
        if (skipBtn != null) skipBtn.Click += SkipVersionButton_Click;

        var remindBtn = this.FindControl<Button>("RemindLaterButton");
        if (remindBtn != null) remindBtn.Click += (s, e) => Close();

        var downloadBtn = this.FindControl<Button>("DownloadButton");
        if (downloadBtn != null) downloadBtn.Click += DownloadButton_Click;
    }

    private void LoadChangelog()
    {
        if (_updateInfo == null)
        {
            ConsoleService.Instance.Log("[ChangelogDialog] Warning: UpdateInfo is null");
            return;
        }

        var currentVersionText = this.FindControl<TextBlock>("CurrentVersionText");
        var latestVersionText = this.FindControl<TextBlock>("LatestVersionText");
        var changelogText = this.FindControl<TextBlock>("ChangelogText");

        if (currentVersionText != null) currentVersionText.Text = AppVersion.Version;
        if (latestVersionText != null) latestVersionText.Text = _updateInfo.Version;
        if (changelogText != null) changelogText.Text = FormatChangelog(_updateInfo.Changelog);
    }

    private string FormatChangelog(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return Services.LocalizationService.Instance["Update_NoChangelog"];
        }

        var formatted = markdown
            .Replace("### ", "• ")
            .Replace("## ", "\n")
            .Replace("# ", "")
            .Replace("**", "")
            .Replace("- ", "  • ")
            .Trim();

        return formatted;
    }

    private async void SkipVersionButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_updateInfo == null) return;

        var settings = await ConfigManager.LoadSettingsAsync();
        settings.SkippedVersion = _updateInfo.Version;
        await ConfigManager.SaveSettingsAsync(settings);

        Close();
    }

    private void DownloadButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_updateInfo == null) return;

        try
        {
            PlatformManager.OpenBrowser(_updateInfo.HtmlUrl);
        }
        catch
        {
            // Silently fail
        }

        Close();
    }
}
