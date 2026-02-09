using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;
using JustLauncher.Converters;

namespace JustLauncher;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        
        Resources.Add("AccountTypeToIconConverter", new AccountTypeToIconConverter());
        Resources.Add("AccountBoolToColorConverter", new AccountBoolToColorConverter());
        Resources.Add("AccountActiveStatusConverter", new AccountActiveStatusConverter());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        IconProvider.Current
            .Register<FontAwesomeIconProvider>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = ConfigManager.LoadSettings();
            switch (settings.Theme)
            {
                case "Light":
                    RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
                    break;
                case "Dark":
                    RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
                    break;
                default:
                    RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Default;
                    break;
            }

            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
