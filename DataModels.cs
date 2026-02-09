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
        [JsonPropertyName("downloads")]
        public LibraryDownloads Downloads { get; set; } = new();
        [JsonPropertyName("rules")]
        public List<Rule> Rules { get; set; } = new();
        [JsonPropertyName("natives")]
        public Dictionary<string, string>? Natives { get; set; }

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
        public string JavaArgs { get; set; } = "-Xmx2G -Xms1G";
        public double MemoryAllocationGb { get; set; } = 4.0;
        public string Icon { get; set; } = "grass_block";
        public DateTime Created { get; set; } = DateTime.Now;
        public DateTime LastPlayed { get; set; }
        public bool IsModded { get; set; }
        public string ModLoader { get; set; } = default!;
        public string ModLoaderVersion { get; set; } = default!;
        public string BaseVersion { get; set; } = default!;
        public int PlayTime { get; set; }
        public bool IsInstalled { get; set; }
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
    }

    public class AccountsConfig
    {
        public List<Account> Accounts { get; set; } = new();
        public string SelectedAccountId { get; set; } = default!;
    }
}