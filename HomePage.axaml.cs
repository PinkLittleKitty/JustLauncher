using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.Linq;

namespace JustLauncher;

public partial class HomePage : UserControl
{
    private string _currentType = "Offline";

    public HomePage()
    {
        InitializeComponent();
        AttachHandlers();
    }

    private void AttachHandlers()
    {
        var offlineBtn = this.FindControl<Button>("OfflineCard");
        if (offlineBtn != null) offlineBtn.Click += (s, e) => ShowStep2("Offline");

        var elyByBtn = this.FindControl<Button>("ElyByCard");
        if (elyByBtn != null) elyByBtn.Click += (s, e) => ShowStep2("ElyBy");

        var backBtn = this.FindControl<Control>("Step2BackBtn");
        if (backBtn != null) backBtn.PointerPressed += (s, e) => {
            var overlay = this.FindControl<Border>("Step2Overlay");
            if (overlay != null) overlay.IsVisible = false;
        };

        var finalBtn = this.FindControl<Button>("FinalLoginBtn");
        if (finalBtn != null) finalBtn.Click += LoginButton_Click;
    }

    private void ShowStep2(string type)
    {
        _currentType = type;
        
        var offlineBtn = this.FindControl<Button>("OfflineCard");
        var elyByBtn = this.FindControl<Button>("ElyByCard");
        
        if (offlineBtn != null) 
        {
            if (type == "Offline") offlineBtn.Classes.Add("Selected");
            else offlineBtn.Classes.Remove("Selected");
        }
        
        if (elyByBtn != null)
        {
            if (type == "ElyBy") elyByBtn.Classes.Add("Selected");
            else elyByBtn.Classes.Remove("Selected");
        }

        var overlay = this.FindControl<Border>("Step2Overlay");
        var header = this.FindControl<TextBlock>("Step2Header");
        var subheader = this.FindControl<TextBlock>("Step2Subheader");
        var label = this.FindControl<TextBlock>("FieldLabel");
        var box = this.FindControl<TextBox>("UsernameTextBox");
        var passContainer = this.FindControl<Control>("PasswordContainer");

        if (overlay != null) overlay.IsVisible = true;
        if (header != null) header.Text = type == "ElyBy" ? "Ely.by Login" : "Offline Login";
        if (subheader != null) subheader.Text = type == "ElyBy" 
            ? "Sync your skins and friends" 
            : "No authentication needed";
        
        if (label != null) label.Text = type == "ElyBy" ? "EMAIL / USERNAME" : "USERNAME";
        if (box != null) {
            box.Watermark = type == "ElyBy" ? "Your Ely.by credentials" : "Your player name";
            box.Text = "";
            box.Focus();
        }
        if (passContainer != null) passContainer.IsVisible = type == "ElyBy";
    }

    private async void LoginButton_Click(object? sender, RoutedEventArgs e)
    {
        var box = this.FindControl<TextBox>("UsernameTextBox");
        string username = box?.Text ?? "";
        
        if (string.IsNullOrWhiteSpace(username)) return;

        var config = await ConfigManager.LoadAccountsAsync();
        
        var account = config.Accounts.FirstOrDefault(a => a.Username == username && a.AccountType == _currentType);
        
        if (account == null)
        {
            account = new Account 
            { 
                Username = username, 
                AccountType = _currentType, 
                IsActive = true 
            };
            config.Accounts.Add(account);
        }
        
        foreach (var acc in config.Accounts) acc.IsActive = (acc.Id == account.Id);
        config.SelectedAccountId = account.Id;
        
        await ConfigManager.SaveAccountsAsync(config);

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow is MainWindow mainWindow)
            {
                var content = mainWindow.FindControl<ContentControl>("MainContent");
                if (content != null) content.Content = new PlayPage(username);
            }
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
