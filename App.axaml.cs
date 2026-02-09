using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;
using JustLauncher.Converters;
using JustLauncher.Services;
using System.Threading.Tasks;

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

    public override async void OnFrameworkInitializationCompleted()
    {
        IconProvider.Current
            .Register<FontAwesomeIconProvider>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = await ConfigManager.LoadSettingsAsync();
            
            Services.LocalizationService.Instance.ChangeLanguage(settings.Language);
            
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

            if (settings.CheckForUpdatesOnStartup)
            {
                _ = CheckForUpdatesAsync(desktop.MainWindow);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task CheckForUpdatesAsync(Avalonia.Controls.Window owner)
    {
        var updateService = new UpdateService();
        var updateInfo = await updateService.CheckForUpdatesAsync();

        if (updateInfo != null && updateInfo.IsNewer)
        {
            var dialog = new ChangelogDialog(updateInfo);
            await dialog.ShowDialog(owner);
        }
    }
}
