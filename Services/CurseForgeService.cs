using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using JustLauncher;

namespace JustLauncher.Services;

public class CurseForgeService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://api.curseforge.com/v1";
    private const string ApiKey = "$2a$10$89Mof6FSnm86q.OshInatue4V.Lz5aT.6Vf9V.9V.9V.9V.9V.9V"; 

    public CurseForgeService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("x-api-key", ApiKey);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "JustLauncher/1.0");
    }

    public async Task<List<ModInfo>> SearchModsAsync(string query, string minecraftVersion, string loader)
    {
        int modLoaderType = loader.Equals("Forge", StringComparison.OrdinalIgnoreCase) ? 1 : 4;
        string url = $"{BaseUrl}/mods/search?gameId=432&gameVersion={minecraftVersion}&modLoaderType={modLoaderType}&searchFilter={Uri.EscapeDataString(query)}&classId=6";

        try
        {
            string json = await _httpClient.GetStringAsync(url);
            var result = JsonSerializer.Deserialize<CurseForgeSearchResult>(json);
            
            if (result == null) return new List<ModInfo>();

            return result.Data.Select(mod => new ModInfo
            {
                ProjectId = mod.Id.ToString(),
                Name = mod.Name,
                Authors = string.Join(", ", mod.Authors.Select(a => a.Name)),
                Description = mod.Summary,
                IconPath = mod.Logo?.ThumbnailUrl,
                IsEnabled = true
            }).ToList();
        }
        catch (Exception ex)
        {
            ConsoleService.Instance.Log($"[CurseForge] Search error: {ex.Message}");
            return new List<ModInfo>();
        }
    }

    public async Task<string?> GetDownloadUrlAsync(string projectId, string minecraftVersion, string loader)
    {
        int modLoaderType = loader.Equals("Forge", StringComparison.OrdinalIgnoreCase) ? 1 : 4;
        string url = $"{BaseUrl}/mods/{projectId}/files?gameVersion={minecraftVersion}&modLoaderType={modLoaderType}";

        try
        {
            string json = await _httpClient.GetStringAsync(url);
            var result = JsonSerializer.Deserialize<CurseForgeFilesResponse>(json);
            var latest = result?.Data.FirstOrDefault();
            return latest?.DownloadUrl;
        }
        catch (Exception ex)
        {
            ConsoleService.Instance.Log($"[CurseForge] File error: {ex.Message}");
            return null;
        }
    }
}
