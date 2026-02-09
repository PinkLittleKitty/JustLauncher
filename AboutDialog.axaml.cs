using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using JustLauncher.Services;

namespace JustLauncher;

public partial class AboutDialog : UserControl
{
    public AboutDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        
        var closeBtn = this.FindControl<Button>("CloseButton");
        if (closeBtn != null) closeBtn.Click += (s, e) => OverlayService.Close();
    }
}
