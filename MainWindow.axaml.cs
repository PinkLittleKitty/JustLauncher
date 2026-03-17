using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.Linq;
using System.Threading.Tasks;

using JustLauncher.Services;

namespace JustLauncher;

public partial class MainWindow : Window
{
    public static MainWindow? Instance { get; private set; }
    private string? _currentModGameDir;

    public MainWindow()
    {
        Instance = this;
        InitializeComponent();

        OverlayService.Initialize(
            this.FindControl<Grid>("OverlayLayer")!,
            this.FindControl<Border>("OverlayDimmer")!,
            this.FindControl<ContentControl>("OverlayContentHost")!
        );

        _ = InitializeAsync();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _ = CheckOnboardingAsync();
    }

    private async Task CheckOnboardingAsync()
    {
        try
        {
            ConsoleService.Instance.Log("[Onboarding] Checking first run status...");
            var settings = await ConfigManager.LoadSettingsAsync();
            ConsoleService.Instance.Log($"[Onboarding] IsFirstRun: {settings.IsFirstRun}");
            
            if (settings.IsFirstRun)
            {
                ConsoleService.Instance.Log("[Onboarding] Opening wizard...");
                var wizard = new Dialogs.OnboardingWizard();
                await wizard.ShowDialog(this);
                ConsoleService.Instance.Log("[Onboarding] Wizard closed.");
                
                await InitializeAsync();
            }
        }
        catch (Exception ex)
        {
            ConsoleService.Instance.Log($"[Onboarding] ERROR: {ex.Message}");
            ConsoleService.Instance.Log(ex.StackTrace ?? "");
        }
    }

    private async Task InitializeAsync()
    {
        var accountsConfig = await ConfigManager.LoadAccountsAsync();
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
        
        UpdateSakiVisibility();
    }

    public static void NotifySakiSettingsChanged()
    {
        if (Instance != null)
        {
            Instance.UpdateSakiVisibility();
            var stickman = Instance.FindControl<Controls.SakiStickman>("SakiStickman");
            if (stickman != null) stickman.UpdateSkin();
        }
    }

    public async void UpdateSakiVisibility()
    {
        var settings = await ConfigManager.LoadSettingsAsync();
        var stickman = this.FindControl<Control>("SakiStickman");
        if (stickman != null)
        {
            stickman.IsVisible = settings.IsSakiEnabled;
        }
    }

    public void SetModdedState(bool isModded, string? gameDir)
    {
        var modsBtn = this.FindControl<Button>("ModsButton");
        if (modsBtn != null)
        {
             modsBtn.IsVisible = isModded;
             _currentModGameDir = gameDir;
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

        var modsBtn = this.FindControl<Button>("ModsButton");
        if (modsBtn != null) modsBtn.Click += ModsButton_Click;

        var importMPBtn = this.FindControl<Button>("ImportModpackButton");
        if (importMPBtn != null) importMPBtn.Click += ImportModpackButton_Click;

        var infoBtn = this.FindControl<Button>("InfoButton");
        if (infoBtn != null)
        {
            infoBtn.Click += async (s, e) => 
            {
                await OverlayService.ShowDialog<object>(new AboutDialog());
            };
        }

        var minBtn = this.FindControl<Button>("MinimizeButton");
        if (minBtn != null) minBtn.Click += MinimizeButton_Click;

        var maxBtn = this.FindControl<Button>("MaximizeButton");
        if (maxBtn != null) maxBtn.Click += MaximizeButton_Click;

        var clsBtn = this.FindControl<Button>("CloseButton");
        if (clsBtn != null) clsBtn.Click += CloseButton_Click;

        var toggleBtn = this.FindControl<Button>("ToggleSidebarButton");
        if (toggleBtn != null) toggleBtn.Click += ToggleSidebarButton_Click;

        var titleBar = this.FindControl<Control>("TitleBar");
        if (titleBar != null) titleBar.PointerPressed += TitleBar_PointerPressed;

        UpdateTelemetry();
        SetActiveNavItem("PlayButton");

        var notifyHost = this.FindControl<StackPanel>("NotificationHost");
        if (notifyHost != null)
        {
            NotificationService.Instance.ActiveNotifications.CollectionChanged += (s, e) =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems != null)
                {
                    foreach (Models.NotificationModel model in e.NewItems)
                    {
                        var item = new Controls.NotificationItem();
                        item.Initialize(model);
                        notifyHost.Children.Add(item);
                    }
                }
                else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove && e.OldItems != null)
                {
                    foreach (Models.NotificationModel model in e.OldItems)
                    {
                        var existing = notifyHost.Children.OfType<Controls.NotificationItem>()
                            .FirstOrDefault(i => i.GetType().GetField("_model", 
                                System.Reflection.BindingFlags.NonPublic | 
                                System.Reflection.BindingFlags.Instance)?.GetValue(i) == model);
                        
                        if (existing != null)
                        {
                            notifyHost.Children.Remove(existing);
                        }
                    }
                }
            };
        }
    }

    private async void PlayButton_Click(object? sender, RoutedEventArgs e)
    {
        var accountsConfig = await ConfigManager.LoadAccountsAsync();
        var activeAccount = accountsConfig.Accounts.FirstOrDefault(a => a.IsActive) 
                          ?? accountsConfig.Accounts.FirstOrDefault(a => a.Id == accountsConfig.SelectedAccountId)
                          ?? accountsConfig.Accounts.FirstOrDefault();
        
        string username = activeAccount?.Username ?? "Player";
        var content = this.FindControl<ContentControl>("MainContent");
        if (content != null) content.Content = new PlayPage(username);
        SetActiveNavItem("PlayButton");
    }

    private void AccountsButton_Click(object? sender, RoutedEventArgs e)
    {
        var content = this.FindControl<ContentControl>("MainContent");
        if (content != null) content.Content = new AccountsPage();
        SetActiveNavItem("AccountsButton");
    }

    private void ConsoleButton_Click(object? sender, RoutedEventArgs e)
    {
        var content = this.FindControl<ContentControl>("MainContent");
        if (content != null) content.Content = new ConsolePage();
        SetActiveNavItem("ConsoleButton");
    }

    private void SettingsButton_Click(object? sender, RoutedEventArgs e)
    {
        var content = this.FindControl<ContentControl>("MainContent");
        if (content != null) content.Content = new SettingsPage();
        SetActiveNavItem("SettingsButton");
    }

    private async void ModsButton_Click(object? sender, RoutedEventArgs e)
    {
        var content = this.FindControl<ContentControl>("MainContent");
        var installations = await ConfigManager.LoadInstallationsAsync();
        var selected = installations.Installations.FirstOrDefault(i => i.Id == installations.SelectedInstallationId);
        if (content != null && selected != null) content.Content = new ModsPage(selected);
        SetActiveNavItem("ModsButton");
    }

    private async void ImportModpackButton_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new ImportModpackDialog();
        dialog.OnImportFinished += async (s, installation) =>
        {
            var config = await ConfigManager.LoadInstallationsAsync();
            config.Installations.Add(installation);
            config.SelectedInstallationId = installation.Id;
            await ConfigManager.SaveInstallationsAsync(config);
            
            // Refresh current page if it's PlayPage
            await InitializeAsync();
        };
        await OverlayService.ShowDialog<object>(dialog);
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

    private void ToggleSidebarButton_Click(object? sender, RoutedEventArgs e)
    {
        var sidebarGrid = this.FindControl<Grid>("SidebarGrid");
        if (sidebarGrid != null)
        {
            var border = sidebarGrid.Parent as Border;
            var parentGrid = border?.Parent as Grid;
            var sidebarColumn = parentGrid?.ColumnDefinitions[0];
            if (sidebarColumn != null)
            {
                if (sidebarGrid.Classes.Contains("Collapsed"))
                {
                    sidebarGrid.Classes.Remove("Collapsed");
                    sidebarColumn.Width = new GridLength(200);
                }
                else
                {
                    sidebarGrid.Classes.Add("Collapsed");
                    sidebarColumn.Width = new GridLength(72);
                }
            }
        }
    }

    private async void UpdateTelemetry()
    {
        try
        {
            var javaVersionText = this.FindControl<TextBlock>("JavaVersionText");
            var memoryText = this.FindControl<TextBlock>("MemoryAllocationText");

            string? javaVer = await Task.Run(() => PlatformManager.GetJavaVersionAsync());
            if (javaVersionText != null)
            {
                if (!string.IsNullOrEmpty(javaVer))
                {
                    int major = PlatformManager.ExtractMajorVersion(javaVer);
                    javaVersionText.Text = $"Java {major}";
                }
                else
                {
                    javaVersionText.Text = "Java --";
                }
            }

            var settings = await ConfigManager.LoadSettingsAsync();
            if (memoryText != null)
            {
                memoryText.Text = $"{settings.MemoryAllocationGb:F1} GB";
            }
        }
        catch { }
    }

    private void SetActiveNavItem(string buttonName)
    {
        string[] navButtons = { "PlayButton", "ImportModpackButton", "AccountsButton", "SettingsButton", "ConsoleButton", "ModsButton" };
        foreach (var name in navButtons)
        {
            var btn = this.FindControl<Button>(name);
            if (btn != null)
            {
                if (name == buttonName) btn.Classes.Add("Active");
                else btn.Classes.Remove("Active");
            }
        }
    }
}
