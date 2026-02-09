using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.Threading.Tasks;

namespace JustLauncher.Services;

public static class OverlayService
{
    private static Grid? _overlayLayer;
    private static Border? _dimmer;
    private static ContentControl? _host;
    private static TaskCompletionSource<object?>? _tcs;

    public static void Initialize(Grid layer, Border dimmer, ContentControl host)
    {
        _overlayLayer = layer;
        _dimmer = dimmer;
        _host = host;
    }

    public static async Task<T?> ShowDialog<T>(Control content)
    {
        if (_overlayLayer == null || _host == null || _dimmer == null) return default;

        _tcs = new TaskCompletionSource<object?>();
        _host.Content = content;
        
        _overlayLayer.IsVisible = true;
        _dimmer.Opacity = 0.6;
        _host.Opacity = 1;
        _host.RenderTransform = new ScaleTransform(1, 1);

        var result = await _tcs.Task;
        return (T?)result;
    }

    public static void Close(object? result = null)
    {
        if (_overlayLayer == null || _host == null || _dimmer == null) return;

        _dimmer.Opacity = 0;
        _host.Opacity = 0;
        _host.RenderTransform = new ScaleTransform(0.9, 0.9);

        Task.Delay(250).ContinueWith(_ => 
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                _overlayLayer.IsVisible = false;
                _host.Content = null;
            });
        });

        _tcs?.SetResult(result);
    }
}
