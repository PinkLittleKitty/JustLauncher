using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using JustLauncher.Services;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace JustLauncher;

public partial class AboutDialog : UserControl
{
    public string Version => AppVersion.Version;
    public string Copyright => AppVersion.Copyright;
    
    public AboutDialog()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        
        var closeBtn = this.FindControl<Button>("CloseButton");
        if (closeBtn != null) closeBtn.Click += (s, e) => OverlayService.Close();

        var githubBtn = this.FindControl<Button>("GitHubButton");
        if (githubBtn != null) githubBtn.Click += (s, e) => OpenUrl("https://github.com/PinkLittleKitty/JustLauncher");

        var websiteBtn = this.FindControl<Button>("WebsiteButton");
        if (websiteBtn != null) websiteBtn.Click += (s, e) => OpenUrl("https://justneki.com");
    }

    private void OpenUrl(string url)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
        }
        catch
        {
            // Silently fail if URL opening is not supported
        }
    }
}
