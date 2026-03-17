using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using JustLauncher.Services;
using System;
using System.IO;
using System.Threading.Tasks;

namespace JustLauncher;

public partial class ImportModpackDialog : UserControl
{
    public event EventHandler? OnClosed;
    public event EventHandler<Installation>? OnImportFinished;

    private string? _selectedPath;
    private ModpackService? _modpackService;

    public ImportModpackDialog()
    {
        InitializeComponent();
        
        // Use PlatformManager to get the minecraft directory
        string mcDir = PlatformManager.GetMinecraftDirectory();
        _modpackService = new ModpackService(mcDir);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        var browseBtn = this.FindControl<Button>("BrowseButton");
        if (browseBtn != null) browseBtn.Click += async (s, e) => await BrowseButton_Click();

        var importBtn = this.FindControl<Button>("ImportButton");
        if (importBtn != null) importBtn.Click += async (s, e) => await ImportButton_Click();

        var cancelBtn = this.FindControl<Button>("CancelButton");
        if (cancelBtn != null) cancelBtn.Click += (s, e) => OnClosed?.Invoke(this, EventArgs.Empty);
    }

    private async Task BrowseButton_Click()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Modpack File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Modpacks") { Patterns = new[] { "*.mrpack", "*.zip" } }
            }
        });

        if (files != null && files.Count > 0)
        {
            _selectedPath = files[0].Path.LocalPath;
            var pathBox = this.FindControl<TextBox>("FilePathBox");
            if (pathBox != null) pathBox.Text = _selectedPath;

            var importBtn = this.FindControl<Button>("ImportButton");
            if (importBtn != null) importBtn.IsEnabled = true;
        }
    }

    private async Task ImportButton_Click()
    {
        if (string.IsNullOrEmpty(_selectedPath) || _modpackService == null) return;

        var importBtn = this.FindControl<Button>("ImportButton");
        var cancelBtn = this.FindControl<Button>("CancelButton");
        var selectionGrid = this.FindControl<Grid>("SelectionGrid");
        var progressArea = this.FindControl<StackPanel>("ProgressArea");
        var statusLabel = this.FindControl<TextBlock>("StatusLabel");
        var progressBar = this.FindControl<ProgressBar>("ImportProgressBar");

        if (importBtn != null) importBtn.IsEnabled = false;
        if (cancelBtn != null) cancelBtn.IsEnabled = false;
        if (selectionGrid != null) selectionGrid.IsVisible = false;
        if (progressArea != null) progressArea.IsVisible = true;

        try
        {
            var installation = await _modpackService.ImportModpackAsync(_selectedPath, (status, progress) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (statusLabel != null) statusLabel.Text = status;
                    if (progressBar != null) progressBar.Value = progress;
                });
            });

            if (installation != null)
            {
                OnImportFinished?.Invoke(this, installation);
                OnClosed?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                if (statusLabel != null) statusLabel.Text = "Import failed!";
                if (importBtn != null)
                {
                    importBtn.IsEnabled = true;
                    importBtn.Content = "Retry";
                }
                if (cancelBtn != null) cancelBtn.IsEnabled = true;
                if (selectionGrid != null) selectionGrid.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            if (statusLabel != null) statusLabel.Text = $"Error: {ex.Message}";
            if (importBtn != null) importBtn.IsEnabled = true;
            if (cancelBtn != null) cancelBtn.IsEnabled = true;
        }
    }
}
