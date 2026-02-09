using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using JustLauncher.Services;
using System;

namespace JustLauncher;

public partial class AccountDialog : UserControl
{
    public AccountDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        
        var uTxt = this.FindControl<TextBox>("UsernameTextBox");
        var tCmb = this.FindControl<ComboBox>("AccountTypeComboBox");
        var saveBtn = this.FindControl<Button>("SaveButton");
        var cancelBtn = this.FindControl<Button>("CancelButton");
        var closeBtn = this.FindControl<Button>("CloseButton");

        if (saveBtn != null) saveBtn.Click += (s, e) => {
            var username = uTxt?.Text;
            if (string.IsNullOrWhiteSpace(username)) return;

            var type = (tCmb?.SelectedItem as ComboBoxItem)?.Tag as string ?? "Offline";
            
            var account = new Account 
            { 
                Id = Guid.NewGuid().ToString(),
                Username = username, 
                AccountType = type,
                IsActive = false
            };
            
            OverlayService.Close(account);
        };

        if (cancelBtn != null) cancelBtn.Click += (s, e) => OverlayService.Close();
        if (closeBtn != null) closeBtn.Click += (s, e) => OverlayService.Close();
    }
}
