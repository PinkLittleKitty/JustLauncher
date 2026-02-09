using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using JustLauncher.Services;
using JustLauncher.Models;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace JustLauncher;

public partial class AboutDialog : UserControl
{
    public AboutDialog()
    {
        InitializeComponent();
        UpdateVersionText();
        LocalizationService.Instance.LanguageChanged += (s, e) => UpdateVersionText();
    }

    private void UpdateVersionText()
    {
        var versionText = this.FindControl<TextBlock>("VersionText");
        if (versionText != null)
        {
            var format = LocalizationService.Instance["About_VersionFormat"];
            versionText.Text = string.Format(format, AppVersion.Version);
        }
    }
    
    public string Version => AppVersion.Version;
    public string Copyright => AppVersion.Copyright;

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        
        var closeBtn = this.FindControl<Button>("CloseButton");
        if (closeBtn != null) closeBtn.Click += (s, e) => OverlayService.Close();

        var githubBtn = this.FindControl<Button>("GitHubButton");
        if (githubBtn != null) githubBtn.Click += (s, e) => OpenUrl("https://github.com/PinkLittleKitty/JustLauncher");

        var websiteBtn = this.FindControl<Button>("WebsiteButton");
        if (websiteBtn != null) websiteBtn.Click += (s, e) => OpenUrl("https://justneki.com");

        var changelogBtn = this.FindControl<Button>("ChangelogButton");
        if (changelogBtn != null) changelogBtn.Click += ChangelogButton_Click;
    }

    private async void ChangelogButton_Click(object? sender, RoutedEventArgs e)
    {
        var updateService = new UpdateService();
        var updateInfo = await updateService.CheckForUpdatesAsync(true);
        if (updateInfo != null)
        {
            var dialog = new ChangelogDialog(updateInfo);
            if (MainWindow.Instance != null)
            {
                await dialog.ShowDialog(MainWindow.Instance);
            }
            else
            {
                dialog.Show();
            }
        }
    }

    private void OpenUrl(string url)
    {
        PlatformManager.OpenBrowser(url);
    }
}
