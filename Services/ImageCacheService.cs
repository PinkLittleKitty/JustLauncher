using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace JustLauncher.Services;

public static class ImageCacheService
{
    private static readonly string CacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JustLauncher", "Cache", "Images");
    private static readonly ConcurrentDictionary<string, Task<string?>> _activeDownloads = new();

    static ImageCacheService()
    {
        if (!Directory.Exists(CacheDir))
        {
            Directory.CreateDirectory(CacheDir);
        }
    }

    public static string? GetCachedPath(string url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        string fileName = GetHash(url);
        string filePath = Path.Combine(CacheDir, fileName);
        return File.Exists(filePath) ? filePath : null;
    }

    public static Task<string?> GetCachedImageAsync(string url)
    {
        if (string.IsNullOrEmpty(url)) return Task.FromResult<string?>(null);

        return _activeDownloads.GetOrAdd(url, async (u) =>
        {
            try
            {
                string? existing = GetCachedPath(u);
                if (existing != null) return existing;

                string fileName = GetHash(u);
                string filePath = Path.Combine(CacheDir, fileName);

                var response = await HttpClientManager.Instance.GetAsync(u);
                if (response.IsSuccessStatusCode)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(filePath, bytes);
                    return filePath;
                }
            }
            catch (Exception ex)
            {
                ConsoleService.Instance.Log($"[ImageCache] Failed to cache image from {u}: {ex.Message}");
            }
            finally
            {
                _activeDownloads.TryRemove(u, out _);
            }

            return null;
        });
    }

    private static string GetHash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }
}
