using System;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using JustLauncher.Utils;

namespace JustLauncher.Converters;

public class SkinToBitmapConverter : IValueConverter
{
    private static readonly string AvatarUrlBase = "https://minotar.net/avatar/";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string username = value as string ?? "Steve";
        string url = $"{AvatarUrlBase}{username}/64";

        var cached = ImageLoader.GetFromCache(url);
        if (cached != null) return cached;

        _ = LoadAsync(url);
        return null;
    }

    private async Task LoadAsync(string url)
    {
        await ImageLoader.LoadFromUrlAsync(url);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
