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
        private InstallationsConfig installationsConfig = new();
        private string minecraftDirectory = string.Empty;
        private MinecraftService _minecraftService = default!;
        private Services.JavaManager _javaManager = new();
        private Services.FabricService _fabricService = new();
        private Services.ForgeService _forgeService = new();
        public string AppVersionText => AppVersion.Version;

        public PlayPage() : this("Player") { }

        public PlayPage(string username)
        {
            InitializeComponent();
            this.username = username;

            var welcomeText = this.FindControl<TextBlock>("WelcomeText");
            if (welcomeText != null) 
            {
                var localizedTemplate = Services.LocalizationService.Instance["Play_WelcomeBack"];
                welcomeText.Text = string.Format(localizedTemplate, username);
            }



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

            var addBtn = this.FindControl<Button>("AddProfileButton");
            if (addBtn != null) addBtn.Click += AddProfileButton_Click;

            var importBtn = this.FindControl<Button>("ImportModpackButton");
            if (importBtn != null) importBtn.Click += ImportModpackButton_Click;
        }

        private async void ImportModpackButton_Click(object? sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Import Modpack",
                AllowMultiple = false,
                Filters = new List<FileDialogFilter>
                {
                    new FileDialogFilter { Name = "Modpacks", Extensions = { "mrpack", "zip" } }
                }
            };

            var window = MainWindow.Instance;
            if (window == null) return;

            string[]? result = await dialog.ShowAsync(window);
            if (result != null && result.Length > 0)
            {
                string path = result[0];
                await Task.Run(async () => {
                    var modpackService = new Services.ModpackService(minecraftDirectory);
                    
                    Dispatcher.UIThread.Post(() => {
                        var statusText = this.FindControl<TextBlock>("StatusText");
                        var progressBar = this.FindControl<ProgressBar>("ProgressBar");
                        if (statusText != null) statusText.Text = "Importing modpack...";
                        if (progressBar != null)
                        {
                            progressBar.IsVisible = true;
                            progressBar.IsIndeterminate = false;
                            progressBar.Value = 0;
                        }
                    });

                    var installation = await modpackService.ImportModpackAsync(path, (msg, prog) => {
                        Dispatcher.UIThread.Post(() => {
                            var statusText = this.FindControl<TextBlock>("StatusText");
                            var progressBar = this.FindControl<ProgressBar>("ProgressBar");
                            if (statusText != null) statusText.Text = msg;
                            if (progressBar != null) progressBar.Value = prog;
                        });
                    });

                    if (installation != null)
                    {
                        installationsConfig.Installations.Add(installation);
                        installationsConfig.SelectedInstallationId = installation.Id;
                        await ConfigManager.SaveInstallationsAsync(installationsConfig);

                        Dispatcher.UIThread.Post(() => {
                            var statusText = this.FindControl<TextBlock>("StatusText");
                            var progressBar = this.FindControl<ProgressBar>("ProgressBar");
                            if (statusText != null) statusText.Text = "Import successful!";
                            if (progressBar != null) progressBar.IsVisible = false;
                            RefreshInstallations();
                        });
                    }
                    else
                    {
                        Dispatcher.UIThread.Post(() => {
                            var statusText = this.FindControl<TextBlock>("StatusText");
                            var progressBar = this.FindControl<ProgressBar>("ProgressBar");
                            if (statusText != null) statusText.Text = "Import failed.";
                            if (progressBar != null) progressBar.IsVisible = false;
                        });
                    }
                });
            }
        }

        private async void LoadInstallations()
        {
            installationsConfig = await ConfigManager.LoadInstallationsAsync();
            RefreshInstallations();
        }

        private void RefreshInstallations()
        {
            var combo = this.FindControl<ComboBox>("ProfileComboBox");
            if (combo != null)
            {
                combo.SelectionChanged += ProfileComboBox_SelectionChanged;
                combo.ItemsSource = null;
                combo.ItemsSource = installationsConfig.Installations.ToList();
                if (installationsConfig.Installations.Count > 0)
                {
                    var selected = installationsConfig.Installations.FirstOrDefault(i => i.Id == installationsConfig.SelectedInstallationId) 
                                   ?? installationsConfig.Installations[0];
                    combo.SelectedItem = selected;
                    UpdateMainWindowState(selected);
                }
            }
        }

        private void UpdateMainWindowState(Installation installation)
        {
            if (MainWindow.Instance != null)
            {
                bool isModded = installation.LoaderType != ModLoaderType.Vanilla;
                string gameDir = !string.IsNullOrEmpty(installation.GameDirectory) 
                               ? installation.GameDirectory 
                               : PlatformManager.GetMinecraftDirectory();
                
                MainWindow.Instance.SetModdedState(isModded, gameDir);
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

                await ConfigManager.SaveInstallationsAsync(installationsConfig);
                RefreshInstallations();
            }
            else if (result != null && result.DeleteRequested && selected != null)
            {
                installationsConfig.Installations.RemoveAll(i => i.Id == selected.Id);
                await ConfigManager.SaveInstallationsAsync(installationsConfig);
                RefreshInstallations();
            }
        }

        private async void ProfileComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox combo && combo.SelectedItem is Installation installation)
            {
                var config = await ConfigManager.LoadInstallationsAsync();
                config.SelectedInstallationId = installation.Id;
                await ConfigManager.SaveInstallationsAsync(config);
                
                UpdateMainWindowState(installation);
            }
        }

        private async void AddProfileButton_Click(object? sender, RoutedEventArgs e)
        {
            var dialog = new InstallationDialog();
            var result = await Services.OverlayService.ShowDialog<InstallationDialog>(dialog);
            
            if (result != null && result.Result != null)
            {
                installationsConfig.Installations.Add(result.Result);
                await ConfigManager.SaveInstallationsAsync(installationsConfig);
                RefreshInstallations();
                
                var combo = this.FindControl<ComboBox>("ProfileComboBox");
                if (combo != null)
                {
                    var newInst = installationsConfig.Installations.FirstOrDefault(i => i.Id == result.Result.Id);
                    if (newInst != null) combo.SelectedItem = newInst;
                }
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

                VersionInfo info;
                
                if (installation.LoaderType == ModLoaderType.Fabric && !string.IsNullOrEmpty(installation.ModLoaderVersion))
                {
                    string fabricVersionId = $"fabric-loader-{installation.ModLoaderVersion}-{installation.Version}";
                    Log($"Verifying Fabric version: {fabricVersionId}...");
                    
                    var jsonPath = Path.Combine(minecraftDirectory, "versions", fabricVersionId, $"{fabricVersionId}.json");
                    if (!File.Exists(jsonPath))
                    {
                         Log("Fabric profile not found, installing...");
                         await _fabricService.InstallFabricAsync(installation.Version, installation.ModLoaderVersion);
                    }
                    
                    Log($"Loading version info for {fabricVersionId}...");
                    info = await _minecraftService.GetVersionInfoFromLocalAsync(fabricVersionId);
                }
                else if (installation.LoaderType == ModLoaderType.Forge && !string.IsNullOrEmpty(installation.ModLoaderVersion))
                {
                     string forgeId = $"{installation.Version}-forge-{installation.ModLoaderVersion}";
                     var jsonPath = Path.Combine(minecraftDirectory, "versions", forgeId, $"{forgeId}.json");
                     
                     if (!File.Exists(jsonPath))
                     {
                          var versionsPath = Path.Combine(minecraftDirectory, "versions");
                          if (Directory.Exists(versionsPath))
                          {
                              var potentialDirs = Directory.GetDirectories(versionsPath)
                                                  .Select(Path.GetFileName)
                                                  .Where(n => n != null && n.Contains("forge") && !string.IsNullOrEmpty(installation.ModLoaderVersion) && n.Contains(installation.ModLoaderVersion))
                                                  .ToList();
                              
                              if (potentialDirs.Any())
                              {
                                  var firstDir = potentialDirs.First();
                                  if (firstDir != null)
                                  {
                                      forgeId = firstDir;
                                      jsonPath = Path.Combine(minecraftDirectory, "versions", forgeId, $"{forgeId}.json");
                                  }
                              }
                          }
                     }

                     if (!File.Exists(jsonPath) || !Directory.Exists(Path.Combine(minecraftDirectory, "versions", forgeId)))
                     {
                          Log("Forge profile not found, installing...");
                          if (!string.IsNullOrEmpty(installation.GameDirectory) && !Directory.Exists(installation.GameDirectory))
                          {
                               Directory.CreateDirectory(installation.GameDirectory);
                          }
                          
                          var installedId = await _forgeService.InstallForgeAsync(installation.Version, installation.ModLoaderVersion, installation.GameDirectory);
                          if (!string.IsNullOrEmpty(installedId)) forgeId = installedId;
                     }
                     
                     Log($"Loading version info for {forgeId}...");
                     info = await _minecraftService.GetVersionInfoFromLocalAsync(forgeId);
                }
                else
                {
                    Log("Fetching version manifest...");
                    var manifest = await _minecraftService.GetVersionManifestAsync();
                    var ver = manifest.Versions.FirstOrDefault(v => v.Id == installation.Version);
                    if (ver == null) throw new Exception("Version not found in manifest");
    
                    Log($"Fetching version details for {ver.Id}...");
                    info = await _minecraftService.GetVersionInfoAsync(ver.Url);
                }

                int requiredJava = info.JavaVersion?.MajorVersion ?? 8;
                if (requiredJava == 0) requiredJava = 8;

                string javaPathToUse = "";

                if (!string.IsNullOrEmpty(installation.JavaPath))
                {
                     if (File.Exists(installation.JavaPath)) 
                     {
                         javaPathToUse = installation.JavaPath;
                         Log($"Using Installation-specific Java: {javaPathToUse}");
                     }
                     else
                     {
                         Log($"Warning: Installation Java path not found: {installation.JavaPath}");
                     }
                }

                if (string.IsNullOrEmpty(javaPathToUse))
                {
                    var globalSettings = await ConfigManager.LoadSettingsAsync();
                    if (!string.IsNullOrEmpty(globalSettings.JavaPath))
                    {
                        if (File.Exists(globalSettings.JavaPath))
                        {
                            javaPathToUse = globalSettings.JavaPath;
                            Log($"Using Global Setting Java: {javaPathToUse}");
                        }
                    }
                }

                if (string.IsNullOrEmpty(javaPathToUse))
                {
                    Log($"Auto-detecting Java {requiredJava}...");
                    var installed = await _javaManager.GetInstalledJavaVersionsAsync();
                    var bestMatch = installed.Where(j => j.MajorVersion == requiredJava).FirstOrDefault();
                    
                    if (bestMatch != null)
                    {
                        javaPathToUse = bestMatch.Path;
                        Log($"Found compatible installed Java: {javaPathToUse}");
                    }
                    else
                    {
                        Log($"Java {requiredJava} not found. Downloading...");
                        
                        Dispatcher.UIThread.Post(() => 
                        {
                             if (statusText != null) statusText.Text = $"Downloading Java Runtime {requiredJava}...";
                             if (progressBar != null) { progressBar.IsVisible = true; progressBar.IsIndeterminate = false; progressBar.Value = 0; }
                        });

                        var downloadedPath = await _javaManager.DownloadJavaAsync(requiredJava, new Progress<double>(p => 
                        {
                            Dispatcher.UIThread.Post(() => { if (progressBar != null) progressBar.Value = p; });
                        }));
                        
                        if (!string.IsNullOrEmpty(downloadedPath) && File.Exists(downloadedPath))
                        {
                            javaPathToUse = downloadedPath;
                            Log($"Java {requiredJava} downloaded successfully: {javaPathToUse}");
                        }
                        else
                        {
                             var (sysVer, sysPath) = await PlatformManager.FindJavaInstallationAsync(requiredJava);
                             if (!string.IsNullOrEmpty(sysPath))
                             {
                                 javaPathToUse = sysPath;
                                 Log($"Download failed/ambiguous, falling back to system Java: {javaPathToUse}");
                             }
                             else
                             {
                                 throw new Exception($"Could not find or download Java {requiredJava}. Please install it manually.");
                             }
                        }
                    }
                }

                if (statusText != null) 
                {
                    Dispatcher.UIThread.Post(() => 
                    {
                        statusText.Text = Services.LocalizationService.Instance["Play_StatusDownloading"];
                        if (progressBar != null) progressBar.IsIndeterminate = false;
                    });
                }
                
                await _minecraftService.DownloadLibrariesAsync(info, (done, total) => 
                {
                    Dispatcher.UIThread.Post(() => { if (progressBar != null) progressBar.Value = (double)done / total * 100; });
                });

                Log("Checking and downloading game JAR...");
                string jarVersion = installation.Version;
                if (!string.IsNullOrEmpty(installation.BaseVersion)) jarVersion = installation.BaseVersion;
                else if (!string.IsNullOrEmpty(info.InheritsFrom)) jarVersion = info.InheritsFrom;
                
                await _minecraftService.DownloadVersionJarAsync(info, jarVersion);

                Dispatcher.UIThread.Post(() => { if (statusText != null) statusText.Text = "Downloading assets..."; });
                await _minecraftService.DownloadAssetsAsync(info, (done, total) => 
                {
                    Dispatcher.UIThread.Post(() => { if (progressBar != null) progressBar.Value = (double)done / total * 100; });
                });

                Dispatcher.UIThread.Post(() => { if (statusText != null) statusText.Text = "Extracting natives..."; });
                await _minecraftService.ExtractNativesAsync(info, installation.Version);

                Log("Starting game...");
                var settings = await ConfigManager.LoadSettingsAsync();
                var accountsConfig = await ConfigManager.LoadAccountsAsync();
                var account = accountsConfig.Accounts.FirstOrDefault(a => a.IsActive) ?? accountsConfig.Accounts.FirstOrDefault();
                
                if (account == null) throw new Exception("No account selected");
                Log($"Using account: {account.Username} ({account.AccountType})");

                if (account.AccountType == "Microsoft" && !string.IsNullOrEmpty(account.RefreshToken))
                {
                    bool needsRefresh = !account.ExpiresAt.HasValue || account.ExpiresAt.Value <= DateTime.Now.AddMinutes(5);
                    if (needsRefresh)
                    {
                        Log("Microsoft token expired or expiring soon. Refreshing...");
                        Dispatcher.UIThread.Post(() => { if (statusText != null) statusText.Text = "Refreshing Microsoft account..."; });
                        
                        var refreshResp = await Services.MicrosoftAuthService.Instance.RefreshTokenAsync(account.RefreshToken);
                        if (refreshResp != null)
                        {
                            var xboxResp = await Services.MicrosoftAuthService.Instance.AuthenticateWithXboxAsync(refreshResp.AccessToken);
                            var xstsResp = await Services.MicrosoftAuthService.Instance.AuthenticateWithXstsAsync(xboxResp.Token);
                            var mcResp = await Services.MicrosoftAuthService.Instance.AuthenticateWithMinecraftAsync(xstsResp.Token, xboxResp.DisplayClaims.Xui[0].Uhs);
                            
                            account.AccessToken = mcResp.AccessToken;
                            account.RefreshToken = refreshResp.RefreshToken;
                            account.ExpiresAt = DateTime.Now.AddSeconds(refreshResp.ExpiresIn);
                            
                            await ConfigManager.SaveAccountsAsync(accountsConfig);
                            Log("Microsoft token refreshed successfully.");
                        }
                        else
                        {
                            throw new Exception("Microsoft session expired. Please log in again.");
                        }
                    }
                }

                string? injectorPath = null;
                if (account.AccountType == "ElyBy")
                {
                    Log("Preparing Ely.by authentication...");
                    Dispatcher.UIThread.Post(() => { if (statusText != null) statusText.Text = "Preparing Ely.by authentication..."; });
                    injectorPath = await _minecraftService.EnsureAuthlibInjectorAsync();
                }

                if (!string.IsNullOrEmpty(installation.GameDirectory) && !Directory.Exists(installation.GameDirectory))
                {
                    Log($"Creating game directory: {installation.GameDirectory}");
                    Directory.CreateDirectory(installation.GameDirectory);
                }

                Log("Building launch arguments...");
                var args = LaunchCommandBuilder.BuildArguments(installation, account, info, settings, injectorPath);
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = javaPathToUse,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = string.IsNullOrEmpty(installation.GameDirectory) ? PlatformManager.GetMinecraftDirectory() : installation.GameDirectory
                };

                foreach (var arg in args)
                {
                    startInfo.ArgumentList.Add(arg);
                }

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
                    if (statusText != null) statusText.Text = Services.LocalizationService.Instance["Play_StatusReady"];
                    if (progressBar != null) progressBar.IsVisible = false;
                    
                    Services.NotificationService.Instance.ShowSuccess(
                        Services.LocalizationService.Instance["Message_SuccessTitle"],
                        Services.LocalizationService.Instance["Message_GameLaunched"]);
                });
                Log("Game started successfully.");
            }
            catch (Exception ex)
            {
                Log($"CRITICAL ERROR: {ex.Message}");
                Dispatcher.UIThread.Post(() => 
                {
                    if (statusText != null) statusText.Text = string.Format(Services.LocalizationService.Instance["Message_ErrorTitle"] + ": {0}", ex.Message);
                    if (progressBar != null) progressBar.IsVisible = false;
                    
                    Services.NotificationService.Instance.ShowError(
                        Services.LocalizationService.Instance["Message_ErrorTitle"],
                        ex.Message);
                });
            }
        }
    }
}
