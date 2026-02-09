using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using JustLauncher.Services;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace JustLauncher.Controls;

public partial class ModsControl : UserControl
{
    private Installation? _installation;
    private ModManagerService _modManager = new();
    private ModrinthService _modrinthService = new();
    private CurseForgeService _curseForgeService = new();

    public ModsControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        var openModsBtn = this.FindControl<Button>("OpenModsFolderButton");
        if (openModsBtn != null) openModsBtn.Click += OpenModsFolderButton_Click;

        var addModBtn = this.FindControl<Button>("AddModButton");
        if (addModBtn != null) addModBtn.Click += async (s, e) => { await AddModButton_Click(); };

        var refreshModsBtn = this.FindControl<Button>("RefreshModsButton");
        if (refreshModsBtn != null) refreshModsBtn.Click += async (s, e) => { await LoadModsAsync(); };

        var searchBtn = this.FindControl<Button>("SearchButton");
        if (searchBtn != null) searchBtn.Click += async (s, e) => { await SearchModsAsync(); };

        var modsList = this.FindControl<ListBox>("ModsListBox");
        if (modsList != null)
        {
            modsList.SelectionChanged += ModsListBox_SelectionChanged;
        }

        var browseList = this.FindControl<ListBox>("BrowseListBox");
        if (browseList != null)
        {
            browseList.AddHandler(Button.ClickEvent, async (object? sender, RoutedEventArgs e) =>
            {
                if (e.Source is Button btn && btn.Name == "DownloadButton" && btn.Tag is ModInfo mod)
                {
                    await DownloadModAsync(mod, btn);
                }
            }, RoutingStrategies.Bubble);
        }
    }

    public void Initialize(Installation? installation)
    {
        if (installation == null) return;

        // Fallback for older installations or incomplete data
        if (string.IsNullOrEmpty(installation.BaseVersion))
        {
            installation.BaseVersion = installation.Version;
        }

        ConsoleService.Instance.Log($"[Mods] Initializing for {installation.Name} (Version: {installation.BaseVersion}, Loader: {installation.LoaderType})");
        _installation = installation;
        _ = LoadModsAsync();
        _ = SearchModsAsync();
    }

    public async Task LoadModsAsync()
    {
        string modsDir = GetModsDirectory();
        if (!Directory.Exists(modsDir)) Directory.CreateDirectory(modsDir);

        var mods = await _modManager.GetModsAsync(modsDir);
        var modsList = this.FindControl<ListBox>("ModsListBox");
        if (modsList != null)
        {
            modsList.ItemsSource = mods;
        }
    }

    private async Task SearchModsAsync()
    {
        if (_installation == null) 
        {
            ConsoleService.Instance.Log("[Mods] Search skipped: No installation context");
            return;
        }

        var searchBox = this.FindControl<TextBox>("SearchBox");
        string query = searchBox?.Text ?? "";
        ConsoleService.Instance.Log($"[Mods] Searching for '{query}'...");
        
        List<ModInfo> results;
        if (_installation.LoaderType == ModLoaderType.Fabric)
        {
            results = await _modrinthService.SearchModsAsync(query, _installation.BaseVersion, "fabric");
        }
        else if (_installation.LoaderType == ModLoaderType.Forge)
        {
            results = await _curseForgeService.SearchModsAsync(query, _installation.BaseVersion, "forge");
        }
        else
        {
            results = new List<ModInfo>();
        }

        var installedMods = await _modManager.GetModsAsync(GetModsDirectory());
        foreach (var mod in results)
        {
            mod.IsInstalled = installedMods.Any(m => m.Name == mod.Name || m.FileName == mod.FileName);
        }

        var browseList = this.FindControl<ListBox>("BrowseListBox");
        if (browseList != null)
        {
            browseList.ItemsSource = null;
            browseList.ItemsSource = results;
        }
    }

    private async Task DownloadModAsync(ModInfo mod, Button btn)
    {
        if (_installation == null) return;
        btn.IsEnabled = false;
        btn.Content = "Downloading...";

        try
        {
            string? downloadUrl = mod.DownloadUrl;
            if (string.IsNullOrEmpty(downloadUrl))
            {
                if (_installation.LoaderType == ModLoaderType.Fabric)
                    downloadUrl = await _modrinthService.GetDownloadUrlAsync(mod.ProjectId!, _installation.BaseVersion, "fabric");
                else if (_installation.LoaderType == ModLoaderType.Forge)
                    downloadUrl = await _curseForgeService.GetDownloadUrlAsync(mod.ProjectId!, _installation.BaseVersion, "forge");
            }

            if (!string.IsNullOrEmpty(downloadUrl))
            {
                string modsDir = GetModsDirectory();
                string fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
                string dest = Path.Combine(modsDir, fileName);

                mod.IsDownloading = true;
                mod.DownloadProgress = 0;

                await MinecraftService.Instance.DownloadFileAsync(downloadUrl, dest, (read, total) =>
                {
                    if (total > 0)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => 
                            mod.DownloadProgress = (double)read / total * 100);
                    }
                });
                
                Avalonia.Threading.Dispatcher.UIThread.Post(async () => {
                    mod.IsDownloading = false;
                    mod.IsInstalled = true;
                    await LoadModsAsync();
                });
            }
        }
        catch (Exception ex)
        {
            ConsoleService.Instance.Log($"[Mods] Download failed: {ex.Message}");
            btn.Content = "Retry";
            btn.IsEnabled = true;
        }
    }

    private string GetModsDirectory()
    {
        string? gameDir = _installation?.GameDirectory;
        if (string.IsNullOrEmpty(gameDir)) return Path.Combine(PlatformManager.GetMinecraftDirectory(), "mods");
        return Path.Combine(gameDir, "mods");
    }

    private void OpenModsFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        string modsDir = GetModsDirectory();
        PlatformManager.OpenFolder(modsDir);
    }
    
    private async Task AddModButton_Click()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;
        
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Mods",
            AllowMultiple = true,
            FileTypeFilter = new[] 
            { 
                 new FilePickerFileType("Mods") { Patterns = new[] { "*.jar", "*.zip" } } 
            }
        });
        
        if (files != null && files.Count > 0)
        {
            string modsDir = GetModsDirectory();
            if (!Directory.Exists(modsDir)) Directory.CreateDirectory(modsDir);
            
            foreach (var file in files)
            {
                var dest = Path.Combine(modsDir, file.Name);
                if (file.Path.LocalPath != dest)
                {
                    File.Copy(file.Path.LocalPath, dest, true);
                }
            }
            
            await LoadModsAsync();
        }
    }

    private async void ModsListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox list && list.SelectedItem is ModInfo mod)
        {
            _modManager.ToggleMod(mod);
            await LoadModsAsync();
            list.SelectedItem = null;
        }
    }
}
