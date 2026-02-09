using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System.Configuration;

namespace JustLauncher;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var content = this.FindControl<ContentControl>("MainContent");
        if (content != null) content.Content = new HomePage();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        var playBtn = this.FindControl<Button>("PlayButton");
        if (playBtn != null) playBtn.Click += PlayButton_Click;

        var accBtn = this.FindControl<Button>("AccountsButton");
        if (accBtn != null) accBtn.Click += AccountsButton_Click;

        var setBtn = this.FindControl<Button>("SettingsButton");
        if (setBtn != null) setBtn.Click += SettingsButton_Click;

        var minBtn = this.FindControl<Button>("MinimizeButton");
        if (minBtn != null) minBtn.Click += MinimizeButton_Click;

        var maxBtn = this.FindControl<Button>("MaximizeButton");
        if (maxBtn != null) maxBtn.Click += MaximizeButton_Click;

        var clsBtn = this.FindControl<Button>("CloseButton");
        if (clsBtn != null) clsBtn.Click += CloseButton_Click;

        var titleBar = this.FindControl<Control>("TitleBar");
        if (titleBar != null) titleBar.PointerPressed += TitleBar_PointerPressed;
    }

    private void PlayButton_Click(object? sender, RoutedEventArgs e)
    {
        string username = ConfigurationManager.AppSettings["LastUsedUsername"] ?? "Player";
        var content = this.FindControl<ContentControl>("MainContent");
        if (content != null) content.Content = new PlayPage(username);
    }

    private void AccountsButton_Click(object? sender, RoutedEventArgs e)
    {
        var content = this.FindControl<ContentControl>("MainContent");
        if (content != null) content.Content = new AccountsPage();
    }

    private void SettingsButton_Click(object? sender, RoutedEventArgs e)
    {
        var content = this.FindControl<ContentControl>("MainContent");
        if (content != null) content.Content = new SettingsPage();
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
        }
        else
        {
            BeginMoveDrag(e);
        }
    }

    private void MinimizeButton_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object? sender, RoutedEventArgs e)
    {
        ToggleMaximize();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
}
