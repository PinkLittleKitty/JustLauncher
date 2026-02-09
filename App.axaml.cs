using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace JustLauncher;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        
        // Register global converters manually to avoid XAML issues
        Resources.Add("AccountTypeToIconConverter", new AccountTypeToIconConverter());
        Resources.Add("AccountBoolToColorConverter", new AccountBoolToColorConverter());
        Resources.Add("AccountActiveStatusConverter", new AccountActiveStatusConverter());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
