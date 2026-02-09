using System;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media;
using JustLauncher.Services;

namespace JustLauncher
{
    public class AccountTypeToIconConverter : IValueConverter
    {
        public static AccountTypeToIconConverter Instance { get; } = new AccountTypeToIconConverter();

        public AccountTypeToIconConverter() { }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value?.ToString() switch
            {
                "Microsoft" => "🔷",
                "Mojang" => "🟫",
                "Offline" => "👤",
                _ => "👤"
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class AccountActiveStatusConverter : IValueConverter
    {
        public static AccountActiveStatusConverter Instance { get; } = new AccountActiveStatusConverter();

        public AccountActiveStatusConverter() { }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isActive)
            {
                return isActive ? "ACTIVE" : "INACTIVE";
            }
            return "INACTIVE";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class AccountBoolToColorConverter : IValueConverter
    {
        public static AccountBoolToColorConverter Instance { get; } = new AccountBoolToColorConverter();

        public AccountBoolToColorConverter() { }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isActive)
            {
                return new SolidColorBrush(isActive ? Color.FromRgb(0, 200, 81) : Color.FromRgb(158, 158, 158));
            }
            return new SolidColorBrush(Color.FromRgb(158, 158, 158));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class AsyncImageConverter : IValueConverter
    {
        public static AsyncImageConverter Instance { get; } = new AsyncImageConverter();
        
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Avalonia.Media.Imaging.Bitmap> _cache = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string url && !string.IsNullOrEmpty(url))
            {
                if (_cache.TryGetValue(url, out var cached)) return cached;

                // Return a placeholder and start loading
                _ = LoadBitmapAsync(url, parameter as ModInfo);
                
                try 
                { 
                    return new Avalonia.Media.Imaging.Bitmap(Avalonia.Platform.AssetLoader.Open(new Uri("avares://JustLauncher/Assets/grass_block.png"))); 
                } 
                catch { return null; }
            }
            return null;
        }

        private async Task LoadBitmapAsync(string url, ModInfo? mod)
        {
            if (_cache.ContainsKey(url)) return;

            try
            {
                var response = await HttpClientManager.Instance.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var stream = await response.Content.ReadAsStreamAsync();
                    var bitmap = new Avalonia.Media.Imaging.Bitmap(stream);
                    _cache[url] = bitmap;
                    
                    if (mod != null)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => mod.NotifyIconChanged());
                    }
                }
            }
            catch { }
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
    }
}