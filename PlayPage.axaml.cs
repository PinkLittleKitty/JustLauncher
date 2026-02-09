using System;
using System.Collections.Generic;
using System.IO;
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

        public PlayPage() : this("Player") { }

        public PlayPage(string username)
        {
            InitializeComponent();
            this.username = username;

            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "JustLauncher/1.0");

            InitializeDirectories();
            LoadInstallationsConfig();
            RefreshInstallations();
            _ = CheckJavaAndShowCompatibleVersions();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void InitializeDirectories()
        {
            minecraftDirectory = PlatformManager.GetMinecraftDirectory();
            if (!Directory.Exists(minecraftDirectory)) Directory.CreateDirectory(minecraftDirectory);
            installationsConfigPath = Path.Combine(minecraftDirectory, "launcher_installations.json");
        }

        private void LoadInstallationsConfig()
        {
            if (File.Exists(installationsConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(installationsConfigPath);
                    installationsConfig = JsonSerializer.Deserialize<InstallationsConfig>(json) ?? new();
                }
                catch { installationsConfig = new(); }
            }

            if (installationsConfig.Installations.Count == 0)
            {
                installationsConfig.Installations.AddRange(MockData.GetInstallations());
            }
        }

        private void RefreshInstallations()
        {
            var panel = this.FindControl<ItemsControl>("InstallationsPanel");
            if (panel != null)
            {
                panel.ItemsSource = null;
                panel.ItemsSource = installationsConfig.Installations;
            }
        }

        private async Task CheckJavaAndShowCompatibleVersions()
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

        private void LaunchButton_Click(object? sender, RoutedEventArgs e)
        {
            // Launch logic
        }
    }
}
