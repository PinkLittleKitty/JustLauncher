using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System.Linq;

namespace JustLauncher;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        var accountsConfig = ConfigManager.LoadAccounts();
        var activeAccount = accountsConfig.Accounts.FirstOrDefault(a => a.IsActive) 
                          ?? accountsConfig.Accounts.FirstOrDefault(a => a.Id == accountsConfig.SelectedAccountId)
                          ?? accountsConfig.Accounts.FirstOrDefault();

        var content = this.FindControl<ContentControl>("MainContent");
        if (content != null)
        {
            if (activeAccount != null)
            {
                content.Content = new PlayPage(activeAccount.Username);
            }
            else
            {
                content.Content = new HomePage();
            }
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        var playBtn = this.FindControl<Button>("PlayButton");
        if (playBtn != null) playBtn.Click += PlayButton_Click;

        var accBtn = this.FindControl<Button>("AccountsButton");
        if (accBtn != null) accBtn.Click += AccountsButton_Click;

        var conBtn = this.FindControl<Button>("ConsoleButton");
        if (conBtn != null) conBtn.Click += ConsoleButton_Click;

        var setBtn = this.FindControl<Button>("SettingsButton");
        if (setBtn != null) setBtn.Click += SettingsButton_Click;

        var infoBtn = this.FindControl<Button>("InfoButton");
        if (infoBtn != null)
        {
            infoBtn.Click += async (s, e) => 
            {
                var dialog = new AboutDialog();
                await dialog.ShowDialog(this);
            };
        }

        var minBtn = this.FindControl<Button>("MinimizeButton");
        if (minBtn != null) minBtn.Click += MinimizeButton_Click;

        var maxBtn = this.FindControl<Button>("MaximizeButton");
        if (maxBtn != null) maxBtn.Click += MaximizeButton_Click;

        var clsBtn = this.FindControl<Button>("CloseButton");
        if (clsBtn != null) clsBtn.Click += CloseButton_Click;

        var titleBar = this.FindControl<Control>("TitleBar");
        if (titleBar != null) titleBar.PointerPressed += TitleBar_PointerPressed;
    }

    private void PlayButton_Click(object? sender, RoutedEventArgs e)
    {
        var accountsConfig = ConfigManager.LoadAccounts();
        var activeAccount = accountsConfig.Accounts.FirstOrDefault(a => a.IsActive) 
                          ?? accountsConfig.Accounts.FirstOrDefault(a => a.Id == accountsConfig.SelectedAccountId)
                          ?? accountsConfig.Accounts.FirstOrDefault();
        
        string username = activeAccount?.Username ?? "Player";
        var content = this.FindControl<ContentControl>("MainContent");
        if (content != null) content.Content = new PlayPage(username);
    }

    private void AccountsButton_Click(object? sender, RoutedEventArgs e)
    {
        var content = this.FindControl<ContentControl>("MainContent");
        if (content != null) content.Content = new AccountsPage();
    }

    private void ConsoleButton_Click(object? sender, RoutedEventArgs e)
    {
        var content = this.FindControl<ContentControl>("MainContent");
        if (content != null) content.Content = new ConsolePage();
    }

    private void SettingsButton_Click(object? sender, RoutedEventArgs e)
    {
        var content = this.FindControl<ContentControl>("MainContent");
        if (content != null) content.Content = new SettingsPage();
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
        }
        else
        {
            BeginMoveDrag(e);
        }
    }

    private void MinimizeButton_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object? sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
}
