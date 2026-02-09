using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.IO.Compression;

namespace JustLauncher;

public class MinecraftService
{
    private readonly HttpClient _httpClient;
    private const string ManifestUrl = "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json";
    private readonly string _baseDir;

    public MinecraftService() : this(PlatformManager.GetMinecraftDirectory())
    {
    }

    public MinecraftService(string baseDir)
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "JustLauncher/1.0");
        _baseDir = baseDir;
    }
    
    // For when we want to share the client
    public MinecraftService(string baseDir, HttpClient client)
    {
        _httpClient = client;
        _baseDir = baseDir;
    }

    public async Task<VersionManifest> GetVersionManifestAsync()
    {
        string json = await _httpClient.GetStringAsync(ManifestUrl);
        return JsonSerializer.Deserialize<VersionManifest>(json) ?? new();
    }

    public async Task<VersionInfo> GetVersionInfoAsync(string url)
    {
        string json = await _httpClient.GetStringAsync(url);
        return JsonSerializer.Deserialize<VersionInfo>(json) ?? new();
    }

    public async Task DownloadFileAsync(string url, string path, Action<long, long>? progressCallback = null)
    {
        string? directory = Path.GetDirectoryName(path);
        if (directory != null && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        long totalBytes = response.Content.Headers.ContentLength ?? -1L;
        using var source = await response.Content.ReadAsStreamAsync();
        using var destination = File.Create(path);

        byte[] buffer = new byte[8192];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            await destination.WriteAsync(buffer, 0, bytesRead);
            totalRead += bytesRead;
            progressCallback?.Invoke(totalRead, totalBytes);
        }
    }

    private bool IsLibraryAllowed(List<Rule> rules, string currentOs)
    {
        if (rules == null || rules.Count == 0) return true;
        bool allow = false;
        foreach (var rule in rules)
        {
            if (rule.Action == "allow")
            {
                if (rule.Os.Name == null || rule.Os.Name == currentOs) allow = true;
            }
            else if (rule.Action == "disallow")
            {
                if (rule.Os.Name == currentOs) allow = false;
            }
        }
        return allow;
    }

    public async Task DownloadLibrariesAsync(VersionInfo info, Action<int, int>? progressCallback = null)
    {
        var currentOs = GetCurrentOsName();
        var libraries = info.Libraries.Where(lib => IsLibraryAllowed(lib.Rules, currentOs)).ToList();

        int completed = 0;
        foreach (var lib in libraries)
        {
            // Download main artifact
            if (lib.Downloads.Artifact != null && !string.IsNullOrEmpty(lib.Downloads.Artifact.Url))
            {
                string path = Path.Combine(_baseDir, "libraries", lib.Downloads.Artifact.Path);
                if (!File.Exists(path))
                {
                    await DownloadFileAsync(lib.Downloads.Artifact.Url, path);
                }
            }

            // Download native artifact if applicable (legacy system)
            if (lib.Natives != null && lib.Natives.TryGetValue(currentOs, out string? classifier))
            {
                if (lib.Downloads.Classifiers != null && lib.Downloads.Classifiers.TryGetValue(classifier, out var nativeArtifact))
                {
                    string path = Path.Combine(_baseDir, "libraries", nativeArtifact.Path);
                    if (!File.Exists(path))
                    {
                        await DownloadFileAsync(nativeArtifact.Url, path);
                    }
                }
            }

            completed++;
            progressCallback?.Invoke(completed, libraries.Count);
        }
    }

    public async Task ExtractNativesAsync(VersionInfo info, string versionId)
    {
        var currentOs = GetCurrentOsName();
        string nativesDir = Path.Combine(_baseDir, "versions", versionId, "natives");
        ConsoleService.Instance.Log($"Extraction OS: {currentOs}");
        ConsoleService.Instance.Log($"Extracting natives to: {nativesDir}");
        
        if (!Directory.Exists(nativesDir)) Directory.CreateDirectory(nativesDir);
        else 
        {
            // Clear old natives to ensure clean state
            foreach (var file in Directory.GetFiles(nativesDir)) File.Delete(file);
        }

        foreach (var lib in info.Libraries)
        {
            if (!IsLibraryAllowed(lib.Rules, currentOs)) continue;

            var candidates = new List<Artifact>();

            // 1. Check legacy natives dictionary
            if (lib.Natives != null && lib.Natives.TryGetValue(currentOs, out string? classifier))
            {
                if (lib.Downloads.Classifiers != null && lib.Downloads.Classifiers.TryGetValue(classifier, out var nativeArtifact))
                {
                    candidates.Add(nativeArtifact);
                }
            }

            // 2. Check classifiers for "natives-linux" etc.
            if (lib.Downloads.Classifiers != null)
            {
                foreach (var entry in lib.Downloads.Classifiers)
                {
                    if (entry.Key.Contains($"natives-{currentOs}"))
                    {
                        candidates.Add(entry.Value);
                    }
                }
            }

            // 3. Modern style: the whole library IS the native (e.g., name contains natives-linux)
            if (lib.Name.Contains($"natives-{currentOs}") && lib.Downloads.Artifact != null)
            {
                candidates.Add(lib.Downloads.Artifact);
            }

            foreach (var artifact in candidates.Where(a => !string.IsNullOrEmpty(a.Url)).GroupBy(a => a.Path).Select(g => g.First()))
            {
                string jarPath = Path.Combine(_baseDir, "libraries", artifact.Path);
                if (File.Exists(jarPath))
                {
                    ConsoleService.Instance.Log($"Extracting from: {Path.GetFileName(jarPath)}");
                    try
                    {
                        using (var archive = ZipFile.OpenRead(jarPath))
                        {
                            foreach (var entry in archive.Entries)
                            {
                                // Only extract shared libraries
                                if (entry.FullName.EndsWith(".so") || entry.FullName.EndsWith(".dll") || entry.FullName.EndsWith(".dylib"))
                                {
                                    string destPath = Path.Combine(nativesDir, entry.Name);
                                    entry.ExtractToFile(destPath, true); // Overwrite if exists
                                    ConsoleService.Instance.Log($"  -> Extracted: {entry.Name}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ConsoleService.Instance.Log($"[ERROR] Failed to extract {Path.GetFileName(jarPath)}: {ex.Message}");
                    }
                }
                else
                {
                    // If it's a candidate but doesn't exist, we might have skipped downloading it?
                    // This is unexpected if IsLibraryAllowed is correct.
                    ConsoleService.Instance.Log($"[WARNING] Native candidate not found in libraries: {artifact.Path}");
                }
            }
        }
    }

    public async Task DownloadAssetsAsync(VersionInfo info, Action<int, int>? progressCallback = null)
    {
        // 1. Download and save the Asset Index file itself
        string indexPath = Path.Combine(_baseDir, "assets", "indexes", $"{info.AssetIndex.Id}.json");
        string indexJson;
        
        if (!File.Exists(indexPath))
        {
            indexJson = await _httpClient.GetStringAsync(info.AssetIndex.Url);
            string? indexDir = Path.GetDirectoryName(indexPath);
            if (indexDir != null && !Directory.Exists(indexDir)) Directory.CreateDirectory(indexDir);
            File.WriteAllText(indexPath, indexJson);
        }
        else
        {
            indexJson = File.ReadAllText(indexPath);
        }

        var manifest = JsonSerializer.Deserialize<AssetManifest>(indexJson);
        if (manifest == null) return;

        int completed = 0;
        int total = manifest.Objects.Count;

        foreach (var asset in manifest.Objects)
        {
            string hash = asset.Value.Hash;
            string prefix = hash.Substring(0, 2);
            string url = $"https://resources.download.minecraft.net/{prefix}/{hash}";
            string path = Path.Combine(_baseDir, "assets", "objects", prefix, hash);

            if (!File.Exists(path))
            {
                await DownloadFileAsync(url, path);
            }

            completed++;
            if (completed % 10 == 0 || completed == total) // Report progress every 10 files
            {
                progressCallback?.Invoke(completed, total);
            }
        }
    }

    private string GetCurrentOsName()
    {
        if (OperatingSystem.IsWindows()) return "windows";
        if (OperatingSystem.IsLinux()) return "linux";
        if (OperatingSystem.IsMacOS()) return "osx";
        return "windows";
    }
}
