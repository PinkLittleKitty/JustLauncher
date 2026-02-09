using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using JustLauncher.Models;
using JustLauncher.Services;
using System;
using System.Threading.Tasks;
using Avalonia.Media;
using Projektanker.Icons.Avalonia;

namespace JustLauncher.Controls;

public partial class NotificationItem : UserControl
{
    private NotificationModel? _model;

    public NotificationItem()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public void Initialize(NotificationModel model)
    {
        _model = model;
        
        var title = this.FindControl<TextBlock>("TitleText");
        var message = this.FindControl<TextBlock>("MessageText");
        var container = this.FindControl<Border>("Container");
        var icon = this.FindControl<Projektanker.Icons.Avalonia.Icon>("TypeIcon");
        var closeBtn = this.FindControl<Button>("CloseButton");

        if (title != null) title.Text = model.Title;
        if (message != null) message.Text = model.Message;
        
        if (container != null)
        {
            container.Classes.Add(model.Type.ToString());
        }

        if (icon != null)
        {
            icon.Value = model.Type switch
            {
                NotificationType.Success => "fa-solid fa-check-circle",
                NotificationType.Warning => "fa-solid fa-exclamation-triangle",
                NotificationType.Error => "fa-solid fa-times-circle",
                _ => "fa-solid fa-info-circle"
            };
            
            icon.Foreground = model.Type switch
            {
                NotificationType.Success => App.Current != null && App.Current.TryFindResource("SuccessBrush", out var res) ? (IBrush)res! : Brushes.Green,
                NotificationType.Warning => Brushes.Orange,
                NotificationType.Error => Brushes.Red,
                _ => App.Current != null && App.Current.TryFindResource("AccentBrush", out var res) ? (IBrush)res! : Brushes.Blue
            };
        }

        if (closeBtn != null)
        {
            closeBtn.Click += (s, e) => RemoveWithAnimation();
        }

        Task.Delay(model.Duration).ContinueWith(_ => 
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => RemoveWithAnimation());
        });

        Task.Delay(50).ContinueWith(_ => 
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => 
            {
                if (container != null)
                {
                    container.Opacity = 1;
                    container.RenderTransform = Avalonia.Media.Transformation.TransformOperations.Parse("translateX(0px)");
                }
            });
        });
    }

    private async void RemoveWithAnimation()
    {
        var container = this.FindControl<Border>("Container");
        if (container != null)
        {
            container.Opacity = 0;
            container.RenderTransform = Avalonia.Media.Transformation.TransformOperations.Parse("translateX(50px)");
            await Task.Delay(300);
        }
        
        if (_model != null)
        {
            NotificationService.Instance.Remove(_model);
        }
    }
}
