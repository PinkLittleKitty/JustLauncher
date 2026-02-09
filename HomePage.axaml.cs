using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System.Linq;

namespace JustLauncher;

public partial class HomePage : UserControl
{
    public HomePage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        var loginBtn = this.FindControl<Button>("LoginButton");
        if (loginBtn != null) loginBtn.Click += LoginButton_Click;
    }

    private void LoginButton_Click(object? sender, RoutedEventArgs e)
    {
        string username = this.FindControl<TextBox>("UsernameTextBox")?.Text ?? "Player";
        if (string.IsNullOrWhiteSpace(username)) username = "Player";

        var config = ConfigManager.LoadAccounts();
        var account = config.Accounts.FirstOrDefault(a => a.Username == username);
        if (account == null)
        {
            account = new Account { Username = username, AccountType = "Offline", IsActive = true };
            config.Accounts.Add(account);
        }
        
        foreach (var acc in config.Accounts) acc.IsActive = (acc.Id == account.Id);
        config.SelectedAccountId = account.Id;
        ConfigManager.SaveAccounts(config);

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow is MainWindow mainWindow)
            {
                var content = mainWindow.FindControl<ContentControl>("MainContent");
                if (content != null) content.Content = new PlayPage(username);
            }
        }
    }
}
