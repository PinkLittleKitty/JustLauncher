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
    private const string BaseUrl = "https://api.curseforge.com/v1";
    private const string ApiKey = "$2a$10$89Mof6FSnm86q.OshInatue4V.Lz5aT.6Vf9V.9V.9V.9V.9V.9V"; 

    public CurseForgeService()
    {
        // Note: CurseForge API key needs to be set per-request as it's service-specific
        // HttpClient is now managed by HttpClientManager singleton
    }

    private async Task<string> GetWithApiKeyAsync(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("x-api-key", ApiKey);
        var response = await HttpClientManager.Instance.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<List<ModInfo>> SearchModsAsync(string query, string minecraftVersion, string loader)
    {
        int modLoaderType = loader.Equals("Forge", StringComparison.OrdinalIgnoreCase) ? 1 : 4;
        string versionParam = string.IsNullOrEmpty(minecraftVersion) ? "" : $"&gameVersion={minecraftVersion}";
        string url = $"{BaseUrl}/mods/search?gameId=432{versionParam}&modLoaderType={modLoaderType}&searchFilter={Uri.EscapeDataString(query)}&classId=6";

        try
        {
            ConsoleService.Instance.Log($"[CurseForge] Searching for '{query}' (Version: {minecraftVersion}, Loader: {loader})");
            string json = await GetWithApiKeyAsync(url);
            var result = JsonSerializer.Deserialize<CurseForgeSearchResult>(json);
            
            if (result == null)
            {
                ConsoleService.Instance.Log("[CurseForge] No results found (null response)");
                return new List<ModInfo>();
            }

            ConsoleService.Instance.Log($"[CurseForge] Found {result.Data.Count} hits");
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
            ConsoleService.Instance.Log($"[CurseForge] Fetching versions for {projectId} (Version: {minecraftVersion}, Loader: {loader})");
            string json = await GetWithApiKeyAsync(url);
            var result = JsonSerializer.Deserialize<CurseForgeFilesResponse>(json);
            
            if (result == null || result.Data.Count == 0)
            {
                ConsoleService.Instance.Log($"[CurseForge] No versions found for {projectId} on {minecraftVersion}");
                return null;
            }

            var latest = result.Data.FirstOrDefault();
            ConsoleService.Instance.Log($"[CurseForge] Found file: {latest?.DownloadUrl}");
            return latest?.DownloadUrl;
        }
        catch (Exception ex)
        {
            ConsoleService.Instance.Log($"[CurseForge] File error: {ex.Message}");
            return null;
        }
    }
}
