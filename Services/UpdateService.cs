using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using JustLauncher.Models;

namespace JustLauncher.Services;

public class UpdateService
{
    private const string GITHUB_API_URL = "https://api.github.com/repos/PinkLittleKitty/JustLauncher/releases/latest";

    public UpdateService()
    {
        // HttpClient is now managed by HttpClientManager singleton
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync(bool force = false)
    {
        try
        {
            var settings = ConfigManager.LoadSettings();
            var now = DateTime.UtcNow;
            
            if (!force && settings.LastUpdateCheck.Date == now.Date)
            {
                ConsoleService.Instance.Log("[Update] Skipping check - already checked today");
                return null;
            }

            ConsoleService.Instance.Log($"[Update] Checking for updates from GitHub...");
            var response = await HttpClientManager.Instance.GetAsync(GITHUB_API_URL);
            
            if (!response.IsSuccessStatusCode)
            {
                ConsoleService.Instance.Log($"[Update] GitHub API request failed: {response.StatusCode}");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var release = JsonSerializer.Deserialize<GitHubRelease>(content);

            if (release == null)
            {
                ConsoleService.Instance.Log("[Update] Failed to deserialize GitHub release");
                return null;
            }

            settings.LastUpdateCheck = now;
            ConfigManager.SaveSettings(settings);

            var latestVersion = release.TagName.TrimStart('v');
            var currentVersion = AppVersion.Version;

            ConsoleService.Instance.Log($"[Update] Current version: {currentVersion}, Latest version: {latestVersion}");

            var isNewer = CompareVersions(latestVersion, currentVersion);

            if (!force && isNewer && settings.SkippedVersion == latestVersion)
            {
                ConsoleService.Instance.Log($"[Update] Update {latestVersion} was previously skipped by user");
                return null;
            }

            if (isNewer)
            {
                ConsoleService.Instance.Log($"[Update] New version available: {latestVersion}");
            }
            else
            {
                ConsoleService.Instance.Log("[Update] Already on latest version");
            }

            return new UpdateInfo
            {
                Version = latestVersion,
                Changelog = release.Body ?? string.Empty,
                DownloadUrl = release.Assets?.Length > 0 ? release.Assets[0].BrowserDownloadUrl : release.HtmlUrl,
                PublishedAt = release.PublishedAt,
                IsNewer = isNewer,
                HtmlUrl = release.HtmlUrl
            };
        }
        catch (Exception ex)
        {
            ConsoleService.Instance.Log($"[Update] Error checking for updates: {ex.Message}");
            return null;
        }
    }

    private bool CompareVersions(string latest, string current)
    {
        try
        {
            var latestParts = latest.Split('.');
            var currentParts = current.Split('.');

            for (int i = 0; i < Math.Min(latestParts.Length, currentParts.Length); i++)
            {
                if (int.TryParse(latestParts[i], out int latestNum) && 
                    int.TryParse(currentParts[i], out int currentNum))
                {
                    if (latestNum > currentNum) return true;
                    if (latestNum < currentNum) return false;
                }
            }

            return latestParts.Length > currentParts.Length;
        }
        catch
        {
            return false;
        }
    }


    private class GitHubRelease
    {
        [System.Text.Json.Serialization.JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;
        
        [System.Text.Json.Serialization.JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;
        
        [System.Text.Json.Serialization.JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;
        
        [System.Text.Json.Serialization.JsonPropertyName("published_at")]
        public DateTime PublishedAt { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("assets")]
        public GitHubAsset[]? Assets { get; set; }
    }

    private class GitHubAsset
    {
        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [System.Text.Json.Serialization.JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;
    }
}
