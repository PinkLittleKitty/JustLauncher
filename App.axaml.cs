using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;
using JustLauncher.Converters;
using JustLauncher.Services;
using System;
using System.IO;
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

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[APP] OnFrameworkInitializationCompleted started");
            Console.WriteLine("[APP] OnFrameworkInitializationCompleted started");
            
            IconProvider.Current
                .Register<FontAwesomeIconProvider>();
            
            Console.WriteLine("[APP] Icon provider registered");

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                Console.WriteLine("[APP] Loading settings...");
                var settings = ConfigManager.LoadSettings();
                Console.WriteLine($"[APP] Settings loaded: {settings.Language}, {settings.Theme}");
                
                Services.LocalizationService.Instance.ChangeLanguage(settings.Language);
                Console.WriteLine("[APP] Language changed");
                
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
                Console.WriteLine("[APP] Theme set");

                Console.WriteLine("[APP] Creating MainWindow...");
                desktop.MainWindow = new MainWindow();
                Console.WriteLine("[APP] MainWindow created, showing...");

                if (settings.CheckForUpdatesOnStartup && !settings.IsFirstRun)
                {
                    _ = CheckForUpdatesAsync(desktop.MainWindow);
                }
            }

            Console.WriteLine("[APP] Calling base.OnFrameworkInitializationCompleted()");
            base.OnFrameworkInitializationCompleted();
            Console.WriteLine("[APP] Initialization complete!");
        }
        catch (Exception ex)
        {
            var msg = $"FATAL ERROR during initialization: {ex}";
            Console.WriteLine(msg);
            Console.Error.WriteLine(msg);
            System.Diagnostics.Debug.WriteLine(msg);
            File.WriteAllText("/tmp/justlauncher_crash.log", msg);
            Environment.Exit(1);
        }
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
