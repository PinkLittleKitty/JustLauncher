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
    public string Type { get; set; } = "Latest"; // Latest or Recommended
    
    public override string ToString() => $"{Type} ({ForgeVersionStr})";
}

public class ForgeService
{
    private class PromosRoot
    {
        [JsonPropertyName("promos")]
        public Dictionary<string, string> Promos { get; set; } = new();
    }

    private readonly HttpClient _httpClient = new();
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
            var json = await _httpClient.GetStringAsync(PromosUrl);
            var root = JsonSerializer.Deserialize<PromosRoot>(json);
            
            if (root != null && root.Promos != null)
            {
                if (root.Promos.TryGetValue($"{mcVersion}-recommended", out var rec))
                {
                    list.Add(new ForgeVersion { MCVersion = mcVersion, ForgeVersionStr = rec, Type = "Recommended" });
                }
                if (root.Promos.TryGetValue($"{mcVersion}-latest", out var lat))
                {
                    // Avoid duplicate if recommended == latest
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
        // 1. Download Installer
        string installerUrl = $"{MavenUrl}/{mcVersion}-{forgeVersion}/forge-{mcVersion}-{forgeVersion}-installer.jar";
        // Older versions might allow direct jar but 1.12+ use installer.
        // There is a slight nuance in URL naming for very old versions, but 1.12+ is standard.
        
        string installerPath = Path.Combine(Path.GetTempPath(), $"forge-{mcVersion}-{forgeVersion}-installer.jar");
        
        try
        {
            if (File.Exists(installerPath)) File.Delete(installerPath);
            
            var bytes = await _httpClient.GetByteArrayAsync(installerUrl);
            await File.WriteAllBytesAsync(installerPath, bytes);
            
            // 2. Run Installer
            string javaPath = "java";
            
            // Try to find a valid Java path if 'java' might not work
            var javas = await _javaManager.GetInstalledJavaVersionsAsync();
            if (javas.Count > 0)
            {
                // Prefer Java 17 for newer, 8 for older, but for installer just use the newest one to be safe?
                // Actually the installer is a jar, so it needs JRE.
                var best = javas.OrderByDescending(j => j.MajorVersion).FirstOrDefault();
                if (best != null) javaPath = best.Path;
            }
            
            var psi = new ProcessStartInfo
            {
                FileName = javaPath,
                Arguments = $"-jar \"{installerPath}\" --installClient \"{gameDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var process = Process.Start(psi);
            if (process == null) return null;
            
            await process.WaitForExitAsync();
            
            File.Delete(installerPath);
            
            if (process.ExitCode == 0)
            {
                // return the version ID.
                // It usually is {mcVersion}-forge-{forgeVersion}
                // Check versions folder
                var versionsDir = Path.Combine(gameDir, "versions");
                var id1 = $"{mcVersion}-forge-{forgeVersion}";
                var id2 = $"{mcVersion}-forge{forgeVersion}"; // older style
                var id3 = $"forge-{mcVersion}-{forgeVersion}"; // another style?

                // Scan for the folder that matches
                if (Directory.Exists(Path.Combine(versionsDir, id1))) return id1;
                if (Directory.Exists(Path.Combine(versionsDir, id2))) return id2;
                
                // Fallback scan
                // The installer creates a profile in launcher_profiles.json sometimes too.
                // But we are interested in the folder in versions/.
                
                // Search for directory containing "forge" and the version string
                foreach(var d in Directory.GetDirectories(versionsDir))
                {
                    var name = Path.GetFileName(d);
                    if (name.Contains("forge") && name.Contains(forgeVersion)) return name;
                }
                
                return id1; // Best guess
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error installing Forge: {ex.Message}");
        }
        
        return null;
    }
}
