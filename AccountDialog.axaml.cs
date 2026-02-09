using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using JustLauncher.Services;

namespace JustLauncher;

public partial class AccountDialog : UserControl
{
    private string _selectedType = "Offline";
    private int _currentStep = 1;

    public AccountDialog()
    {
        InitializeComponent();
        AttachHandlers();
        UpdateStepVisibility();
    }

    private void AttachHandlers()
    {
        var closeBtn = this.FindControl<Button>("CloseButton");
        if (closeBtn != null) closeBtn.Click += (s, e) => OverlayService.Close();

        var cancelBtn = this.FindControl<Button>("CancelButton");
        if (cancelBtn != null) cancelBtn.Click += (s, e) => OverlayService.Close();

        var backBtn = this.FindControl<Button>("BackButton");
        if (backBtn != null) backBtn.Click += (s, e) => { _currentStep = 1; UpdateStepVisibility(); };

        var nextBtn = this.FindControl<Button>("NextButton");
        if (nextBtn != null) nextBtn.Click += (s, e) => { _currentStep = 2; UpdateStepVisibility(); };

        var saveBtn = this.FindControl<Button>("SaveButton");
        if (saveBtn != null) saveBtn.Click += SaveButton_Click;

        var listBox = this.FindControl<ListBox>("AuthTypeListBox");
        if (listBox != null)
        {
            listBox.SelectionChanged += (s, e) =>
            {
                if (listBox.SelectedItem is ListBoxItem item && item.Tag is string tag)
                {
                    _selectedType = tag;
                    UpdateStep2UI();
                }
            };
            listBox.SelectedIndex = 0;
        }
    }

    private void UpdateStepVisibility()
    {
        var s1 = this.FindControl<Control>("Step1Container");
        var s2 = this.FindControl<Control>("Step2Container");
        var back = this.FindControl<Control>("BackButton");
        var next = this.FindControl<Control>("NextButton");
        var save = this.FindControl<Control>("SaveButton");
        var title = this.FindControl<TextBlock>("DialogTitle");

        if (s1 != null) s1.IsVisible = _currentStep == 1;
        if (s2 != null) s2.IsVisible = _currentStep == 2;
        if (back != null) back.IsVisible = _currentStep == 2;
        if (next != null) next.IsVisible = _currentStep == 1;
        if (save != null) save.IsVisible = _currentStep == 2;

        if (title != null) title.Text = _currentStep == 1 ? "Add Minecraft Account" : "Account Details";
    }

    private void UpdateStep2UI()
    {
        var label = this.FindControl<TextBlock>("FieldLabel");
        var box = this.FindControl<TextBox>("UsernameTextBox");
        var passContainer = this.FindControl<Control>("PasswordContainer");
        var infoTitle = this.FindControl<TextBlock>("Step2InfoTitle");
        var infoBody = this.FindControl<TextBlock>("Step2InfoBody");
        var icon = this.FindControl<Projektanker.Icons.Avalonia.Icon>("Step2Icon");

        if (label != null) label.Text = _selectedType == "ElyBy" ? "EMAIL / USERNAME" : "USERNAME";
        if (box != null) box.Watermark = _selectedType == "ElyBy" ? "Your Ely.by credentials" : "Your player name";
        if (passContainer != null) passContainer.IsVisible = _selectedType == "ElyBy";

        if (infoTitle != null) infoTitle.Text = _selectedType == "ElyBy" ? "Ely.by Account" : "Offline Account";
        if (infoBody != null) infoBody.Text = _selectedType == "ElyBy" 
            ? "Authenticate with Ely.by to access your skins, capes, and friends in-game." 
            : "Offline accounts work on cracked servers and local network play.";
        
        if (icon != null) {
            icon.Value = _selectedType == "ElyBy" ? "fa-solid fa-globe" : "fa-solid fa-user-slash";
            
            string resourceKey = _selectedType == "ElyBy" ? "AccentBrush" : "SuccessBrush";
            if (this.TryFindResource(resourceKey, out var res) && res is IBrush b)
            {
                icon.Foreground = b;
            }
        }
    }

    private void SaveButton_Click(object? sender, RoutedEventArgs e)
    {
        var box = this.FindControl<TextBox>("UsernameTextBox");
        if (string.IsNullOrWhiteSpace(box?.Text)) return;

        var account = new Account 
        { 
            Username = box.Text, 
            AccountType = _selectedType,
            IsActive = false
        };
        
        OverlayService.Close(account);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
