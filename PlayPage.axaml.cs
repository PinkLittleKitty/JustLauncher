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
        private string installationsConfigPath = string.Empty;
        private string minecraftDirectory = string.Empty;
        private MinecraftService _minecraftService = default!;

        public PlayPage() : this("Player") { }

        public PlayPage(string username)
        {
            InitializeComponent();
            this.username = username;

            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "JustLauncher/1.0");

            minecraftDirectory = PlatformManager.GetMinecraftDirectory();
            Log($"Minecraft directory: {minecraftDirectory}");
            _minecraftService = new MinecraftService(minecraftDirectory);
            _ = CheckJavaAndShowCompatibleVersions();
            LoadInstallations();
            
            Log($"Launcher started for user: {username}");
        }

        private void Log(string message)
        {
            ConsoleService.Instance.Log(message);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            
            var addBtn = this.FindControl<Button>("AddInstallationButton");
            if (addBtn != null) addBtn.Click += AddInstallationButton_Click;

            var panel = this.FindControl<ItemsControl>("InstallationsPanel");
            if (panel != null)
            {
                panel.AddHandler(Button.ClickEvent, (s, e) =>
                {
                    if (e.Source is Button btn)
                    {
                        if (btn.Name == "LaunchButton") LaunchButton_Click(btn, e);
                        else if (btn.Name == "EditInstallationButton") EditInstallation_Click(btn, e);
                    }
                });
            }
        }

        private async void EditInstallation_Click(object? sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var installation = btn?.Tag as Installation;
            if (installation == null) return;

            var dialog = new InstallationDialog(installation);
            var parent = VisualRoot as Window;
            var result = await dialog.ShowDialog<bool>(parent!);

            if (result)
            {
                if (dialog.DeleteRequested)
                {
                    installationsConfig.Installations.RemoveAll(i => i.Id == installation.Id);
                    ConfigManager.SaveInstallations(installationsConfig);
                    RefreshInstallations();
                    return;
                }

                if (dialog.Result != null)
                {
                    var existing = installationsConfig.Installations.FirstOrDefault(i => i.Id == installation.Id);
                    if (existing != null)
                    {
                        existing.Name = dialog.Result.Name;
                        existing.Version = dialog.Result.Version;
                        existing.GameDirectory = dialog.Result.GameDirectory;
                        existing.JavaArgs = dialog.Result.JavaArgs;
                        
                        ConfigManager.SaveInstallations(installationsConfig);
                        RefreshInstallations();
                    }
                }
            }
        }

        private void LoadInstallations()
        {
            installationsConfig = ConfigManager.LoadInstallations();
            RefreshInstallations();
        }

        private void RefreshInstallations()
        {
            var panel = this.FindControl<ItemsControl>("InstallationsPanel");
            if (panel != null)
            {
                panel.ItemsSource = null;
                panel.ItemsSource = installationsConfig.Installations.ToList();
            }
        }

        private async Task CheckJavaAndShowCompatibleVersions()
        {
            try
            {
                string? javaVersion = await PlatformManager.GetJavaVersionAsync();
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var textBlock = this.FindControl<TextBlock>("JavaVersionText");
                    var button = this.FindControl<Button>("DownloadJavaButton");
                    
                    if (textBlock != null && button != null)
                    {
                        if (javaVersion != null)
                        {
                            textBlock.Text = $"Java: {javaVersion}";
                            textBlock.Foreground = Brushes.LightGreen;
                            button.IsVisible = false;
                        }
                        else
                        {
                            textBlock.Text = "Java: Not Found";
                            textBlock.Foreground = Brushes.Red;
                            button.IsVisible = true;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Log($"Error checking Java: {ex.Message}");
                await Dispatcher.UIThread.InvokeAsync(() => {
                    var textBlock = this.FindControl<TextBlock>("JavaVersionText");
                    if (textBlock != null) textBlock.Text = "Java: Error";
                });
            }
        }

        private async void AddInstallationButton_Click(object? sender, RoutedEventArgs e)
        {
            var dialog = new InstallationDialog();
            var parent = VisualRoot as Window;
            var result = await dialog.ShowDialog<bool>(parent!);
            
            if (result)
            {
                if (dialog.Result != null)
                {
                    installationsConfig.Installations.Add(dialog.Result);
                    ConfigManager.SaveInstallations(installationsConfig);
                    RefreshInstallations();
                }
            }
        }


        private async void LaunchButton_Click(object? sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var installation = btn?.Tag as Installation;
            if (installation == null) return;

            var statusText = this.FindControl<TextBlock>("StatusText");
            var progressBar = this.FindControl<ProgressBar>("ProgressBar");
            
            Log($"Preparing to launch {installation.Name} ({installation.Version})");

            try
            {
                if (statusText != null) statusText.Text = $"Fetching version info for {installation.Version}...";
                if (progressBar != null) { progressBar.IsVisible = true; progressBar.IsIndeterminate = true; }

                Log("Fetching version manifest...");
                var manifest = await _minecraftService.GetVersionManifestAsync();
                var ver = manifest.Versions.FirstOrDefault(v => v.Id == installation.Version);
                if (ver == null) throw new Exception("Version not found in manifest");

                Log($"Fetching version details for {ver.Id}...");
                var info = await _minecraftService.GetVersionInfoAsync(ver.Url);

                // Java Version Validation
                int requiredJava = info.JavaVersion?.MajorVersion ?? 17; // Default to 17 for modern MC if not specified
                string? currentJavaStr = await PlatformManager.GetJavaVersionAsync();
                int currentMajor = 0;
                if (currentJavaStr != null)
                {
                    var parts = currentJavaStr.Split('.');
                    if (parts.Length > 0 && int.TryParse(parts[0], out int major)) currentMajor = major;
                    // Handle old version formats like 1.8.x
                    if (currentMajor == 1 && parts.Length > 1 && int.TryParse(parts[1], out int second)) currentMajor = second;
                }

                if (currentMajor < requiredJava)
                {
                    Log($"[WARNING] Java Version Mismatch!");
                    Log($"[WARNING] Required: Java {requiredJava}, Detected: Java {currentMajor}");
                    Log($"[WARNING] The game might fail to start. Please update your Java Runtime.");
                }

                // Download Libraries
                Log("Checking libraries...");
                if (statusText != null) statusText.Text = "Downloading libraries...";
                if (progressBar != null) progressBar.IsIndeterminate = false;
                await _minecraftService.DownloadLibrariesAsync(info, (done, total) => 
                {
                    Dispatcher.UIThread.Post(() => { if (progressBar != null) progressBar.Value = (double)done / total * 100; });
                });

                // Download Client
                string jarPath = Path.Combine(minecraftDirectory, "versions", installation.Version, installation.Version + ".jar");
                if (!File.Exists(jarPath))
                {
                    Log("Downloading client JAR...");
                    if (statusText != null) statusText.Text = "Downloading client jar...";
                    await _minecraftService.DownloadFileAsync(info.Downloads.Client.Url, jarPath, (done, total) => 
                    {
                        Dispatcher.UIThread.Post(() => { if (progressBar != null) progressBar.Value = (double)done / total * 100; });
                    });
                }

                // Download Assets
                Log("Checking assets...");
                if (statusText != null) statusText.Text = "Downloading assets...";
                await _minecraftService.DownloadAssetsAsync(info, (done, total) => 
                {
                    Dispatcher.UIThread.Post(() => { if (progressBar != null) progressBar.Value = (double)done / total * 100; });
                });

                // Extract Natives
                Log("Extracting native libraries...");
                if (statusText != null) statusText.Text = "Extracting natives...";
                await _minecraftService.ExtractNativesAsync(info, installation.Version);

                // Launching
                Log("Building launch command...");
                if (statusText != null) statusText.Text = "Starting game...";
                var settings = ConfigManager.LoadSettings();
                var accountsConfig = ConfigManager.LoadAccounts();
                var account = accountsConfig.Accounts.FirstOrDefault(a => a.IsActive) ?? accountsConfig.Accounts.FirstOrDefault();
                
                if (account == null) throw new Exception("No account selected");

                string args = LaunchCommandBuilder.BuildArguments(installation, account, info, settings);
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = string.IsNullOrEmpty(settings.JavaPath) ? PlatformManager.GetJavaExecutable() : settings.JavaPath,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = installation.GameDirectory
                };

                Log("Launching game process...");
                var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                
                process.OutputDataReceived += (s, ev) => { if (ev.Data != null) Log($"[GAME] {ev.Data}"); };
                process.ErrorDataReceived += (s, ev) => { if (ev.Data != null) Log($"[ERROR] {ev.Data}"); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                
                if (statusText != null) statusText.Text = "Game launched!";
                if (progressBar != null) progressBar.IsVisible = false;
                Log("Game started successfully.");
            }
            catch (Exception ex)
            {
                Log($"CRITICAL ERROR: {ex.Message}");
                if (statusText != null) statusText.Text = $"Error: {ex.Message}";
                if (progressBar != null) progressBar.IsVisible = false;
            }
        }
    }
}
