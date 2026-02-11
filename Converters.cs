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
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Task> _loadingTasks = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string url && !string.IsNullOrEmpty(url))
            {
                if (_cache.TryGetValue(url, out var cached)) return cached;

                _ = LoadBitmapAsync(url, parameter as ModInfo);
                
                try 
                { 
                    return new Avalonia.Media.Imaging.Bitmap(Avalonia.Platform.AssetLoader.Open(new Uri("avares://JustLauncher/Assets/grass_block.png"))); 
                } 
                catch (Exception ex)
                {
                    ConsoleService.Instance.Log($"[AsyncImage] Error loading placeholder: {ex.Message}");
                    return null;
                }
            }
            return null;
        }

        private async Task LoadBitmapAsync(string url, ModInfo? mod)
        {
            if (_cache.ContainsKey(url)) return;

            if (_loadingTasks.TryGetValue(url, out var existingTask))
            {
                await existingTask;
                if (mod != null)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => mod.NotifyIconChanged());
                }
                return;
            }

            var tcs = new TaskCompletionSource();
            if (!_loadingTasks.TryAdd(url, tcs.Task)) return;

            try
            {
                string? cachedPath = await ImageCacheService.GetCachedImageAsync(url);
                if (!string.IsNullOrEmpty(cachedPath) && File.Exists(cachedPath))
                {
                    await Task.Run(() =>
                    {
                        using (var stream = File.OpenRead(cachedPath))
                        {
                            var bitmap = Avalonia.Media.Imaging.Bitmap.DecodeToWidth(stream, 80);
                            _cache[url] = bitmap;
                        }
                    });
                    
                    if (mod != null)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => mod.NotifyIconChanged());
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleService.Instance.Log($"[AsyncImage] Error loading {url}: {ex.Message}");
            }
            finally
            {
                _loadingTasks.TryRemove(url, out _);
                tcs.SetResult();
            }
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
    }
}