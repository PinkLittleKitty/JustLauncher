using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using JustLauncher.Services;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace JustLauncher.Controls;

public partial class ModsControl : UserControl
{
    private string? _gameDirectory;
    private ModManagerService _modManager = new();

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

        var modsList = this.FindControl<ListBox>("ModsListBox");
        if (modsList != null)
        {
            modsList.SelectionChanged += ModsListBox_SelectionChanged;
        }
    }

    public void Initialize(string? gameDirectory)
    {
        _gameDirectory = gameDirectory;
        _ = LoadModsAsync();
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

    private string GetModsDirectory()
    {
        if (string.IsNullOrEmpty(_gameDirectory)) return Path.Combine(PlatformManager.GetMinecraftDirectory(), "mods");
        return Path.Combine(_gameDirectory, "mods");
    }

    private void OpenModsFolderButton_Click(object? sender, RoutedEventArgs e)
    {
        string modsDir = GetModsDirectory();
        if (!Directory.Exists(modsDir)) Directory.CreateDirectory(modsDir);
        
        if (PlatformManager.IsWindows()) Process.Start("explorer", modsDir);
        else if (PlatformManager.IsLinux()) Process.Start("xdg-open", modsDir);
        else if (PlatformManager.IsMac()) Process.Start("open", modsDir);
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
            // Toggle mod
            _modManager.ToggleMod(mod);
            await LoadModsAsync();
            list.SelectedItem = null; // Deselect
        }
    }
}
