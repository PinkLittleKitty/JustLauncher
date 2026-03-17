using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace JustLauncher.Services;

public class ModpackService
{
    private readonly string _minecraftDir;
    private readonly ModrinthService _modrinthService = new();
    private readonly CurseForgeService _curseForgeService = new();

    public ModpackService(string minecraftDir)
    {
        _minecraftDir = minecraftDir;
    }

    public async Task<Installation?> ImportModpackAsync(string zipPath, Action<string, double>? onProgress = null)
    {
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            
            var modrinthIndex = archive.GetEntry("modrinth.index.json");
            if (modrinthIndex != null)
            {
                return await ImportModrinthPackInfoAsync(archive, modrinthIndex, onProgress);
            }

            var curseForgeManifest = archive.GetEntry("manifest.json");
            if (curseForgeManifest != null)
            {
                return await ImportCurseForgePackInfoAsync(archive, curseForgeManifest, onProgress);
            }
        }
        catch (Exception ex)
        {
            ConsoleService.Instance.Log($"[Modpack] Import failed: {ex.Message}");
        }

        return null;
    }

    private async Task<Installation?> ImportModrinthPackInfoAsync(ZipArchive archive, ZipArchiveEntry entry, Action<string, double>? onProgress)
    {
        using var stream = entry.Open();
        var index = await JsonSerializer.DeserializeAsync<ModrinthModpack>(stream);
        if (index == null) return null;

        onProgress?.Invoke($"Importing Modrinth Pack: {index.Name}", 0);

        var installation = new Installation
        {
            Name = index.Name,
            BaseVersion = index.Dependencies["minecraft"]
        };

        if (index.Dependencies.ContainsKey("fabric-loader"))
        {
            installation.LoaderType = ModLoaderType.Fabric;
            installation.ModLoaderVersion = index.Dependencies["fabric-loader"];
            installation.IsModded = true;
        }
        else if (index.Dependencies.ContainsKey("forge"))
        {
            installation.LoaderType = ModLoaderType.Forge;
            installation.ModLoaderVersion = index.Dependencies["forge"];
            installation.IsModded = true;
        }

        string instanceDir = Path.Combine(_minecraftDir, "instances", index.Name.Replace(" ", "_"));
        installation.GameDirectory = instanceDir;
        if (!Directory.Exists(instanceDir)) Directory.CreateDirectory(instanceDir);

        int totalFiles = index.Files.Count;
        int currentFile = 0;

        foreach (var file in index.Files)
        {
            currentFile++;
            onProgress?.Invoke($"Downloading {file.Path}...", (double)currentFile / totalFiles * 100);

            string destPath = Path.Combine(instanceDir, file.Path);
            string? dir = Path.GetDirectoryName(destPath);
            if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            if (file.Downloads.Count > 0)
            {
                await MinecraftService.Instance.DownloadFileAsync(file.Downloads[0], destPath);
            }
        }

        onProgress?.Invoke("Applying overrides...", 100);
        ExtractOverrides(archive, "overrides", instanceDir);
        ExtractOverrides(archive, "client-overrides", instanceDir);

        return installation;
    }

    private async Task<Installation?> ImportCurseForgePackInfoAsync(ZipArchive archive, ZipArchiveEntry entry, Action<string, double>? onProgress)
    {
        using var stream = entry.Open();
        var manifest = await JsonSerializer.DeserializeAsync<CurseForgeModpack>(stream);
        if (manifest == null) return null;

        onProgress?.Invoke($"Importing CurseForge Pack: {manifest.Name}", 0);

        var installation = new Installation
        {
            Name = manifest.Name,
            BaseVersion = manifest.Minecraft.Version
        };

        var primaryLoader = manifest.Minecraft.ModLoaders.FirstOrDefault(l => l.Primary) ?? manifest.Minecraft.ModLoaders.FirstOrDefault();
        if (primaryLoader != null)
        {
            if (primaryLoader.Id.StartsWith("fabric-"))
            {
                installation.LoaderType = ModLoaderType.Fabric;
                installation.ModLoaderVersion = primaryLoader.Id.Replace("fabric-", "");
            }
            else if (primaryLoader.Id.StartsWith("forge-"))
            {
                installation.LoaderType = ModLoaderType.Forge;
                installation.ModLoaderVersion = primaryLoader.Id.Replace("forge-", "");
            }
            installation.IsModded = true;
        }

        string instanceDir = Path.Combine(_minecraftDir, "instances", manifest.Name.Replace(" ", "_"));
        installation.GameDirectory = instanceDir;
        if (!Directory.Exists(instanceDir)) Directory.CreateDirectory(instanceDir);

        int totalFiles = manifest.Files.Count;
        int currentFile = 0;

        foreach (var file in manifest.Files)
        {
            currentFile++;
            onProgress?.Invoke($"Fetching mod {file.ProjectId}...", (double)currentFile / totalFiles * 100);

            var cfFile = await _curseForgeService.GetFileAsync(file.ProjectId.ToString(), file.FileId.ToString());
            if (cfFile != null && !string.IsNullOrEmpty(cfFile.DownloadUrl))
            {
                string modsDir = Path.Combine(instanceDir, "mods");
                if (!Directory.Exists(modsDir)) Directory.CreateDirectory(modsDir);
                
                string destPath = Path.Combine(modsDir, cfFile.FileName);
                await MinecraftService.Instance.DownloadFileAsync(cfFile.DownloadUrl, destPath);
            }
        }

        onProgress?.Invoke("Applying overrides...", 100);
        ExtractOverrides(archive, manifest.Overrides, instanceDir);

        return installation;
    }

    private void ExtractOverrides(ZipArchive archive, string overrideFolder, string targetDir)
    {
        string prefix = overrideFolder + "/";
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.StartsWith(prefix) && !string.IsNullOrEmpty(entry.Name))
            {
                string relativePath = entry.FullName.Substring(prefix.Length);
                string destPath = Path.Combine(targetDir, relativePath);
                
                string? dir = Path.GetDirectoryName(destPath);
                if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                
                entry.ExtractToFile(destPath, true);
            }
        }
    }
}
