using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using JustLauncher;

namespace JustLauncher.Services;

public class ModrinthService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://api.modrinth.com/v2";

    public ModrinthService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "JustLauncher/1.0 (contact@example.com)");
    }

    public async Task<List<ModInfo>> SearchModsAsync(string query, string minecraftVersion, string loader)
    {
        string facets = $"[[\"versions:{minecraftVersion}\"],[\"categories:{loader.ToLower()}\"],[\"project_type:mod\"]]";
        string url = $"{BaseUrl}/search?query={Uri.EscapeDataString(query)}&facets={Uri.EscapeDataString(facets)}&limit=20";

        try
        {
            string json = await _httpClient.GetStringAsync(url);
            var result = JsonSerializer.Deserialize<ModrinthSearchResult>(json);
            
            if (result == null) return new List<ModInfo>();

            return result.Hits.Select(hit => new ModInfo
            {
                ProjectId = hit.ProjectId,
                Name = hit.Title,
                Authors = hit.Author,
                Description = hit.Description,
                IconPath = hit.IconUrl,
                IsEnabled = true
            }).ToList();
        }
        catch (Exception ex)
        {
            ConsoleService.Instance.Log($"[Modrinth] Search error: {ex.Message}");
            return new List<ModInfo>();
        }
    }

    public async Task<string?> GetDownloadUrlAsync(string projectId, string minecraftVersion, string loader)
    {
        string url = $"{BaseUrl}/project/{projectId}/version?loaders=[\"{loader.ToLower()}\"]&game_versions=[\"{minecraftVersion}\"]";

        try
        {
            string json = await _httpClient.GetStringAsync(url);
            var versions = JsonSerializer.Deserialize<List<ModrinthVersion>>(json);
            
            var latest = versions?.FirstOrDefault();
            var file = latest?.Files.FirstOrDefault(f => f.Primary) ?? latest?.Files.FirstOrDefault();
            
            return file?.Url;
        }
        catch (Exception ex)
        {
            ConsoleService.Instance.Log($"[Modrinth] Version error: {ex.Message}");
            return null;
        }
    }
}
