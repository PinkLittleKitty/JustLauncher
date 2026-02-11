using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using System.Threading.Tasks;
using JustLauncher.Services;

namespace JustLauncher;

public partial class AccountDialog : UserControl
{
    private string _selectedType = "Offline";
    private int _currentStep = 1;

    private bool _isAuthInProgress = false;
    private System.Threading.CancellationTokenSource? _authCts;

    public AccountDialog()
    {
        InitializeComponent();
        AttachHandlers();
        UpdateStepVisibility();
    }

    private void AttachHandlers()
    {
        var closeBtn = this.FindControl<Button>("CloseButton");
        if (closeBtn != null) closeBtn.Click += (s, e) => { CancelAuth(); OverlayService.Close(); };

        var cancelBtn = this.FindControl<Button>("CancelButton");
        if (cancelBtn != null) cancelBtn.Click += (s, e) => { CancelAuth(); OverlayService.Close(); };

        var backBtn = this.FindControl<Button>("BackButton");
        if (backBtn != null) backBtn.Click += (s, e) => { CancelAuth(); _currentStep = 1; UpdateStepVisibility(); };

        var nextBtn = this.FindControl<Button>("NextButton");
        if (nextBtn != null) nextBtn.Click += NextButton_Click;

        var saveBtn = this.FindControl<Button>("SaveButton");
        if (saveBtn != null) saveBtn.Click += SaveButton_Click;

        var copyBtn = this.FindControl<Button>("CopyCodeButton");
        if (copyBtn != null) copyBtn.Click += async (s, e) => {
            var codeText = this.FindControl<TextBlock>("UserCodeText");
            if (codeText != null && !string.IsNullOrEmpty(codeText.Text))
            {
                await TopLevel.GetTopLevel(this)!.Clipboard!.SetTextAsync(codeText.Text);
            }
        };

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

    private void CancelAuth()
    {
        _authCts?.Cancel();
        _isAuthInProgress = false;
    }

    private async void NextButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_selectedType == "Microsoft")
        {
            _currentStep = 3;
            UpdateStepVisibility();
            await StartMicrosoftAuth();
        }
        else
        {
            _currentStep = 2;
            UpdateStepVisibility();
        }
    }

    private async Task StartMicrosoftAuth()
    {
        _isAuthInProgress = true;
        _authCts = new System.Threading.CancellationTokenSource();
        
        try 
        {
            const int port = 28547;
            var redirectUri = $"http://localhost:{port}/";
            var pkce = MicrosoftAuthService.Instance.GeneratePkcePair();
            var authUrl = MicrosoftAuthService.Instance.GetAuthorizationUrl(pkce.Challenge, redirectUri);
            
            var urlText = this.FindControl<TextBlock>("LoginUrlText");
            var codeText = this.FindControl<TextBlock>("UserCodeText");
            if (urlText != null) urlText.Text = "Waiting for browser login...";
            if (codeText != null) codeText.Text = "Check your browser";

            ConsoleService.Instance.Log($"Microsoft Login: Opening browser for authentication...");
            
            try
            {
                Process.Start(new ProcessStartInfo { FileName = authUrl, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ConsoleService.Instance.Log($"Could not open browser: {ex.Message}");
                NotificationService.Instance.ShowError("Error", "Could not open browser. Please copy the URL from logs.");
                if (urlText != null) urlText.Text = authUrl;
            }

            var codeTask = MicrosoftAuthService.Instance.ListenForCodeAsync(port);
            var resultTask = await Task.WhenAny(codeTask, Task.Delay(-1, _authCts.Token));

            if (resultTask == codeTask)
            {
                var code = await codeTask;
                ConsoleService.Instance.Log("Authorization code received. Exchanging for token...");
                
                var tokenResponse = await MicrosoftAuthService.Instance.ExchangeCodeForTokenAsync(code, pkce.Verifier, redirectUri);
                await FinalizeMicrosoftAuth(tokenResponse);
            }
            else
            {
                return;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ConsoleService.Instance.Log($"Authentication Error: {ex.Message}");
            NotificationService.Instance.ShowError("Authentication Error", ex.Message);
            _currentStep = 1;
            UpdateStepVisibility();
        }
    }

    private async Task FinalizeMicrosoftAuth(MsTokenResponse msToken)
    {
        try
        {
            var xboxResp = await MicrosoftAuthService.Instance.AuthenticateWithXboxAsync(msToken.AccessToken);
            var xstsResp = await MicrosoftAuthService.Instance.AuthenticateWithXstsAsync(xboxResp.Token);
            var mcResp = await MicrosoftAuthService.Instance.AuthenticateWithMinecraftAsync(xstsResp.Token, xboxResp.DisplayClaims.Xui[0].Uhs);
            var profile = await MicrosoftAuthService.Instance.GetMinecraftProfileAsync(mcResp.AccessToken);

            var account = new Account
            {
                Username = profile.Name,
                Id = profile.Id,
                Email = "",
                AccountType = "Microsoft",
                AccessToken = mcResp.AccessToken,
                RefreshToken = msToken.RefreshToken,
                ExpiresAt = DateTime.Now.AddSeconds(msToken.ExpiresIn),
                Xuid = xboxResp.DisplayClaims.Xui[0].Uhs
            };

            _isAuthInProgress = false;
            OverlayService.Close(account);
        }
        catch (Exception ex)
        {
            ConsoleService.Instance.Log($"Minecraft Authentication Failed: {ex.Message}");
            NotificationService.Instance.ShowError("Minecraft Authentication Failed", "Could not verify your Minecraft license: " + ex.Message);
            _currentStep = 1;
            UpdateStepVisibility();
        }
    }

    private void UpdateStepVisibility()
    {
        var s1 = this.FindControl<Control>("Step1Container");
        var s2 = this.FindControl<Control>("Step2Container");
        var s3 = this.FindControl<Control>("Step3Container");
        
        var back = this.FindControl<Control>("BackButton");
        var next = this.FindControl<Control>("NextButton");
        var save = this.FindControl<Control>("SaveButton");
        var title = this.FindControl<TextBlock>("DialogTitle");

        if (s1 != null) s1.IsVisible = _currentStep == 1;
        if (s2 != null) s2.IsVisible = _currentStep == 2;
        if (s3 != null) s3.IsVisible = _currentStep == 3;
        
        if (back != null) back.IsVisible = _currentStep > 1;
        if (next != null) next.IsVisible = _currentStep == 1;
        if (save != null) save.IsVisible = _currentStep == 2;

        if (title != null) title.Text = _currentStep == 1 ? "Add Minecraft Account" : 
                                        _currentStep == 2 ? "Account Details" : "Microsoft Login";
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
