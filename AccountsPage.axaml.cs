using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using System.Linq;

namespace JustLauncher;

public partial class AccountsPage : UserControl
{
    public AccountsPage()
    {
        InitializeComponent();
        LoadAccounts();
    }

    private AccountsConfig _config = new();

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        var addBtn = this.FindControl<Button>("AddAccountButton");
        if (addBtn != null) addBtn.Click += AddAccountButton_Click;

        var refreshBtn = this.FindControl<Button>("RefreshButton");
        if (refreshBtn != null) refreshBtn.Click += RefreshButton_Click;

        var panel = this.FindControl<ItemsControl>("AccountsPanel");
        if (panel != null)
        {
            panel.AddHandler(Button.ClickEvent, (s, e) =>
            {
                var btn = (e.Source as Control)?.FindAncestorOfType<Button>(true);
                if (btn == null) return;

                if (btn.Name == "UseAccountButton") SetActiveAccount(btn.Tag as Account);
                else if (btn.Name == "EditAccountButton") EditAccount(btn.Tag as Account);
                else if (btn.Name == "DeleteAccountButton") DeleteAccount(btn.Tag as Account);
            });
        }
    }

    private void LoadAccounts()
    {
        var panel = this.FindControl<ItemsControl>("AccountsPanel");
        var countText = this.FindControl<TextBlock>("AccountCountText");

        _config = ConfigManager.LoadAccounts();

        foreach (var acc in _config.Accounts)
        {
            acc.IsActive = acc.Id == _config.SelectedAccountId;
        }

        if (panel != null)
        {
            panel.ItemsSource = _config.Accounts.ToList();
        }
        if (countText != null) 
        {
            var localizedTemplate = Services.LocalizationService.Instance["Accounts_Count"];
            countText.Text = string.Format(localizedTemplate, _config.Accounts.Count);
        }
    }

    private async void AddAccountButton_Click(object? sender, RoutedEventArgs e)
    {
        var result = await Services.OverlayService.ShowDialog<Account>(new AccountDialog());
        
        if (result != null)
        {
            _config.Accounts.Add(result);
            if (string.IsNullOrEmpty(_config.SelectedAccountId) || _config.Accounts.Count == 1)
            {
                _config.SelectedAccountId = result.Id;
            }
            ConfigManager.SaveAccounts(_config);
            Controls.FaceTracker.NotifyAccountChanged();
            LoadAccounts();
        }
    }

    private void RefreshButton_Click(object? sender, RoutedEventArgs e) => LoadAccounts();

    private void SetActiveAccount(Account? account)
    {
        if (account == null) return;
        foreach (var acc in _config.Accounts) acc.IsActive = (acc.Id == account.Id);
        _config.SelectedAccountId = account.Id;
        ConfigManager.SaveAccounts(_config);
        Controls.FaceTracker.NotifyAccountChanged();
        LoadAccounts();
    }

    private async void EditAccount(Account? account)
    {
        if (account == null) return;
        var result = await Services.OverlayService.ShowDialog<Account>(new AccountDialog());
        
        if (result != null)
        {
            var existing = _config.Accounts.FirstOrDefault(a => a.Id == account.Id);
            if (existing != null)
            {
                existing.Username = result.Username;
                existing.AccountType = result.AccountType;
                ConfigManager.SaveAccounts(_config);
                LoadAccounts();
            }
        }
    }

    private void DeleteAccount(Account? account)
    {
        if (account == null) return;
        _config.Accounts.RemoveAll(a => a.Id == account.Id);
        if (_config.SelectedAccountId == account.Id) _config.SelectedAccountId = _config.Accounts.FirstOrDefault()?.Id ?? "";
        ConfigManager.SaveAccounts(_config);
        Controls.FaceTracker.NotifyAccountChanged();
        LoadAccounts();
    }
}
