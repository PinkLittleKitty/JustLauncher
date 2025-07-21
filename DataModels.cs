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
        public List<Rule> Rules { get; set; } // Fixed IDE1006: Renamed 'rules' to 'Rules'
        public object Value { get; set; } // Fixed IDE1006: Renamed 'value' to 'Value'
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
        public Artifact NativesWindows { get; set; } // Fixed IDE1006: Renamed 'natives_windows' to 'NativesWindows'
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
        public string Action { get; set; } // Fixed IDE1006: Renamed 'action' to 'Action'
        public Os Os { get; set; } // Fixed IDE1006: Renamed 'os' to 'Os'
    }

    public class Os
    {
        public string Name { get; set; } // Fixed IDE1006: Renamed 'name' to 'Name'
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
        public Dictionary<string, AssetObject> Objects { get; set; } // Fixed IDE1006: Renamed 'objects' to 'Objects'
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
        public List<GameProfile> Profiles { get; set; } // Fixed IDE1006: Renamed 'profiles' to 'Profiles'
    }

    // Added missing GameProfile class to fix CS0246
    public class GameProfile
    {
        public string Name { get; set; }
        public string Directory { get; set; }
        public string GameDirectory { get; set; }
        public string LastUsedUsername { get; set; }
        public string JavaArgs { get; set; }
    }
}