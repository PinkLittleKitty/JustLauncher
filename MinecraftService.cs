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

    public async Task<VersionInfo> GetVersionInfoFromLocalAsync(string versionId)
    {
        string jsonPath = Path.Combine(_baseDir, "versions", versionId, $"{versionId}.json");
        if (!File.Exists(jsonPath)) throw new FileNotFoundException($"Version JSON not found: {jsonPath}");

        string json = await File.ReadAllTextAsync(jsonPath);
        var info = JsonSerializer.Deserialize<VersionInfo>(json) ?? new();

        if (!string.IsNullOrEmpty(info.InheritsFrom))
        {
            var manifest = await GetVersionManifestAsync();
            var parentVer = manifest.Versions.FirstOrDefault(v => v.Id == info.InheritsFrom);
            if (parentVer != null)
            {
                var parentInfo = await GetVersionInfoAsync(parentVer.Url);
                MergeVersionInfo(info, parentInfo);
            }
        }

        return info;
    }

    private void MergeVersionInfo(VersionInfo child, VersionInfo parent)
    {
        child.Libraries.AddRange(parent.Libraries);
        
        if (child.AssetIndex == null || string.IsNullOrEmpty(child.AssetIndex.Id))
        {
            child.AssetIndex = parent.AssetIndex;
        }

        if (child.Downloads == null || child.Downloads.Client == null || string.IsNullOrEmpty(child.Downloads.Client.Url))
        {
            child.Downloads = parent.Downloads;
        }

        if (child.JavaVersion == null || child.JavaVersion.MajorVersion == 0)
        {
            child.JavaVersion = parent.JavaVersion;
        }
        
        if (parent.Arguments != null)
        {
            if (child.Arguments == null) child.Arguments = new Arguments();
            child.Arguments.Game.InsertRange(0, parent.Arguments.Game);
            child.Arguments.Jvm.InsertRange(0, parent.Arguments.Jvm);
        }
    }

    public async Task DownloadFileAsync(string url, string path, Action<long, long>? progressCallback = null)
    {
        string? directory = Path.GetDirectoryName(path);
        if (directory != null && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

        string tempPath = path + ".tmp";
        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? -1L;
            using var source = await response.Content.ReadAsStreamAsync();
            using (var destination = File.Create(tempPath))
            {
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

            if (File.Exists(path)) File.Delete(path);
            File.Move(tempPath, path);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    public async Task DownloadVersionJarAsync(VersionInfo info, string versionId)
    {
        if (info.Downloads?.Client != null && !string.IsNullOrEmpty(info.Downloads.Client.Url))
        {
            string path = Path.Combine(_baseDir, "versions", versionId, $"{versionId}.jar");
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
            {
                ConsoleService.Instance.Log($"Downloading client jar for {versionId}...");
                await DownloadFileAsync(info.Downloads.Client.Url, path);
            }
        }
    }

    public async Task DownloadLibrariesAsync(VersionInfo info, Action<int, int>? progressCallback = null)
    {
        var currentOs = GetCurrentOsName();
        var libraries = info.Libraries.Where(lib => lib.IsAllowed(currentOs)).ToList();

        int completed = 0;
        foreach (var lib in libraries)
        {
            string libPath = lib.GetPath();
            string fullPath = Path.Combine(_baseDir, "libraries", libPath);

            if (!File.Exists(fullPath) || new FileInfo(fullPath).Length == 0)
            {
                string? url = null;
                if (lib.Downloads?.Artifact != null && !string.IsNullOrEmpty(lib.Downloads.Artifact.Url))
                {
                    url = lib.Downloads.Artifact.Url;
                }
                else if (!string.IsNullOrEmpty(lib.Url))
                {
                    url = lib.Url.TrimEnd('/') + "/" + libPath;
                }
                else
                {
                    if (lib.Name.Contains("forge") || lib.Name.Contains("minecraftforge"))
                    {
                        url = "https://maven.minecraftforge.net/" + libPath;
                    }
                    else
                    {
                        url = "https://libraries.minecraft.net/" + libPath;
                    }
                }

                if (url != null)
                {
                    try
                    {
                        await DownloadFileAsync(url, fullPath);
                    }
                    catch (Exception ex)
                    {
                        ConsoleService.Instance.Log($"[ERROR] Failed to download library {lib.Name}: {ex.Message}");
                    }
                }
            }

            if (lib.Natives != null && lib.Natives.TryGetValue(currentOs, out string? legacyClassifier))
            {
                if (lib.Downloads?.Classifiers != null && lib.Downloads.Classifiers.TryGetValue(legacyClassifier, out var nativeArtifact))
                {
                    string path = Path.Combine(_baseDir, "libraries", nativeArtifact.Path);
                    if (!File.Exists(path))
                    {
                        ConsoleService.Instance.Log($"Downloading legacy native: {lib.Name} ({legacyClassifier})");
                        await DownloadFileAsync(nativeArtifact.Url, path);
                    }
                }
            }

            if (lib.Downloads?.Classifiers != null)
            {
                foreach (var classifier in lib.Downloads.Classifiers)
                {
                    if (classifier.Key.Contains($"natives-{currentOs}") && PlatformManager.IsArchitectureMatch(classifier.Key, currentOs))
                    {
                        string path = Path.Combine(_baseDir, "libraries", classifier.Value.Path);
                        if (!File.Exists(path))
                        {
                            ConsoleService.Instance.Log($"Downloading modern native: {lib.Name} ({classifier.Key})");
                            await DownloadFileAsync(classifier.Value.Url, path);
                        }
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
        ConsoleService.Instance.Log($"Total libraries in version info: {info.Libraries.Count}");
        
        if (!Directory.Exists(nativesDir)) Directory.CreateDirectory(nativesDir);
        else 
        {
            foreach (var file in Directory.GetFiles(nativesDir)) File.Delete(file);
        }

        foreach (var lib in info.Libraries)
        {
            if (!lib.IsAllowed(currentOs)) continue;
            
            var candidates = new List<Artifact>();

            if (lib.Natives != null && lib.Natives.TryGetValue(currentOs, out string? classifier))
            {
                if (lib.Downloads.Classifiers != null && lib.Downloads.Classifiers.TryGetValue(classifier, out var nativeArtifact))
                {
                    candidates.Add(nativeArtifact);
                }
            }

            if (lib.Downloads.Classifiers != null)
            {
                foreach (var entry in lib.Downloads.Classifiers)
                {
                    if (entry.Key.Contains($"natives-{currentOs}") && PlatformManager.IsArchitectureMatch(entry.Key, currentOs))
                    {
                        candidates.Add(entry.Value);
                    }
                }
            }

            if (lib.Name.Contains($"natives-{currentOs}") && PlatformManager.IsArchitectureMatch(lib.Name, currentOs))
            {
                if (lib.Downloads.Artifact != null)
                {
                    candidates.Add(lib.Downloads.Artifact);
                }
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
                                if (entry.FullName.EndsWith(".so") || entry.FullName.EndsWith(".dll") || entry.FullName.EndsWith(".dylib"))
                                {
                                    string destPath = Path.Combine(nativesDir, entry.Name);
                                    entry.ExtractToFile(destPath, true);
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
                    ConsoleService.Instance.Log($"[WARNING] Native jar not found on disk: {artifact.Path}");
                }
            }
        }
    }

    public async Task DownloadAssetsAsync(VersionInfo info, Action<int, int>? progressCallback = null)
    {
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
            if (completed % 10 == 0 || completed == total)
            {
                progressCallback?.Invoke(completed, total);
            }
        }
    }

    public async Task<string> EnsureAuthlibInjectorAsync()
    {
        string toolsDir = Path.Combine(_baseDir, "tools");
        if (!Directory.Exists(toolsDir)) Directory.CreateDirectory(toolsDir);
        
        string injectorPath = Path.Combine(toolsDir, "authlib-injector.jar");
        if (!File.Exists(injectorPath))
        {
            ConsoleService.Instance.Log("Downloading authlib-injector...");
            string url = "https://github.com/yushijinhun/authlib-injector/releases/download/v1.2.7/authlib-injector-1.2.7.jar";
            await DownloadFileAsync(url, injectorPath);
        }
        
        return injectorPath;
    }

    private string GetCurrentOsName()
    {
        return PlatformManager.GetCurrentOsName();
    }
}
