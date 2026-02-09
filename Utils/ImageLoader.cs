using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace JustLauncher.Utils;

public static class ImageLoader
{
    private static readonly ConcurrentDictionary<string, Bitmap> _cache = new();

    public static async Task<Bitmap?> LoadFromUrlAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (_cache.TryGetValue(url, out var bitmap)) return bitmap;

        try
        {
            var bytes = await HttpClientManager.Instance.GetByteArrayAsync(url);
            using var stream = new MemoryStream(bytes);
            var result = new Bitmap(stream);
            _cache[url] = result;
            return result;
        }
        catch (Exception ex)
        {
            ConsoleService.Instance.Log($"[ERROR] Failed to load image from {url}: {ex.Message}");
            return null;
        }
    }

    public static Bitmap? GetFromCache(string url)
    {
        _cache.TryGetValue(url, out var bitmap);
        return bitmap;
    }
}
