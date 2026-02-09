using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace JustLauncher;

public partial class ProfileDialog : Window
{
    public string? ProfileName { get; private set; }
    public string? GameDirectory { get; private set; }
    public double MemoryAllocationGb { get; private set; } = 4.0;

    public ProfileDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        var closeBtn = this.FindControl<Button>("CloseButton");
        if (closeBtn != null) closeBtn.Click += CloseButton_Click;

        var browseBtn = this.FindControl<Button>("BrowseDirectoryButton");
        if (browseBtn != null) browseBtn.Click += BrowseDirectoryButton_Click;

        var cancelBtn = this.FindControl<Button>("CancelButton");
        if (cancelBtn != null) cancelBtn.Click += CancelButton_Click;

        var createBtn = this.FindControl<Button>("CreateButton");
        if (createBtn != null) createBtn.Click += CreateButton_Click;

        var header = this.FindControl<Control>("DialogHeader");
        if (header != null) header.PointerPressed += (s, e) => BeginMoveDrag(e);

        var memorySlider = this.FindControl<Slider>("MemorySlider");
        if (memorySlider != null)
        {
            memorySlider.Value = MemoryAllocationGb;
        }
    }

    private void MemorySlider_PropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Avalonia.Controls.Primitives.RangeBase.ValueProperty && sender is Slider slider)
        {
            MemoryAllocationGb = slider.Value;
            
            var valueText = this.FindControl<TextBlock>("MemoryValueText");
            if (valueText != null)
            {
                valueText.Text = $"{(int)slider.Value} GB";
            }
        }
    }

    private void CreateButton_Click(object? sender, RoutedEventArgs e)
    {
        ProfileName = this.FindControl<TextBox>("ProfileNameTextBox")?.Text;
        GameDirectory = this.FindControl<TextBox>("GameDirectoryTextBox")?.Text;
        Close(true);
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e) => Close(false);
    private void CloseButton_Click(object? sender, RoutedEventArgs e) => Close(false);

    private async void BrowseDirectoryButton_Click(object? sender, RoutedEventArgs e)
    {
        var result = await this.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            Title = "Select Game Directory",
            SuggestedStartLocation = await this.StorageProvider.TryGetFolderFromPathAsync(new Uri("file://" + PlatformManager.GetMinecraftDirectory()))
        });

        if (result != null && result.Count > 0)
        {
            var dirBox = this.FindControl<TextBox>("GameDirectoryTextBox");
            if (dirBox != null) dirBox.Text = result[0].Path.LocalPath;
        }
    }
}
