using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace JustLauncher;

public partial class JavaVersionDialog : Window
{
    public bool ShouldLaunch { get; private set; } = false;

    public JavaVersionDialog()
    {
        InitializeComponent();
    }

    public JavaVersionDialog(string requiredVersion, string detectedVersion) : this()
    {
        var messageText = this.FindControl<TextBlock>("MessageText");
        var requiredText = this.FindControl<TextBlock>("RequiredVersionText");
        var detectedText = this.FindControl<TextBlock>("DetectedVersionText");

        if (messageText != null)
        {
            messageText.Text = $"The selected Minecraft version requires Java {requiredVersion}, but your system has Java {detectedVersion} installed.";
        }

        if (requiredText != null)
        {
            requiredText.Text = $"Java {requiredVersion}";
        }

        if (detectedText != null)
        {
            detectedText.Text = $"Java {detectedVersion}";
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        var downloadBtn = this.FindControl<Button>("DownloadButton");
        if (downloadBtn != null) downloadBtn.Click += DownloadButton_Click;

        var launchBtn = this.FindControl<Button>("LaunchAnywayButton");
        if (launchBtn != null) launchBtn.Click += LaunchAnywayButton_Click;
    }

    private void DownloadButton_Click(object? sender, RoutedEventArgs e)
    {
        PlatformManager.OpenBrowser("https://adoptium.net/temurin/releases/");
        Close();
    }

    private void LaunchAnywayButton_Click(object? sender, RoutedEventArgs e)
    {
        ShouldLaunch = true;
        Close();
    }
}
