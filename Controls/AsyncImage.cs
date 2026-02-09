using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using JustLauncher.Utils;

namespace JustLauncher.Controls;

public class AsyncImage : Image
{
    public static readonly StyledProperty<string> SourceUrlProperty =
        AvaloniaProperty.Register<AsyncImage, string>(nameof(SourceUrl));

    public static readonly StyledProperty<Rect> SourceRectProperty =
        AvaloniaProperty.Register<AsyncImage, Rect>(nameof(SourceRect));

    public string SourceUrl
    {
        get => GetValue(SourceUrlProperty);
        set => SetValue(SourceUrlProperty, value);
    }

    public Rect SourceRect
    {
        get => GetValue(SourceRectProperty);
        set => SetValue(SourceRectProperty, value);
    }

    private CancellationTokenSource? _cts;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SourceUrlProperty || change.Property == SourceRectProperty)
        {
            UpdateSource(SourceUrl);
        }
    }

    private async void UpdateSource(string? url)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        if (string.IsNullOrEmpty(url))
        {
            Source = null;
            return;
        }

        var cached = ImageLoader.GetFromCache(url);
        Bitmap? bitmap = cached;

        if (bitmap == null)
        {
            Source = null;
            bitmap = await ImageLoader.LoadFromUrlAsync(url);
        }

        if (!token.IsCancellationRequested && bitmap != null)
        {
            if (SourceRect != default)
            {
                double scale = bitmap.PixelSize.Width / 64.0;
                var cropped = new CroppedBitmap(bitmap, new PixelRect(
                    (int)(SourceRect.X * scale), (int)(SourceRect.Y * scale), 
                    (int)(SourceRect.Width * scale), (int)(SourceRect.Height * scale)));
                Dispatcher.UIThread.Post(() => Source = cropped);
            }
            else
            {
                Dispatcher.UIThread.Post(() => Source = bitmap);
            }
        }
    }
}
