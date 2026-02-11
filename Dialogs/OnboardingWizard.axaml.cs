using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JustLauncher;
using JustLauncher.Services;

namespace JustLauncher.Dialogs
{
    public partial class OnboardingWizard : Window
    {
        private int _currentStep = 1;
        private const int TOTAL_STEPS = 5;
        
        public string? SelectedAccountType { get; private set; }
        public string? AccountUsername { get; private set; }
        public string? JavaPath { get; private set; }
        public string? InstallationName { get; private set; }
        public string? MinecraftVersion { get; private set; }
        public string? ModLoader { get; private set; }
        
        public bool WasCompleted { get; private set; } = false;

        public OnboardingWizard()
        {
            ConsoleService.Instance.Log("[OnboardingWizard] Initializing...");
            try 
            {
                InitializeComponent();
                ShowStep(1);
                ConsoleService.Instance.Log("[OnboardingWizard] Initialization complete.");
            }
            catch (Exception ex)
            {
                ConsoleService.Instance.Log($"[OnboardingWizard] INIT ERROR: {ex.Message}");
                ConsoleService.Instance.Log(ex.StackTrace ?? "");
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            
            var titleBar = this.FindControl<Control>("TitleBar");
            if (titleBar != null) titleBar.PointerPressed += (s, e) => BeginMoveDrag(e);
                    
            var offlineCard = this.FindControl<Button>("OfflineAccountCard");
            if (offlineCard != null) offlineCard.Click += OnAccountTypeSelected;
            
            var elyByCard = this.FindControl<Button>("ElyByAccountCard");
            if (elyByCard != null) elyByCard.Click += OnAccountTypeSelected;

            var msCard = this.FindControl<Button>("MicrosoftAccountCard");
            if (msCard != null) msCard.Click += OnAccountTypeSelected;
            
            var browseJavaBtn = this.FindControl<Button>("BrowseJavaButton");
            if (browseJavaBtn != null) browseJavaBtn.Click += BrowseForJava;
            
            var createInstallBtn = this.FindControl<Button>("CreateInstallationButton");
            if (createInstallBtn != null) createInstallBtn.Click += CreateInstallation;
            
            var startPlayingBtn = this.FindControl<Button>("StartPlayingButton");
            if (startPlayingBtn != null) startPlayingBtn.Click += (s, e) => Complete();
            
            var skipBtn = this.FindControl<Button>("SkipButton");
            if (skipBtn != null) skipBtn.Click += (s, e) => Skip();
            
            var backBtn = this.FindControl<Button>("BackButton");
            if (backBtn != null) backBtn.Click += (s, e) => PreviousStep();
            
            var nextBtn = this.FindControl<Button>("NextButton");
            if (nextBtn != null) nextBtn.Click += (s, e) => NextStep();
            
            _ = DetectJavaAsync();
        }

        private async void OnAccountTypeSelected(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string accountType)
            {
                SelectedAccountType = accountType;
                
                if (accountType == "Offline")
                {
                    var accountsConfig = await ConfigManager.LoadAccountsAsync();
                    var newAccount = new Account
                    {
                        Id = Guid.NewGuid().ToString(),
                        Username = "Player",
                        AccountType = "Offline",
                        IsActive = true
                    };
                    
                    foreach (var acc in accountsConfig.Accounts)
                    {
                        acc.IsActive = false;
                    }
                    
                    accountsConfig.Accounts.Add(newAccount);
                    accountsConfig.SelectedAccountId = newAccount.Id;
                    await ConfigManager.SaveAccountsAsync(accountsConfig);
                    
                    AccountUsername = "Player";
                }
                else if (accountType == "Microsoft")
                {
                    var dialog = new AccountDialog();
                    var account = await OverlayService.ShowDialog<Account>(dialog);
                    if (account != null)
                    {
                        var accountsConfig = await ConfigManager.LoadAccountsAsync();
                        var existing = accountsConfig.Accounts.FirstOrDefault(a => a.Username == account.Username && a.AccountType == account.AccountType);
                        if (existing != null)
                        {
                            existing.AccessToken = account.AccessToken;
                            existing.RefreshToken = account.RefreshToken;
                            existing.ExpiresAt = account.ExpiresAt;
                            account = existing;
                        }
                        else
                        {
                            accountsConfig.Accounts.Add(account);
                        }

                        foreach (var acc in accountsConfig.Accounts) acc.IsActive = (acc.Id == account.Id);
                        accountsConfig.SelectedAccountId = account.Id;
                        await ConfigManager.SaveAccountsAsync(accountsConfig);

                        AccountUsername = account.Username;
                        NextStep();
                    }
                    return;
                }
                
                NextStep();
            }
        }

        private async Task DetectJavaAsync()
        {
            try
            {
                var javaInfo = await PlatformManager.FindJavaInstallationAsync(17);
                
                if (javaInfo.path != null)
                {
                    JavaPath = javaInfo.path;
                    var javaVersionText = this.FindControl<TextBlock>("JavaVersionText");
                    if (javaVersionText != null)
                    {
                        javaVersionText.Text = $"Java {javaInfo.version} ({javaInfo.path})";
                    }
                    
                    var statusIcon = this.FindControl<Projektanker.Icons.Avalonia.Icon>("JavaStatusIcon");
                    if (statusIcon != null)
                    {
                        statusIcon.Value = "fa-solid fa-check-circle";
                        statusIcon.Foreground = (Avalonia.Media.IBrush)this.FindResource("SuccessBrush")!;
                    }

                    var settings = await ConfigManager.LoadSettingsAsync();
                    settings.JavaPath = JavaPath;
                    await ConfigManager.SaveSettingsAsync(settings);
                }
                else
                {
                    var javaVersionText = this.FindControl<TextBlock>("JavaVersionText");
                    if (javaVersionText != null)
                    {
                        javaVersionText.Text = "Java not found. Please install Java 17 or higher.";
                    }
                    
                    var statusIcon = this.FindControl<Projektanker.Icons.Avalonia.Icon>("JavaStatusIcon");
                    if (statusIcon != null)
                    {
                        statusIcon.Value = "fa-solid fa-times-circle";
                        statusIcon.Foreground = (Avalonia.Media.IBrush)this.FindResource("ErrorBrush")!;
                    }
                }
            }
            catch
            {
            }
        }

        private async void BrowseForJava(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Select Java Executable",
                AllowMultiple = false
            });

            if (files != null && files.Count > 0)
            {
                JavaPath = files[0].Path.LocalPath;
                var javaVersionText = this.FindControl<TextBlock>("JavaVersionText");
                if (javaVersionText != null)
                {
                    javaVersionText.Text = $"Selected: {JavaPath}";
                }
            }
        }

        private async void CreateInstallation(object? sender, RoutedEventArgs e)
        {
            var nameBox = this.FindControl<TextBox>("InstallationNameBox");
            InstallationName = nameBox?.Text ?? "My First Installation";
            
            var versionCombo = this.FindControl<ComboBox>("VersionComboBox");
            var selectedVersionItem = versionCombo?.SelectedItem as ComboBoxItem;
            var versionText = selectedVersionItem?.Content?.ToString() ?? "1.21.1";
            
            string version = "1.21.1";
            if (versionText.Contains("1.21.1")) version = "1.21.1";
            else if (versionText.Contains("Snapshot")) version = "24w33a"; // Dummy snapshot
            
            var modLoaderCombo = this.FindControl<ComboBox>("ModLoaderComboBox");
            var selectedModLoaderItem = modLoaderCombo?.SelectedItem as ComboBoxItem;
            ModLoader = selectedModLoaderItem?.Content?.ToString() ?? "None (Vanilla)";
            
            ModLoaderType modLoaderType = ModLoaderType.Vanilla;
            if (ModLoader.Contains("Fabric")) modLoaderType = ModLoaderType.Fabric;
            else if (ModLoader.Contains("Forge")) modLoaderType = ModLoaderType.Forge;
            
            var installationsConfig = await ConfigManager.LoadInstallationsAsync();
            var newInstallation = new Installation
            {
                Name = InstallationName,
                Version = version,
                BaseVersion = version,
                LoaderType = modLoaderType,
                IsModded = modLoaderType != ModLoaderType.Vanilla,
                GameDirectory = Path.Combine(PlatformManager.GetMinecraftDirectory(), "instances", InstallationName.Replace(" ", "_"))
            };
            
            installationsConfig.Installations.Add(newInstallation);
            await ConfigManager.SaveInstallationsAsync(installationsConfig);
            
            NextStep();
        }

        private void ShowStep(int step)
        {
            _currentStep = step;
            
            for (int i = 1; i <= TOTAL_STEPS; i++)
            {
                var stepControl = this.FindControl<Control>($"Step{i}");
                if (stepControl != null) stepControl.IsVisible = false;
            }
            
            var currentStepControl = this.FindControl<Control>($"Step{_currentStep}");
            if (currentStepControl != null) currentStepControl.IsVisible = true;
            
            UpdateProgress();
            
            var backBtn = this.FindControl<Button>("BackButton");
            if (backBtn != null) backBtn.IsVisible = _currentStep > 1 && _currentStep < TOTAL_STEPS;
            
            var skipBtn = this.FindControl<Button>("SkipButton");
            if (skipBtn != null) skipBtn.IsVisible = _currentStep < TOTAL_STEPS;
            
            var nextBtn = this.FindControl<Button>("NextButton");
            if (nextBtn != null) 
            {
                nextBtn.IsVisible = _currentStep < TOTAL_STEPS && _currentStep != 2 && _currentStep != 4;
                
                if (_currentStep == 1)
                {
                    nextBtn.Content = Services.LocalizationService.Instance["Onboarding_GetStarted"];
                }
                else if (_currentStep == 3)
                {
                    nextBtn.Content = Services.LocalizationService.Instance["Onboarding_Continue"];
                }
                else
                {
                    nextBtn.Content = Services.LocalizationService.Instance["Onboarding_Continue"];
                }
            }
        }

        private void UpdateProgress()
        {
            var progressText = this.FindControl<TextBlock>("ProgressText");
            if (progressText != null)
            {
                var format = Services.LocalizationService.Instance["Onboarding_StepCount"];
                progressText.Text = string.Format(format, _currentStep, TOTAL_STEPS);
            }
            
            var progressBar = this.FindControl<ProgressBar>("ProgressBar");
            if (progressBar != null) progressBar.Value = (_currentStep / (double)TOTAL_STEPS) * 100;
        }

        private void NextStep()
        {
            if (_currentStep < TOTAL_STEPS)
            {
                ShowStep(_currentStep + 1);
            }
            else
            {
                Complete();
            }
        }

        private void PreviousStep()
        {
            if (_currentStep > 1)
            {
                ShowStep(_currentStep - 1);
            }
        }

        private async void Skip()
        {
            WasCompleted = false;
            
            var settings = await ConfigManager.LoadSettingsAsync();
            settings.IsFirstRun = false;
            await ConfigManager.SaveSettingsAsync(settings);
            
            Close();
        }

        private async void Complete()
        {
            WasCompleted = true;
            
            var settings = await ConfigManager.LoadSettingsAsync();
            settings.IsFirstRun = false;
            await ConfigManager.SaveSettingsAsync(settings);
            
            Close();
        }
    }
}
