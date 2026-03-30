using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Linq;
using JustLauncher;

namespace JustLauncher.Services;

public class CurseForgeService
{
    private const string BaseUrl = "https://api.curseforge.com/v1";
    private static readonly string ApiKey = ConfigProvider.Get("CURSEFORGE_API_KEY") ?? ""; 

    public CurseForgeService() { }

    private async Task<string> GetWithApiKeyAsync(string url)
    {
        if (string.IsNullOrEmpty(ApiKey))
        {
            throw new Exception("CurseForge API Key is missing. Please set CURSEFORGE_API_KEY in your .env file.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("x-api-key", ApiKey);
        var response = await HttpClientManager.Instance.SendAsync(request);
        
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden || response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new Exception($"CurseForge API access denied ({response.StatusCode}). Your API key might be invalid or expired.");
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<List<ModInfo>> SearchModsAsync(string query, string minecraftVersion, string loader, int index = 0)
    {
        int modLoaderType = loader.Equals("Forge", StringComparison.OrdinalIgnoreCase) ? 1 : 4;
        string versionParam = string.IsNullOrEmpty(minecraftVersion) ? "" : $"&gameVersion={minecraftVersion}";
        string url = $"{BaseUrl}/mods/search?gameId=432{versionParam}&modLoaderType={modLoaderType}&searchFilter={Uri.EscapeDataString(query)}&classId=6&index={index}";

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

    public async Task<CurseForgeMod?> GetModAsync(string projectId)
    {
        string url = $"{BaseUrl}/mods/{projectId}";
        try
        {
            string json = await GetWithApiKeyAsync(url);
            var result = JsonSerializer.Deserialize<CurseForgeModResponse>(json);
            return result?.Data;
        }
        catch (Exception ex)
        {
            ConsoleService.Instance.Log($"[CurseForge] Mod lookup error ({projectId}): {ex.Message}");
            return null;
        }
    }

    public async Task<List<CurseForgeDependency>> GetDependenciesAsync(string projectId, string fileId)
    {
        string url = $"{BaseUrl}/mods/{projectId}/files/{fileId}";
        try
        {
            string json = await GetWithApiKeyAsync(url);
            var result = JsonSerializer.Deserialize<CurseForgeFileResponse>(json);
            return result?.Data?.Dependencies ?? new List<CurseForgeDependency>();
        }
        catch (Exception ex)
        {
            ConsoleService.Instance.Log($"[CurseForge] Dependency lookup error ({projectId}/{fileId}): {ex.Message}");
            return new List<CurseForgeDependency>();
        }
    }

    public async Task<CurseForgeFile?> GetFileAsync(string projectId, string fileId)
    {
        string url = $"{BaseUrl}/mods/{projectId}/files/{fileId}";
        try
        {
            string json = await GetWithApiKeyAsync(url);
            var result = JsonSerializer.Deserialize<CurseForgeFileResponse>(json);
            return result?.Data;
        }
        catch (Exception ex)
        {
            ConsoleService.Instance.Log($"[CurseForge] File lookup error ({projectId}/{fileId}): {ex.Message}");
            return null;
        }
    }
}

public class CurseForgeFileResponse
{
    [JsonPropertyName("data")]
    public CurseForgeFile Data { get; set; } = default!;
}

public class CurseForgeDependency
{
    [JsonPropertyName("modId")]
    public int ModId { get; set; }
    [JsonPropertyName("relationType")]
    public int RelationType { get; set; }
}

public class CurseForgeModResponse
{
    [JsonPropertyName("data")]
    public CurseForgeMod Data { get; set; } = default!;
}
