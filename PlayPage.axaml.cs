using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;

namespace JustLauncher
{
    public partial class PlayPage : UserControl
    {
        private readonly string username;
        private readonly HttpClient httpClient;
        private InstallationsConfig installationsConfig = new();
        private string minecraftDirectory = string.Empty;
        private MinecraftService _minecraftService = default!;

        public PlayPage() : this("Player") { }

        public PlayPage(string username)
        {
            InitializeComponent();
            this.username = username;

            var welcomeText = this.FindControl<TextBlock>("WelcomeText");
            if (welcomeText != null) welcomeText.Text = $"Welcome back, {username}!";

            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "JustLauncher/1.0");

            minecraftDirectory = PlatformManager.GetMinecraftDirectory();
            _minecraftService = new MinecraftService(minecraftDirectory);
            
            LoadInstallations();
        }

        private void Log(string message)
        {
            ConsoleService.Instance.Log(message);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            
            var playBtn = this.FindControl<Button>("PlayButton");
            if (playBtn != null) playBtn.Click += PlayButton_Click;

            var manageBtn = this.FindControl<Button>("ManageProfilesButton");
            if (manageBtn != null) manageBtn.Click += ManageProfilesButton_Click;
        }

        private void LoadInstallations()
        {
            installationsConfig = ConfigManager.LoadInstallations();
            RefreshInstallations();
        }

        private void RefreshInstallations()
        {
            var combo = this.FindControl<ComboBox>("ProfileComboBox");
            if (combo != null)
            {
                combo.ItemsSource = null;
                combo.ItemsSource = installationsConfig.Installations.ToList();
                if (installationsConfig.Installations.Count > 0)
                {
                    combo.SelectedIndex = 0;
                }
            }
        }

        private async void ManageProfilesButton_Click(object? sender, RoutedEventArgs e)
        {
            var combo = this.FindControl<ComboBox>("ProfileComboBox");
            var selected = combo?.SelectedItem as Installation;

            var dialog = selected != null ? new InstallationDialog(selected) : new InstallationDialog();
            var result = await Services.OverlayService.ShowDialog<InstallationDialog>(dialog);
            
            if (result != null && result.Result != null)
            {
                if (selected != null)
                {
                    var index = installationsConfig.Installations.FindIndex(i => i.Id == selected.Id);
                    if (index >= 0)
                    {
                        installationsConfig.Installations[index] = result.Result;
                    }
                }
                else
                {
                    installationsConfig.Installations.Add(result.Result);
                }

                ConfigManager.SaveInstallations(installationsConfig);
                RefreshInstallations();
            }
            else if (result != null && result.DeleteRequested && selected != null)
            {
                installationsConfig.Installations.RemoveAll(i => i.Id == selected.Id);
                ConfigManager.SaveInstallations(installationsConfig);
                RefreshInstallations();
            }
        }

        private async void PlayButton_Click(object? sender, RoutedEventArgs e)
        {
            var combo = this.FindControl<ComboBox>("ProfileComboBox");
            var installation = combo?.SelectedItem as Installation;
            
            if (installation == null)
            {
                return;
            }

            await LaunchGame(installation);
        }

        private async Task LaunchGame(Installation installation)
        {
            var statusText = this.FindControl<TextBlock>("StatusText");
            var progressBar = this.FindControl<ProgressBar>("ProgressBar");
            
            Log($"Preparing to launch {installation.Name} ({installation.Version})");

            try
            {
                Dispatcher.UIThread.Post(() => 
                {
                    if (statusText != null) statusText.Text = $"Fetching version info for {installation.Version}...";
                    if (progressBar != null) { progressBar.IsVisible = true; progressBar.IsIndeterminate = true; }
                });

                Log("Fetching version manifest...");
                var manifest = await _minecraftService.GetVersionManifestAsync();
                var ver = manifest.Versions.FirstOrDefault(v => v.Id == installation.Version);
                if (ver == null) throw new Exception("Version not found in manifest");

                Log($"Fetching version details for {ver.Id}...");
                var info = await _minecraftService.GetVersionInfoAsync(ver.Url);

                int requiredJava = info.JavaVersion?.MajorVersion ?? 17;
                var (foundVersion, foundPath) = await PlatformManager.FindJavaInstallationAsync(requiredJava);
                
                if (foundVersion != null && foundPath != null)
                {
                    int foundMajor = PlatformManager.ExtractMajorVersion(foundVersion);
                    if (foundMajor < requiredJava)
                    {
                        bool userApproved = await Dispatcher.UIThread.InvokeAsync(async () => 
                            await Services.OverlayService.ShowDialog<bool>(new JavaVersionDialog(requiredJava.ToString(), foundMajor.ToString())));
                        if (!userApproved) return;
                    }
                    else if (foundPath != PlatformManager.GetJavaExecutableName())
                    {
                         var javaSettings = ConfigManager.LoadSettings();
                         javaSettings.JavaPath = foundPath;
                         ConfigManager.SaveSettings(javaSettings);
                    }
                }
                else
                {
                    bool userApproved = await Dispatcher.UIThread.InvokeAsync(async () => 
                        await Services.OverlayService.ShowDialog<bool>(new JavaVersionDialog(requiredJava.ToString(), "Not Found")));
                    if (!userApproved) return;
                }

                if (statusText != null) 
                {
                    Dispatcher.UIThread.Post(() => 
                    {
                        statusText.Text = "Downloading libraries...";
                        if (progressBar != null) progressBar.IsIndeterminate = false;
                    });
                }
                
                await _minecraftService.DownloadLibrariesAsync(info, (done, total) => 
                {
                    Dispatcher.UIThread.Post(() => { if (progressBar != null) progressBar.Value = (double)done / total * 100; });
                });

                if (statusText != null) 
                {
                    Dispatcher.UIThread.Post(() => statusText.Text = "Downloading version jar...");
                }
                
                string mcDir = PlatformManager.GetMinecraftDirectory();
                string jarPath = Path.Combine(mcDir, "versions", installation.Version, installation.Version + ".jar");
                if (!File.Exists(jarPath))
                {
                    await _minecraftService.DownloadFileAsync(info.Downloads.Client.Url, jarPath, (done, total) => 
                    {
                        Dispatcher.UIThread.Post(() => { if (progressBar != null) progressBar.Value = (double)done / total * 100; });
                    });
                }

                Dispatcher.UIThread.Post(() => { if (statusText != null) statusText.Text = "Downloading assets..."; });
                await _minecraftService.DownloadAssetsAsync(info, (done, total) => 
                {
                    Dispatcher.UIThread.Post(() => { if (progressBar != null) progressBar.Value = (double)done / total * 100; });
                });

                Dispatcher.UIThread.Post(() => { if (statusText != null) statusText.Text = "Extracting natives..."; });
                await _minecraftService.ExtractNativesAsync(info, installation.Version);

                Log("Starting game...");
                var settings = ConfigManager.LoadSettings();
                var accountsConfig = ConfigManager.LoadAccounts();
                var account = accountsConfig.Accounts.FirstOrDefault(a => a.IsActive) ?? accountsConfig.Accounts.FirstOrDefault();
                
                if (account == null) throw new Exception("No account selected");
                Log($"Using account: {account.Username} ({account.AccountType})");

                if (account.AccountType == "ElyBy")
                {
                    Log("Preparing Ely.by authentication...");
                    Dispatcher.UIThread.Post(() => { if (statusText != null) statusText.Text = "Preparing Ely.by authentication..."; });
                    await _minecraftService.EnsureAuthlibInjectorAsync();
                }

                if (!string.IsNullOrEmpty(installation.GameDirectory) && !Directory.Exists(installation.GameDirectory))
                {
                    Log($"Creating game directory: {installation.GameDirectory}");
                    Directory.CreateDirectory(installation.GameDirectory);
                }

                Log("Building launch arguments...");
                string args = LaunchCommandBuilder.BuildArguments(installation, account, info, settings);
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = string.IsNullOrEmpty(settings.JavaPath) ? PlatformManager.GetJavaExecutable() : settings.JavaPath,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = string.IsNullOrEmpty(installation.GameDirectory) ? PlatformManager.GetMinecraftDirectory() : installation.GameDirectory
                };

                Log($"Launching process: {startInfo.FileName}");
                Log($"Arguments (hidden for security): [FILTERED]");
                
                var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                
                process.OutputDataReceived += (s, ev) => { if (ev.Data != null) Log($"[GAME] {ev.Data}"); };
                process.ErrorDataReceived += (s, ev) => { if (ev.Data != null) Log($"[ERROR] {ev.Data}"); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                
                Dispatcher.UIThread.Post(() => 
                {
                    if (statusText != null) statusText.Text = "Game launched!";
                    if (progressBar != null) progressBar.IsVisible = false;
                });
                Log("Game started successfully.");
            }
            catch (Exception ex)
            {
                Log($"CRITICAL ERROR: {ex.Message}");
                Dispatcher.UIThread.Post(() => 
                {
                    if (statusText != null) statusText.Text = $"Error: {ex.Message}";
                    if (progressBar != null) progressBar.IsVisible = false;
                });
            }
        }
    }
}
