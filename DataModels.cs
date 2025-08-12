using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JustLauncher
{
    public class VersionManifest
    {
        [JsonPropertyName("versions")]
        public List<MinecraftVersion> Versions { get; set; }
    }

    public class MinecraftVersion
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("type")]
        public string Type { get; set; }
        [JsonPropertyName("url")]
        public string Url { get; set; }
        [JsonPropertyName("releaseTime")]
        public DateTime ReleaseTime { get; set; }
    }

    public class VersionInfo
    {
        [JsonPropertyName("mainClass")]
        public string MainClass { get; set; }
        [JsonPropertyName("type")]
        public string Type { get; set; }
        [JsonPropertyName("inheritsFrom")]
        public string InheritsFrom { get; set; }
        [JsonPropertyName("minecraftArguments")]
        public string MinecraftArguments { get; set; }
        [JsonPropertyName("arguments")]
        public Arguments Arguments { get; set; }
        [JsonPropertyName("assetIndex")]
        public AssetIndex AssetIndex { get; set; }
        [JsonPropertyName("libraries")]
        public List<Library> Libraries { get; set; }
        [JsonPropertyName("downloads")]
        public Downloads Downloads { get; set; }
    }

    public class Arguments
    {
        [JsonPropertyName("game")]
        public List<object> Game { get; set; }
        [JsonPropertyName("jvm")]
        public List<object> Jvm { get; set; }
    }

    public class ConditionalArgument
    {
        public List<Rule> Rules { get; set; }
        public object Value { get; set; }
    }

    public class AssetIndex
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("url")]
        public string Url { get; set; }
    }

    public class Library
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("downloads")]
        public LibraryDownloads Downloads { get; set; }
        [JsonPropertyName("rules")]
        public List<Rule> Rules { get; set; }
    }

    public class LibraryDownloads
    {
        [JsonPropertyName("artifact")]
        public Artifact Artifact { get; set; }
        [JsonPropertyName("classifiers")]
        public Classifiers Classifiers { get; set; }
    }

    public class Classifiers
    {
        [JsonPropertyName("natives-windows")]
        public Artifact NativesWindows { get; set; }
        
        [JsonPropertyName("natives-windows-x86_64")]
        public Artifact NativesWindowsX64 { get; set; }
        
        [JsonPropertyName("natives-windows-x86")]
        public Artifact NativesWindowsX86 { get; set; }
        
        [JsonPropertyName("natives-linux")]
        public Artifact NativesLinux { get; set; }
        
        [JsonPropertyName("natives-linux-x86_64")]
        public Artifact NativesLinuxX64 { get; set; }
        
        [JsonPropertyName("natives-macos")]
        public Artifact NativesMacOS { get; set; }
        
        [JsonPropertyName("natives-macos-arm64")]
        public Artifact NativesMacOSArm64 { get; set; }
        
        [JsonExtensionData]
        public Dictionary<string, JsonElement> AdditionalClassifiers { get; set; }
    }

    public class Artifact
    {
        [JsonPropertyName("path")]
        public string Path { get; set; }
        [JsonPropertyName("url")]
        public string Url { get; set; }
    }

    public class Rule
    {
        public string Action { get; set; }
        public Os Os { get; set; }
    }

    public class Os
    {
        public string Name { get; set; }
    }

    public class Downloads
    {
        [JsonPropertyName("client")]
        public ClientDownload Client { get; set; }
    }

    public class ClientDownload
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }
    }

    public class AssetIndexInfo
    {
        public Dictionary<string, AssetObject> Objects { get; set; }
    }

    public class AssetObject
    {
        [JsonPropertyName("hash")]
        public string Hash { get; set; }
        [JsonPropertyName("size")]
        public int Size { get; set; }
    }

    public class VersionListItem
    {
        public MinecraftVersion Version { get; set; }
        public string DisplayText { get; set; }
        public bool IsInstalled { get; set; }

        public override string ToString()
        {
            return DisplayText;
        }
    }

    public class ProfilesConfig
    {
        public List<GameProfile> Profiles { get; set; }
    }

    public class GameProfile
    {
        public string Name { get; set; }
        public string Directory { get; set; }
        public string GameDirectory { get; set; }
        public string LastUsedUsername { get; set; }
        public string JavaArgs { get; set; }
    }

    public class Installation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Version { get; set; }
        public string GameDirectory { get; set; }
        public string JavaPath { get; set; }
        public string JavaArgs { get; set; } = "-Xmx2G -Xms1G";
        public string Icon { get; set; } = "grass_block";
        public DateTime Created { get; set; } = DateTime.Now;
        public DateTime LastPlayed { get; set; }
        public bool IsModded { get; set; }
        public string ModLoader { get; set; }
        public string ModLoaderVersion { get; set; }
        public string BaseVersion { get; set; }
        public int PlayTime { get; set; }
        public bool IsInstalled { get; set; }
    }

    public class InstallationsConfig
    {
        public List<Installation> Installations { get; set; } = new List<Installation>();
        public string SelectedInstallationId { get; set; }
    }

    public class AssetManifest
    {
        [JsonPropertyName("objects")]
        public Dictionary<string, AssetObject> Objects { get; set; }
    }
}