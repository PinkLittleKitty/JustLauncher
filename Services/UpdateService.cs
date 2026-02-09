using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using JustLauncher.Models;

namespace JustLauncher.Services;

public class UpdateService
{
    private const string GITHUB_API_URL = "https://api.github.com/repos/PinkLittleKitty/JustLauncher/releases/latest";
    private readonly HttpClient _httpClient;

    public UpdateService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "JustLauncher");
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync(bool force = false)
    {
        try
        {
            var settings = ConfigManager.LoadSettings();
            var now = DateTime.UtcNow;
            
            if (!force && settings.LastUpdateCheck.Date == now.Date)
            {
                return null;
            }

            var response = await _httpClient.GetAsync(GITHUB_API_URL);
            
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var release = JsonSerializer.Deserialize<GitHubRelease>(content);

            if (release == null)
            {
                return null;
            }

            settings.LastUpdateCheck = now;
            ConfigManager.SaveSettings(settings);

            var latestVersion = release.TagName.TrimStart('v');
            var currentVersion = AppVersion.Version;

            var isNewer = CompareVersions(latestVersion, currentVersion);

            if (!force && isNewer && settings.SkippedVersion == latestVersion)
            {
                return null;
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
        catch (Exception)
        {
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
        public string TagName { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string HtmlUrl { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; }
        public GitHubAsset[]? Assets { get; set; }
    }

    private class GitHubAsset
    {
        public string Name { get; set; } = string.Empty;
        public string BrowserDownloadUrl { get; set; } = string.Empty;
    }
}
