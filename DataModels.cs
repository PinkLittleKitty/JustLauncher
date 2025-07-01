using System;
using System.Collections.Generic;
using System.Text.Json;

namespace JustLauncher
{
    public class VersionManifest
    {
        public List<MinecraftVersion> versions { get; set; }
    }

    public class MinecraftVersion
    {
        public string id { get; set; }
        public string type { get; set; }
        public string url { get; set; }
        public DateTime releaseTime { get; set; }
    }

    public class VersionInfo
    {
        public string mainClass { get; set; }
        public string type { get; set; }
        public string inheritsFrom { get; set; }
        public string minecraftArguments { get; set; }
        public Arguments arguments { get; set; }
        public AssetIndex assetIndex { get; set; }
        public List<Library> libraries { get; set; }
        public Downloads downloads { get; set; }
    }

    public class Arguments
    {
        public List<object> game { get; set; }
        public List<object> jvm { get; set; }
    }

    public class ConditionalArgument
    {
        public List<Rule> rules { get; set; }
        public object value { get; set; }
    }

    public class AssetIndex
    {
        public string id { get; set; }
        public string url { get; set; }
    }

    public class Library
    {
        public string name { get; set; }
        public LibraryDownloads downloads { get; set; }
        public List<Rule> rules { get; set; }
    }

    public class LibraryDownloads
    {
        public Artifact artifact { get; set; }
        public Classifiers classifiers { get; set; }
    }

    public class Classifiers
    {
        public Artifact natives_windows { get; set; }
    }

    public class Artifact
    {
        public string path { get; set; }
        public string url { get; set; }
    }

    public class Rule
    {
        public string action { get; set; }
        public Os os { get; set; }
    }

    public class Os
    {
        public string name { get; set; }
    }

    public class Downloads
    {
        public ClientDownload client { get; set; }
    }

    public class ClientDownload
    {
        public string url { get; set; }
    }

    public class AssetIndexInfo
    {
        public Dictionary<string, AssetObject> objects { get; set; }
    }

    public class AssetObject
    {
        public string hash { get; set; }
        public int size { get; set; }
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
        public List<GameProfile> profiles { get; set; }
    }
}