using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace JustLauncher.Services;

public class ModManagerService
{
    public async Task<List<ModInfo>> GetModsAsync(string modsDirectory)
    {
        if (!Directory.Exists(modsDirectory)) Directory.CreateDirectory(modsDirectory);
        var modsDir = modsDirectory;

        var mods = new List<ModInfo>();

        foreach (var file in Directory.GetFiles(modsDir))
        {
            var isEnabled = file.EndsWith(".jar", StringComparison.OrdinalIgnoreCase);
            var isDisabled = file.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);

            if (!isEnabled && !isDisabled) continue;

            var mod = await GetModAsync(file);
            if (mod != null)
            {
                mods.Add(mod);
            }
        }

        return mods;
    }

    public async Task<ModInfo?> GetModAsync(string file)
    {
        if (!File.Exists(file)) return null;

        var fileName = Path.GetFileName(file);
        var isEnabled = file.EndsWith(".jar", StringComparison.OrdinalIgnoreCase);
        
        var mod = new ModInfo
        {
            FileName = fileName,
            Path = file,
            Name = fileName,
            IsEnabled = isEnabled,
            IsInstalled = true
        };

        try
        {
            if (isEnabled)
            {
                using var archive = ZipFile.OpenRead(file);
                
                var fabricJson = archive.GetEntry("fabric.mod.json");
                if (fabricJson != null)
                {
                    using var stream = fabricJson.Open();
                    var meta = await JsonSerializer.DeserializeAsync<FabricModMetadata>(stream);
                    if (meta != null)
                    {
                        mod.Name = meta.Name ?? fileName;
                        mod.Version = meta.Version ?? "";
                        mod.Description = meta.Description ?? "";
                        
                        if (meta.Authors.ValueKind == JsonValueKind.Array)
                        {
                            var authors = new List<string>();
                            foreach (var element in meta.Authors.EnumerateArray())
                            {
                                if (element.ValueKind == JsonValueKind.String)
                                    authors.Add(element.GetString() ?? "");
                                else if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("name", out var nameProp))
                                    authors.Add(nameProp.GetString() ?? "");
                            }
                            mod.Authors = string.Join(", ", authors);
                        }
                        else if (meta.Authors.ValueKind == JsonValueKind.String)
                        {
                            mod.Authors = meta.Authors.GetString() ?? "";
                        }
                    }
                }
                else
                {
                    var forgeToml = archive.GetEntry("META-INF/mods.toml") ?? archive.GetEntry("META-INF/neoforge.mods.toml");
                    if (forgeToml != null)
                    {
                        using var reader = new StreamReader(forgeToml.Open());
                        string content = await reader.ReadToEndAsync();
                        
                        var nameMatch = System.Text.RegularExpressions.Regex.Match(content, @"displayName\s*=\s*""([^""]+)""");
                        var versionMatch = System.Text.RegularExpressions.Regex.Match(content, @"version\s*=\s*""([^""]+)""");
                        var descMatch = System.Text.RegularExpressions.Regex.Match(content, @"description\s*=\s*'''?([\s\S]*?)'''?|description\s*=\s*""([^""]+)""");
                        var authorsMatch = System.Text.RegularExpressions.Regex.Match(content, @"authors?\s*=\s*""([^""]+)""");

                        if (nameMatch.Success) mod.Name = nameMatch.Groups[1].Value;
                        if (versionMatch.Success) mod.Version = versionMatch.Groups[1].Value;
                        if (descMatch.Success) mod.Description = descMatch.Groups[1].Success ? descMatch.Groups[1].Value : descMatch.Groups[2].Value;
                        if (authorsMatch.Success) mod.Authors = authorsMatch.Groups[1].Value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading mod metadata for {fileName}: {ex.Message}");
        }

        return mod;
    }

    public async Task CheckForUpdatesAsync(List<ModInfo> mods, string mcVersion, string loader)
    {
        var modrinth = new ModrinthService();
        var curseForge = new CurseForgeService();
        
        foreach (var mod in mods)
        {
            if (!File.Exists(mod.Path)) continue;

            try
            {
                if (string.IsNullOrEmpty(mod.ProjectId))
                {
                    string hash;
                    using (var stream = File.OpenRead(mod.Path))
                    {
                        using (var sha1 = System.Security.Cryptography.SHA1.Create())
                        {
                            var hashBytes = sha1.ComputeHash(stream);
                            hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                        }
                    }

                    var currentVersion = await modrinth.GetVersionByHashAsync(hash);
                    if (currentVersion != null)
                    {
                        mod.ProjectId = currentVersion.ProjectId;
                    }
                }

                if (!string.IsNullOrEmpty(mod.ProjectId))
                {
                    if (string.IsNullOrEmpty(mod.IconPath))
                    {
                        if (mod.ProjectId.All(char.IsDigit))
                        {
                            var cfMod = await curseForge.GetModAsync(mod.ProjectId);
                            if (cfMod != null)
                            {
                                mod.IconPath = cfMod.Logo?.ThumbnailUrl;
                                if (string.IsNullOrEmpty(mod.Description)) mod.Description = cfMod.Summary;
                            }
                        }
                        else
                        {
                            var mrMod = await modrinth.GetProjectAsync(mod.ProjectId);
                            if (mrMod != null)
                            {
                                mod.IconPath = mrMod.IconUrl;
                                if (string.IsNullOrEmpty(mod.Description)) mod.Description = mrMod.Description;
                            }
                        }
                    }

                    var versions = await modrinth.GetVersionsAsync(mod.ProjectId, mcVersion, loader);
                    if (versions.Count > 0)
                    {
                        var latest = versions.FirstOrDefault();
                        if (latest != null && latest.VersionNumber != mod.Version)
                        {
                            var latestFile = latest.Files.FirstOrDefault(f => f.Primary) ?? latest.Files.FirstOrDefault();
                            if (latestFile != null)
                            {
                                mod.UpdateAvailable = true;
                                mod.RemoteVersion = latest.VersionNumber; 
                                mod.UpdateUrl = latestFile.Url;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleService.Instance.Log($"[Mods] Failed to check for updates for {mod.FileName}: {ex.Message}");
            }
        }
    }

    public void ToggleMod(ModInfo mod)
    {
        if (mod.IsEnabled)
        {
            var newPath = mod.Path + ".disabled";
            if (File.Exists(newPath)) File.Delete(newPath);
            File.Move(mod.Path, newPath);
            mod.Path = newPath;
            mod.FileName = Path.GetFileName(newPath);
            mod.IsEnabled = false;
        }
        else
        {
            if (mod.Path.EndsWith(".disabled"))
            {
                var newPath = mod.Path.Substring(0, mod.Path.Length - ".disabled".Length);
                if (File.Exists(newPath)) File.Delete(newPath);
                File.Move(mod.Path, newPath);
                mod.Path = newPath;
                mod.FileName = Path.GetFileName(newPath);
                mod.IsEnabled = true;
            }
        }
    }

    public async Task<List<ModInfo>> ResolveDependenciesAsync(string projectId, string mcVersion, string loader, List<ModInfo> installedMods)
    {
        var dependenciesToInstall = new List<ModInfo>();
        var queue = new Queue<string>();
        queue.Enqueue(projectId);

        var processed = new HashSet<string>();

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            if (processed.Contains(currentId)) continue;
            processed.Add(currentId);

            if (loader.Equals("Fabric", StringComparison.OrdinalIgnoreCase))
            {
                var modrinth = new ModrinthService();
                var versions = await modrinth.GetVersionsAsync(currentId, mcVersion, "fabric");
                var latest = versions.FirstOrDefault();
                if (latest != null)
                {
                    foreach (var dep in latest.Dependencies.Where(d => d.DependencyType == "required"))
                    {
                        if (dep.ProjectId != null && !installedMods.Any(m => m.ProjectId == dep.ProjectId) && !dependenciesToInstall.Any(m => m.ProjectId == dep.ProjectId))
                        {
                            var depProject = await modrinth.GetProjectAsync(dep.ProjectId);
                            if (depProject != null)
                            {
                                dependenciesToInstall.Add(new ModInfo
                                {
                                    ProjectId = dep.ProjectId,
                                    Name = depProject.Title,
                                    Description = depProject.Description,
                                    IsEnabled = true
                                });
                                queue.Enqueue(dep.ProjectId);
                            }
                        }
                    }
                }
            }
            else
            {
                var curseForge = new CurseForgeService();
                var cfMod = await curseForge.GetModAsync(currentId);
                if (cfMod != null)
                {
                    var latestFile = cfMod.LatestFiles.FirstOrDefault(f => f.GameVersions.Contains(mcVersion));
                    if (latestFile != null)
                    {
                        var fullFile = await curseForge.GetDependenciesAsync(currentId, latestFile.Id.ToString());
                        foreach (var dep in fullFile.Where(d => d.RelationType == 3))
                        {
                            string depId = dep.ModId.ToString();
                            if (!installedMods.Any(m => m.ProjectId == depId) && !dependenciesToInstall.Any(m => m.ProjectId == depId))
                            {
                                var depMod = await curseForge.GetModAsync(depId);
                                if (depMod != null)
                                {
                                    dependenciesToInstall.Add(new ModInfo
                                    {
                                        ProjectId = depId,
                                        Name = depMod.Name,
                                        Description = depMod.Summary,
                                        IsEnabled = true
                                    });
                                    queue.Enqueue(depId);
                                }
                            }
                        }
                    }
                }
            }
        }

        return dependenciesToInstall;
    }
}

public class FabricModMetadata
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("version")]
    public string? Version { get; set; }
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    [JsonPropertyName("authors")]
    public JsonElement Authors { get; set; }
}
