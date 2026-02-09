using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System.Linq;

namespace JustLauncher;

public partial class AccountsPage : UserControl
{
    public AccountsPage()
    {
        InitializeComponent();
        LoadAccounts();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        var addBtn = this.FindControl<Button>("AddAccountButton");
        if (addBtn != null) addBtn.Click += AddAccountButton_Click;

        var refreshBtn = this.FindControl<Button>("RefreshButton");
        if (refreshBtn != null) refreshBtn.Click += RefreshButton_Click;
    }

    private void LoadAccounts()
    {
        var panel = this.FindControl<ItemsControl>("AccountsPanel");
        var countText = this.FindControl<TextBlock>("AccountCountText");

        var accounts = MockData.GetAccounts();

        if (panel != null) panel.ItemsSource = accounts;
        if (countText != null) countText.Text = $"{accounts.Count} Accounts Connected";
    }

    private void AddAccountButton_Click(object? sender, RoutedEventArgs e)
    {
        // Add account logic
    }

    private void RefreshButton_Click(object? sender, RoutedEventArgs e)
    {
        LoadAccounts();
    }

    private void SetActiveAccount_Click(object? sender, RoutedEventArgs e)
    {
        // Set active account logic
    }

    private void EditAccount_Click(object? sender, RoutedEventArgs e)
    {
        // Edit account logic
    }

    private void DeleteAccount_Click(object? sender, RoutedEventArgs e)
    {
        // Delete account logic
    }
}
