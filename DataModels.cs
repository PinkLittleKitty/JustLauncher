using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JustLauncher
{
    public class VersionManifest
    {
        [JsonPropertyName("versions")]
        public List<MinecraftVersion> Versions { get; set; } = new();
    }

    public class MinecraftVersion
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = default!;
        [JsonPropertyName("type")]
        public string Type { get; set; } = default!;
        [JsonPropertyName("url")]
        public string Url { get; set; } = default!;
        [JsonPropertyName("releaseTime")]
        public DateTime ReleaseTime { get; set; }
    }

    public class VersionInfo
    {
        [JsonPropertyName("mainClass")]
        public string MainClass { get; set; } = default!;
        [JsonPropertyName("type")]
        public string Type { get; set; } = default!;
        [JsonPropertyName("inheritsFrom")]
        public string InheritsFrom { get; set; } = default!;
        [JsonPropertyName("minecraftArguments")]
        public string MinecraftArguments { get; set; } = default!;
        [JsonPropertyName("arguments")]
        public Arguments Arguments { get; set; } = new();
        [JsonPropertyName("assetIndex")]
        public AssetIndex AssetIndex { get; set; } = new();
        [JsonPropertyName("libraries")]
        public List<Library> Libraries { get; set; } = new();
        [JsonPropertyName("downloads")]
        public VersionDownloads Downloads { get; set; } = new();
        [JsonPropertyName("javaVersion")]
        public JavaVersion JavaVersion { get; set; } = new();
    }

    public class JavaVersion
    {
        [JsonPropertyName("component")]
        public string Component { get; set; } = default!;
        [JsonPropertyName("majorVersion")]
        public int MajorVersion { get; set; }
    }

    public class Arguments
    {
        [JsonPropertyName("game")]
        public List<object> Game { get; set; } = new();
        [JsonPropertyName("jvm")]
        public List<object> Jvm { get; set; } = new();
    }

    public class ConditionalArgument
    {
        public List<Rule> Rules { get; set; } = new();
        public object Value { get; set; } = default!;
    }

    public class AssetIndex
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = default!;
        [JsonPropertyName("url")]
        public string Url { get; set; } = default!;
    }

    public class Library
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = default!;
        [JsonPropertyName("url")]
        public string? Url { get; set; }
        [JsonPropertyName("downloads")]
        public LibraryDownloads Downloads { get; set; } = new();
        [JsonPropertyName("rules")]
        public List<Rule> Rules { get; set; } = new();
        [JsonPropertyName("natives")]
        public Dictionary<string, string>? Natives { get; set; }

        public string GetPath()
        {
            if (Downloads?.Artifact != null && !string.IsNullOrEmpty(Downloads.Artifact.Path))
                return Downloads.Artifact.Path;

            var parts = Name.Split(':');
            if (parts.Length < 3) return Name.Replace(':', '/');

            var group = parts[0].Replace('.', '/');
            var artifact = parts[1];
            var version = parts[2];
            var classifier = parts.Length > 3 ? "-" + parts[3] : "";

            return $"{group}/{artifact}/{version}/{artifact}-{version}{classifier}.jar";
        }

        public bool IsAllowed(string currentOs)
        {
            if (Rules == null || Rules.Count == 0) return true;
            bool allow = false;
            foreach (var rule in Rules)
            {
                if (rule.Action == "allow")
                {
                    if (rule.Os == null || rule.Os.Name == null || rule.Os.Name == currentOs) allow = true;
                }
                else if (rule.Action == "disallow")
                {
                    if (rule.Os != null && rule.Os.Name == currentOs) allow = false;
                }
            }
            return allow;
        }
    }

    public class LibraryDownloads
    {
        [JsonPropertyName("artifact")]
        public Artifact Artifact { get; set; } = new();
        [JsonPropertyName("classifiers")]
        public Classifiers Classifiers { get; set; } = new();
    }

    public class Classifiers : Dictionary<string, Artifact>
    {
    }

    public class Artifact
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = default!;
        [JsonPropertyName("url")]
        public string Url { get; set; } = default!;
    }

    public class Rule
    {
        [JsonPropertyName("action")]
        public string Action { get; set; } = default!;
        [JsonPropertyName("os")]
        public Os? Os { get; set; }
        [JsonPropertyName("features")]
        public Dictionary<string, bool>? Features { get; set; }
    }

    public class Os
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    public class VersionDownloads
    {
        [JsonPropertyName("client")]
        public ClientDownload Client { get; set; } = new();
    }

    public class ClientDownload
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = default!;
    }

    public class AssetIndexInfo
    {
        public Dictionary<string, AssetObject> Objects { get; set; } = new();
    }

    public class AssetObject
    {
        [JsonPropertyName("hash")]
        public string Hash { get; set; } = default!;
        [JsonPropertyName("size")]
        public int Size { get; set; }
    }

    public class VersionListItem
    {
        public MinecraftVersion Version { get; set; } = new();
        public string DisplayText { get; set; } = default!;
        public bool IsInstalled { get; set; }

        public override string ToString() => DisplayText;
    }

    public class ProfilesConfig
    {
        public List<GameProfile> Profiles { get; set; } = new();
    }

    public class GameProfile
    {
        public string Name { get; set; } = default!;
        public string Directory { get; set; } = default!;
        public string GameDirectory { get; set; } = default!;
        public string LastUsedUsername { get; set; } = default!;
        public string JavaArgs { get; set; } = default!;
    }

    public class Installation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = default!;
        public string Version { get; set; } = default!;
        public string GameDirectory { get; set; } = default!;
        public string JavaPath { get; set; } = default!;
        public string JavaArgs { get; set; } = "";
        public double MemoryAllocationGb { get; set; } = 0; // 0 means use global setting
        public string Icon { get; set; } = "grass_block";
        public DateTime Created { get; set; } = DateTime.Now;
        public DateTime LastPlayed { get; set; }
        public bool IsModded { get; set; }
        public ModLoaderType LoaderType { get; set; } = ModLoaderType.Vanilla;
        public string? ModLoaderVersion { get; set; }
        public string BaseVersion { get; set; } = default!;
        public int PlayTime { get; set; }
        public bool IsInstalled { get; set; }
    }

    public enum ModLoaderType
    {
        Vanilla,
        Fabric,
        Forge,
        NeoForge
    }

    public class InstallationsConfig
    {
        public List<Installation> Installations { get; set; } = new();
        public string SelectedInstallationId { get; set; } = default!;
    }

    public class AssetManifest
    {
        [JsonPropertyName("objects")]
        public Dictionary<string, AssetObject> Objects { get; set; } = new();
    }

    public class Account
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Username { get; set; } = default!;
        public string AccountType { get; set; } = "Offline";
        public string Email { get; set; } = default!;
        public DateTime LastUsed { get; set; } = DateTime.Now;
        public bool IsActive { get; set; }
        public string SkinUrl { get; set; } = default!;
        public DateTime Created { get; set; } = DateTime.Now;

        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string? Xuid { get; set; }

        public string GetAvatarUrl(int size = 64)
        {
            if (AccountType == "ElyBy")
            {
                return $"http://skinsystem.ely.by/skins/{Username}.png";
            }
            return $"https://minotar.net/avatar/{Username}/{size}";
        }
    }

    public class AccountsConfig
    {
        public List<Account> Accounts { get; set; } = new();
        public string SelectedAccountId { get; set; } = default!;
    }

    public class ModInfo : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isInstalled;
        private bool _isEnabled;
        private double _downloadProgress;
        private bool _isDownloading;
        private bool _updateAvailable;
        private string? _remoteVersion;
        private string? _updateUrl;

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public void NotifyIconChanged()
        {
            OnPropertyChanged(nameof(IconPath));
        }

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

        public string FileName { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string Version { get; set; } = default!;
        public string Description { get; set; } = default!;
        public string Authors { get; set; } = default!;
        public string Path { get; set; } = default!;
        private string? _iconPath;
        public string? IconPath 
        { 
            get => _iconPath; 
            set { _iconPath = value; OnPropertyChanged(); } 
        }

        public string? ProjectId { get; set; }
        public string? DownloadUrl { get; set; }

        public bool IsEnabled 
        { 
            get => _isEnabled; 
            set { _isEnabled = value; OnPropertyChanged(); } 
        }

        public bool IsInstalled 
        { 
            get => _isInstalled; 
            set { _isInstalled = value; OnPropertyChanged(); } 
        }

        public double DownloadProgress
        {
            get => _downloadProgress;
            set { _downloadProgress = value; OnPropertyChanged(); }
        }

        public bool IsDownloading
        {
            get => _isDownloading;
            set { _isDownloading = value; OnPropertyChanged(); }
        }

        public bool UpdateAvailable
        {
            get => _updateAvailable;
            set { _updateAvailable = value; OnPropertyChanged(); }
        }

        public string? RemoteVersion
        {
            get => _remoteVersion;
            set { _remoteVersion = value; OnPropertyChanged(); }
        }

        public string? UpdateUrl
        {
            get => _updateUrl;
            set { _updateUrl = value; OnPropertyChanged(); }
        }
    }

    public class ModrinthSearchResult
    {
        [JsonPropertyName("hits")]
        public List<ModrinthHit> Hits { get; set; } = new();
    }

    public class ModrinthHit
    {
        [JsonPropertyName("project_id")]
        public string ProjectId { get; set; } = default!;
        [JsonPropertyName("title")]
        public string Title { get; set; } = default!;
        [JsonPropertyName("description")]
        public string Description { get; set; } = default!;
        [JsonPropertyName("author")]
        public string Author { get; set; } = default!;
        [JsonPropertyName("icon_url")]
        public string IconUrl { get; set; } = default!;
        [JsonPropertyName("versions")]
        public List<string> Versions { get; set; } = new();
    }

    public class ModrinthVersion
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = default!;
        [JsonPropertyName("project_id")]
        public string ProjectId { get; set; } = default!;
        [JsonPropertyName("version_number")]
        public string VersionNumber { get; set; } = default!;
        [JsonPropertyName("files")]
        public List<ModrinthFile> Files { get; set; } = new();
    }

    public class ModrinthFile
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = default!;
        [JsonPropertyName("filename")]
        public string Filename { get; set; } = default!;
        [JsonPropertyName("primary")]
        public bool Primary { get; set; }
    }

    public class CurseForgeSearchResult
    {
        [JsonPropertyName("data")]
        public List<CurseForgeMod> Data { get; set; } = new();
    }

    public class CurseForgeFilesResponse
    {
        [JsonPropertyName("data")]
        public List<CurseForgeFile> Data { get; set; } = new();
    }

    public class CurseForgeMod
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; } = default!;
        [JsonPropertyName("summary")]
        public string Summary { get; set; } = default!;
        [JsonPropertyName("authors")]
        public List<CurseForgeAuthor> Authors { get; set; } = new();
        [JsonPropertyName("logo")]
        public CurseForgeLogo? Logo { get; set; }
        [JsonPropertyName("latestFiles")]
        public List<CurseForgeFile> LatestFiles { get; set; } = new();
    }

    public class CurseForgeAuthor
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = default!;
    }

    public class CurseForgeLogo
    {
        [JsonPropertyName("thumbnailUrl")]
        public string ThumbnailUrl { get; set; } = default!;
    }

    public class CurseForgeFile
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = default!;
        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = default!;
        [JsonPropertyName("downloadUrl")]
        public string DownloadUrl { get; set; } = default!;
        [JsonPropertyName("gameVersions")]
        public List<string> GameVersions { get; set; } = new();
        [JsonPropertyName("modLoader")]
        public int? ModLoader { get; set; }
    }
}