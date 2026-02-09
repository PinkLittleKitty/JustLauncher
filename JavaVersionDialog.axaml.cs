using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using JustLauncher.Services;

namespace JustLauncher;

public partial class JavaVersionDialog : UserControl
{
    public JavaVersionDialog()
    {
        InitializeComponent();
    }

    public JavaVersionDialog(string required, string detected) : this()
    {
        var reqText = this.FindControl<TextBlock>("RequiredVersionText");
        var detText = this.FindControl<TextBlock>("DetectedVersionText");
        var msgText = this.FindControl<TextBlock>("MessageText");

        if (reqText != null) reqText.Text = required;
        if (detText != null) detText.Text = detected;
        if (msgText != null) msgText.Text = $"The current Minecraft profile requires Java {required}, but we detected {detected} on your system.";
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        var dlBtn = this.FindControl<Button>("DownloadButton");
        var goBtn = this.FindControl<Button>("LaunchAnywayButton");

        if (dlBtn != null) dlBtn.Click += (s, e) => {
            PlatformManager.OpenBrowser("https://www.oracle.com/java/technologies/downloads/");
            OverlayService.Close(false);
        };

        if (goBtn != null) goBtn.Click += (s, e) => OverlayService.Close(true);
    }
}
