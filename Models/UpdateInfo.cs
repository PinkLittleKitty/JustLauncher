using System;

namespace JustLauncher.Models;

public class UpdateInfo
{
    public string Version { get; set; } = string.Empty;
    public string Changelog { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public bool IsNewer { get; set; }
    public string HtmlUrl { get; set; } = string.Empty;
}
