using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace JustLauncher;

public partial class ModsPage : UserControl
{
    public ModsPage() : this(null) { }

    public ModsPage(Installation? installation)
    {
        InitializeComponent();
        
        var modsControl = this.FindControl<Controls.ModsControl>("ModsControl");
        if (modsControl != null)
        {
            modsControl.Initialize(installation);
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
