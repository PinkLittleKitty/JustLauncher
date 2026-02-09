using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace JustLauncher;

public partial class AccountDialog : Window
{
    public Account? Result { get; private set; }

    public AccountDialog()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        
        // Manual event attachment to bypass XAML compiler issues
        var closeBtn = this.FindControl<Button>("CloseButton");
        if (closeBtn != null) closeBtn.Click += CloseButton_Click;

        var cancelBtn = this.FindControl<Button>("CancelButton");
        if (cancelBtn != null) cancelBtn.Click += CancelButton_Click;

        var saveBtn = this.FindControl<Button>("SaveButton");
        if (saveBtn != null) saveBtn.Click += SaveButton_Click;

        var combo = this.FindControl<ComboBox>("AccountTypeComboBox");
        if (combo != null) combo.SelectionChanged += AccountTypeComboBox_SelectionChanged;

        var header = this.FindControl<Control>("DialogHeader");
        if (header != null) header.PointerPressed += (s, e) => BeginMoveDrag(e);
    }

    private void AccountTypeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var combo = this.FindControl<ComboBox>("AccountTypeComboBox");
        var title = this.FindControl<TextBlock>("InfoTitle");
        var text = this.FindControl<TextBlock>("InfoText");

        if (combo == null || title == null || text == null) return;

        var selectedItem = combo.SelectedItem as ComboBoxItem;
        string tag = selectedItem?.Tag?.ToString() ?? "Offline";

        if (tag == "Offline")
        {
            title.Text = "Offline Account";
            title.Foreground = Avalonia.Media.Brushes.Green;
            text.Text = "Offline accounts allow you to play Minecraft without authentication. You can use any username you want.";
        }
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        string username = this.FindControl<TextBox>("UsernameTextBox")?.Text ?? "Player";
        var selectedItem = this.FindControl<ComboBox>("AccountTypeComboBox")?.SelectedItem as ComboBoxItem;
        string type = selectedItem?.Tag?.ToString() ?? "Offline";

        Result = new Account
        {
            Username = username,
            AccountType = type,
            IsActive = true
        };
        
        Close(Result);
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e) => Close();
    private void CloseButton_Click(object? sender, RoutedEventArgs e) => Close();
}
