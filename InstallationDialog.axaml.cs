using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace JustLauncher;

public partial class InstallationDialog : Window
{
    public Installation? Result { get; private set; }

    public InstallationDialog()
    {
        InitializeComponent();
        var textBox = this.FindControl<TextBox>("GameDirectoryTextBox");
        if (textBox != null) textBox.Text = PlatformManager.GetMinecraftDirectory();
    }

    public InstallationDialog(Installation existing) : this()
    {
        var nameBox = this.FindControl<TextBox>("NameTextBox");
        var dirBox = this.FindControl<TextBox>("GameDirectoryTextBox");
        var argsBox = this.FindControl<TextBox>("JavaArgsTextBox");
        var title = this.FindControl<TextBlock>("DialogTitle");
        var button = this.FindControl<Button>("CreateButton");

        if (nameBox != null) nameBox.Text = existing.Name;
        if (dirBox != null) dirBox.Text = existing.GameDirectory;
        if (argsBox != null) argsBox.Text = existing.JavaArgs;
        if (title != null) title.Text = "Edit Installation";
        if (button != null) button.Content = "Save Changes";
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        var closeBtn = this.FindControl<Button>("CloseButton");
        if (closeBtn != null) closeBtn.Click += CloseButton_Click;

        var browseBtn = this.FindControl<Button>("BrowseGameDirectoryButton");
        if (browseBtn != null) browseBtn.Click += BrowseGameDirectory_Click;

        var cancelBtn = this.FindControl<Button>("CancelButton");
        if (cancelBtn != null) cancelBtn.Click += CancelButton_Click;

        var createBtn = this.FindControl<Button>("CreateButton");
        if (createBtn != null) createBtn.Click += CreateButton_Click;

        var header = this.FindControl<Control>("DialogHeader");
        if (header != null) header.PointerPressed += (s, e) => BeginMoveDrag(e);
    }

    private void CreateButton_Click(object? sender, RoutedEventArgs e)
    {
        var nameBox = this.FindControl<TextBox>("NameTextBox");
        var versionCombo = this.FindControl<ComboBox>("VersionComboBox");
        var dirBox = this.FindControl<TextBox>("GameDirectoryTextBox");
        var argsBox = this.FindControl<TextBox>("JavaArgsTextBox");

        Result = new Installation
        {
            Name = string.IsNullOrWhiteSpace(nameBox?.Text) ? "New Installation" : nameBox.Text,
            Version = versionCombo?.SelectedItem?.ToString() ?? "1.21.1",
            GameDirectory = dirBox?.Text ?? PlatformManager.GetMinecraftDirectory(),
            JavaArgs = argsBox?.Text ?? "-Xmx2G",
            Icon = "grass_block"
        };
        Close(true);
    }

    private async void BrowseGameDirectory_Click(object? sender, RoutedEventArgs e)
    {
        var dirBox = this.FindControl<TextBox>("GameDirectoryTextBox");
        var startPath = dirBox?.Text ?? PlatformManager.GetMinecraftDirectory();
        
        Uri folderUri;
        try 
        { 
            if (Path.IsPathRooted(startPath))
            {
                folderUri = new Uri("file://" + startPath.Replace("\\", "/"));
            }
            else
            {
                folderUri = new Uri(startPath, UriKind.RelativeOrAbsolute);
            }
        } 
        catch { folderUri = new Uri("file:///"); }

        var result = await this.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            Title = "Select Game Directory",
            SuggestedStartLocation = await this.StorageProvider.TryGetFolderFromPathAsync(folderUri)
        });

        if (result != null && result.Count > 0)
        {
            if (dirBox != null) dirBox.Text = result[0].Path.LocalPath;
        }
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e) => Close(false);
    private void CloseButton_Click(object? sender, RoutedEventArgs e) => Close(false);
}
