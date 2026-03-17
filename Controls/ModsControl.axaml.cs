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
using System.Collections.ObjectModel;
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
    private int _searchIndex = 0;
    private ObservableCollection<ModInfo> _mods = new();
    private ObservableCollection<ModInfo> _browseResults = new();
    private HashSet<string> _processedProjectIds = new();
    private bool _isSearching = false;

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

        var checkUpdatesBtn = this.FindControl<Button>("CheckUpdatesButton");
        if (checkUpdatesBtn != null) checkUpdatesBtn.Click += async (s, e) => { await CheckForUpdatesAsync(); };

        var searchBtn = this.FindControl<Button>("SearchButton");
        if (searchBtn != null) searchBtn.Click += async (s, e) => { await SearchModsAsync(); };

        var loadMoreBtn = this.FindControl<Button>("LoadMoreButton");
        if (loadMoreBtn != null) loadMoreBtn.Click += async (s, e) => { await LoadMoreModsAsync(); };

        var modsList = this.FindControl<ListBox>("ModsListBox");
        if (modsList != null)
        {
            modsList.SelectionChanged += ModsListBox_SelectionChanged;
            modsList.AddHandler(Button.ClickEvent, async (object? sender, RoutedEventArgs e) =>
            {
                if (e.Source is Button btn && btn.Name == "UpdateSingleButton" && btn.Tag is ModInfo mod)
                {
                    await UpdateModAsync(mod, btn);
                }
            }, RoutingStrategies.Bubble);
        }

        var browseList = this.FindControl<ListBox>("BrowseListBox");
        if (browseList != null)
        {
            browseList.ItemsSource = _browseResults;
            browseList.AddHandler(Button.ClickEvent, async (object? sender, RoutedEventArgs e) =>
            {
                if (e.Source is Button btn && (btn.Name == "DownloadButton" || btn.Name == "UpdateBrowseButton") && btn.Tag is ModInfo mod)
                {
                    await DownloadModAsync(mod, btn);
                }
            }, RoutingStrategies.Bubble);
        }

        var modsListControl = this.FindControl<ListBox>("ModsListBox");
        if (modsListControl != null)
        {
            modsListControl.ItemsSource = _mods;
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
        
        _mods.Clear();
        foreach (var mod in mods) _mods.Add(mod);

        _ = Task.Run(async () => {
            if (_installation != null)
                await _modManager.CheckForUpdatesAsync(_mods.ToList(), _installation.BaseVersion, _installation.LoaderType.ToString().ToLower());
        });
    }

    private async Task CheckForUpdatesAsync()
    {
        if (_installation == null) return;
        var modsList = this.FindControl<ListBox>("ModsListBox");
        if (modsList == null || modsList.ItemsSource == null) return;

        var mods = ((IEnumerable<ModInfo>)modsList.ItemsSource).ToList();
        var checkBtn = this.FindControl<Button>("CheckUpdatesButton");
        if (checkBtn != null) checkBtn.IsEnabled = false;

        ConsoleService.Instance.Log($"[Mods] Checking for updates for {mods.Count} mods...");
        
        string loader = _installation.LoaderType.ToString().ToLower();
        await _modManager.CheckForUpdatesAsync(mods, _installation.BaseVersion, loader);
        
        if (checkBtn != null) checkBtn.IsEnabled = true;
        ConsoleService.Instance.Log("[Mods] Update check complete.");
    }

    private async Task SearchModsAsync()
    {
        _searchIndex = 0;
        _browseResults.Clear();
        _processedProjectIds.Clear();

        await PerformSearchAsync();
    }

    private async Task PerformSearchAsync()
    {
        if (_installation == null || _isSearching) return;
        _isSearching = true;

        var loadMoreBtn = this.FindControl<Button>("LoadMoreButton");
        if (loadMoreBtn != null) loadMoreBtn.Content = "Loading...";

        try
        {
            var searchBox = this.FindControl<TextBox>("SearchBox");
            string query = searchBox?.Text ?? "";
            
            List<ModInfo> results;
            if (_installation.LoaderType == ModLoaderType.Fabric)
            {
                results = await _modrinthService.SearchModsAsync(query, _installation.BaseVersion, "fabric", _searchIndex);
            }
            else if (_installation.LoaderType == ModLoaderType.Forge)
            {
                results = await _curseForgeService.SearchModsAsync(query, _installation.BaseVersion, "forge", _searchIndex);
            }
            else
            {
                results = new List<ModInfo>();
            }

            var installedMods = await _modManager.GetModsAsync(GetModsDirectory());
            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                _browseResults.Clear();
                foreach (var mod in results)
                {
                    if (string.IsNullOrEmpty(mod.ProjectId) || _processedProjectIds.Contains(mod.ProjectId))
                        continue;

                    mod.IsInstalled = installedMods.Any(m => m.Name == mod.Name || m.FileName == mod.FileName);
                    _browseResults.Add(mod);
                    _processedProjectIds.Add(mod.ProjectId);
                }
            });

            if (loadMoreBtn != null)
            {
                loadMoreBtn.IsVisible = results.Count >= 20;
                loadMoreBtn.Content = "Load More";
            }
        }
        finally
        {
            _isSearching = false;
        }
    }

    private async Task LoadMoreModsAsync()
    {
        _searchIndex += 20;
        await PerformSearchAsync();
    }

    private async Task UpdateModAsync(ModInfo mod, Button btn)
    {
        if (string.IsNullOrEmpty(mod.UpdateUrl)) return;
        
        btn.IsEnabled = false;
        btn.Content = "Updating...";

        try
        {
            mod.DownloadUrl = mod.UpdateUrl;
            await DownloadModAsync(mod, btn, true);
        }
        catch (Exception ex)
        {
            ConsoleService.Instance.Log($"[Mods] Update failed for {mod.Name}: {ex.Message}");
            btn.Content = "Retry";
            btn.IsEnabled = true;
        }
    }

    private async Task DownloadModAsync(ModInfo mod, Button btn, bool isUpdate = false)
    {
        if (_installation == null) return;
        btn.IsEnabled = false;
        string originalContent = isUpdate ? "Update" : (btn.Content?.ToString() ?? "Download");
        btn.Content = "Processing...";

        try
        {
            string modsDir = GetModsDirectory();
            var installedMods = await _modManager.GetModsAsync(modsDir);
            string loader = _installation.LoaderType.ToString();
            
            ConsoleService.Instance.Log($"[Mods] Resolving dependencies for {mod.Name}...");
            var deps = await _modManager.ResolveDependenciesAsync(mod.ProjectId!, _installation.BaseVersion, loader, installedMods);
            
            if (deps.Count > 0)
            {
                ConsoleService.Instance.Log($"[Mods] Found {deps.Count} dependencies to install.");
                foreach (var dep in deps)
                {
                    btn.Content = $"Downloading {dep.Name}...";
                    await DownloadModInternalAsync(dep, modsDir);
                    
                    if (!_mods.Any(m => m.ProjectId == dep.ProjectId))
                    {
                        var newMod = await _modManager.GetModAsync(dep.Path);
                        if (newMod != null) 
                        {
                            newMod.ProjectId = dep.ProjectId;
                            Avalonia.Threading.Dispatcher.UIThread.Post(() => _mods.Add(newMod));
                        }
                    }
                }
            }

            btn.Content = "Downloading...";
            string oldPath = mod.Path;
            await DownloadModInternalAsync(mod, modsDir);

            // If it was an update and the filename changed, delete the old file
            if (isUpdate && !string.IsNullOrEmpty(oldPath) && oldPath != mod.Path && File.Exists(oldPath))
            {
                try { File.Delete(oldPath); } catch { /* ignore */ }
            }

            // Update metadata for the mod
            var updatedMeta = await _modManager.GetModAsync(mod.Path);
            if (updatedMeta != null)
            {
                mod.Name = updatedMeta.Name;
                mod.Version = updatedMeta.Version;
                mod.Description = updatedMeta.Description;
                mod.FileName = updatedMeta.FileName;
            }

            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                mod.IsDownloading = false;
                mod.IsInstalled = true;
                mod.UpdateAvailable = false;
                btn.Content = originalContent;
                btn.IsEnabled = true;
                
                if (!isUpdate && !_mods.Any(m => m.ProjectId == mod.ProjectId))
                {
                    _mods.Add(mod);
                }
            });
        }
        catch (Exception ex)
        {
            ConsoleService.Instance.Log($"[Mods] Download failed: {ex.Message}");
            btn.Content = "Retry";
            btn.IsEnabled = true;
        }
    }

    private async Task DownloadModInternalAsync(ModInfo mod, string modsDir)
    {
        if (_installation == null) return;

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
            mod.IsDownloading = false;
            mod.IsInstalled = true;
            mod.Path = dest;
            mod.FileName = fileName;
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

    private void ModsListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox list && list.SelectedItem is ModInfo mod)
        {
            _modManager.ToggleMod(mod);
            list.SelectedItem = null;
        }
    }
}
