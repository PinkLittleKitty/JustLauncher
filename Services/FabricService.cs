using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace JustLauncher.Services
{
    public class FabricService
    {
        private const string META_URL = "https://meta.fabricmc.net/v2";
        private readonly HttpClient _httpClient = new();

        public async Task<List<FabricGameVersion>> GetGameVersionsAsync()
        {
            try
            {
                var response = await _httpClient.GetStringAsync($"{META_URL}/versions/game");
                return JsonSerializer.Deserialize<List<FabricGameVersion>>(response) ?? new List<FabricGameVersion>();
            }
            catch { return new List<FabricGameVersion>(); }
        }

        public async Task<List<FabricLoaderVersion>> GetLoaderVersionsAsync(string gameVersion)
        {
            try
            {
                var response = await _httpClient.GetStringAsync($"{META_URL}/versions/loader/{gameVersion}");
                return JsonSerializer.Deserialize<List<FabricLoaderVersion>>(response) ?? new List<FabricLoaderVersion>();
            }
            catch { return new List<FabricLoaderVersion>(); }
        }

        public async Task<string?> InstallFabricAsync(string gameVersion, string loaderVersion)
        {
            try
            {
                string url = $"{META_URL}/versions/loader/{gameVersion}/{loaderVersion}/profile/json";
                var json = await _httpClient.GetStringAsync(url);
                
                string versionId = $"fabric-loader-{loaderVersion}-{gameVersion}";
                string versionsDir = Path.Combine(PlatformManager.GetMinecraftDirectory(), "versions", versionId);
                
                if (!Directory.Exists(versionsDir)) Directory.CreateDirectory(versionsDir);
                
                string targetJsonPath = Path.Combine(versionsDir, $"{versionId}.json");
                await File.WriteAllTextAsync(targetJsonPath, json);
                
                return versionId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fabric install failed: {ex}");
                return null;
            }
        }
    }

    public class FabricGameVersion
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = default!;
        [JsonPropertyName("stable")]
        public bool Stable { get; set; }
    }

    public class FabricLoaderVersion
    {
        [JsonPropertyName("loader")]
        public FabricLoaderDetails Loader { get; set; } = new();
    }

    public class FabricLoaderDetails
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = default!;
        [JsonPropertyName("stable")]
        public bool Stable { get; set; }
    }
}
