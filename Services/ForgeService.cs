using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace JustLauncher.Services;

public class ForgeVersion
{
    public string MCVersion { get; set; } = "";
    public string ForgeVersionStr { get; set; } = "";
    public string Type { get; set; } = "Latest";
    
    public override string ToString() => $"{Type} ({ForgeVersionStr})";
}

public class ForgeService
{
    private class PromosRoot
    {
        [JsonPropertyName("promos")]
        public Dictionary<string, string> Promos { get; set; } = new();
    }


    private readonly JavaManager _javaManager;
    private const string PromosUrl = "https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json";
    private const string MavenUrl = "https://maven.minecraftforge.net/net/minecraftforge/forge";

    public ForgeService()
    {
        _javaManager = new JavaManager();
    }

    public async Task<List<ForgeVersion>> GetForgeVersionsAsync(string mcVersion)
    {
        var list = new List<ForgeVersion>();
        try
        {
            var json = await HttpClientManager.Instance.GetStringAsync(PromosUrl);
            var root = JsonSerializer.Deserialize<PromosRoot>(json);
            
            if (root != null && root.Promos != null)
            {
                if (root.Promos.TryGetValue($"{mcVersion}-recommended", out var rec))
                {
                    list.Add(new ForgeVersion { MCVersion = mcVersion, ForgeVersionStr = rec, Type = "Recommended" });
                }
                if (root.Promos.TryGetValue($"{mcVersion}-latest", out var lat))
                {
                    if (list.Count == 0 || list[0].ForgeVersionStr != lat)
                    {
                        list.Add(new ForgeVersion { MCVersion = mcVersion, ForgeVersionStr = lat, Type = "Latest" });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching Forge versions: {ex.Message}");
        }
        return list;
    }

    public async Task<string?> InstallForgeAsync(string mcVersion, string forgeVersion, string gameDir)
    {
        string installerUrl = $"{MavenUrl}/{mcVersion}-{forgeVersion}/forge-{mcVersion}-{forgeVersion}-installer.jar";
        
        string installerPath = Path.Combine(Path.GetTempPath(), $"forge-{mcVersion}-{forgeVersion}-installer.jar");
        
        try
        {
            if (File.Exists(installerPath)) File.Delete(installerPath);
            
            var bytes = await HttpClientManager.Instance.GetByteArrayAsync(installerUrl);
            await File.WriteAllBytesAsync(installerPath, bytes);
            
            string javaPath = "java";
            string mcDir = PlatformManager.GetMinecraftDirectory();
            
            string profilesPath = Path.Combine(mcDir, "launcher_profiles.json");
            if (!File.Exists(profilesPath))
            {
                File.WriteAllText(profilesPath, "{ \"profiles\": {} }");
            }

            var javas = await _javaManager.GetInstalledJavaVersionsAsync();
            if (javas.Count > 0)
            {
                var best = javas.OrderByDescending(j => j.MajorVersion).FirstOrDefault();
                if (best != null) javaPath = best.Path;
            }
            
            var psi = new ProcessStartInfo
            {
                FileName = javaPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-jar");
            psi.ArgumentList.Add(installerPath);
            psi.ArgumentList.Add("--installClient");
            psi.ArgumentList.Add(mcDir);
            
            using var process = Process.Start(psi);
            if (process == null) return null;
            
            await process.WaitForExitAsync();
            
            File.Delete(installerPath);
            
            if (process.ExitCode == 0)
            {
                var versionsDir = Path.Combine(mcDir, "versions");
                var id1 = $"{mcVersion}-forge-{forgeVersion}";
                var id2 = $"{mcVersion}-forge{forgeVersion}";
                var id3 = $"forge-{mcVersion}-{forgeVersion}";

                if (Directory.Exists(Path.Combine(versionsDir, id1))) return id1;
                if (Directory.Exists(Path.Combine(versionsDir, id2))) return id2;
                
                foreach(var d in Directory.GetDirectories(versionsDir))
                {
                    var name = Path.GetFileName(d);
                    if (name.Contains("forge") && name.Contains(forgeVersion)) return name;
                }
                
                return id1;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error installing Forge: {ex.Message}");
        }
        
        return null;
    }
}
