using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text;

namespace JustLauncher;

public static class LaunchCommandBuilder
{
    public static List<string> BuildArguments(Installation installation, Account account, VersionInfo versionInfo, LauncherSettings settings)
    {
        var args = new List<string>();
        string mcDir = PlatformManager.GetMinecraftDirectory();
        string currentOs = PlatformManager.GetCurrentOsName();

        var placeholders = new Dictionary<string, string>
        {
            { "${auth_player_name}", account.Username },
            { "${version_name}", installation.Version },
            { "${game_directory}", installation.GameDirectory },
            { "${assets_root}", Path.Combine(mcDir, "assets") },
            { "${assets_index_name}", versionInfo.AssetIndex.Id },
            { "${auth_uuid}", account.Id.Replace("-", "") },
            { "${auth_access_token}", "0" },
            { "${user_type}", "legacy" },
            { "${version_type}", versionInfo.Type },
            { "${user_properties}", "{}" },
            { "${launcher_name}", "JustLauncher" },
            { "${launcher_version}", AppVersion.Version },
            { "${nativedir}", Path.Combine(mcDir, "versions", installation.Version, "natives") },
            { "${clientid}", "0" },
            { "${auth_xuid}", "0" },
            { "${resolution_width}", "854" },
            { "${resolution_height}", "480" },
            { "${quickPlayPath}", "" },
            { "${quickPlaySingleplayer}", "" },
            { "${quickPlayMultiplayer}", "" },
            { "${quickPlayRealms}", "" }
        };

        var classpath = new List<string>();
        foreach (var lib in versionInfo.Libraries)
        {
            if (!lib.IsAllowed(currentOs)) continue;

            string libPath = lib.GetPath();
            classpath.Add(Path.Combine(mcDir, "libraries", libPath));

            if (lib.Downloads?.Classifiers != null && lib.Natives != null && lib.Natives.TryGetValue(currentOs, out string? classifier))
            {
                if (lib.Downloads.Classifiers.TryGetValue(classifier, out var nativeArtifact))
                {
                    classpath.Add(Path.Combine(mcDir, "libraries", nativeArtifact.Path));
                }
            }
        }
        string jarVersion = installation.Version;
        if (!string.IsNullOrEmpty(installation.BaseVersion)) jarVersion = installation.BaseVersion;
        else if (!string.IsNullOrEmpty(versionInfo.InheritsFrom)) jarVersion = versionInfo.InheritsFrom;

        classpath.Add(Path.Combine(mcDir, "versions", jarVersion, jarVersion + ".jar"));
        placeholders["${classpath}"] = string.Join(Path.PathSeparator, classpath);

        if (versionInfo.Arguments?.Jvm != null && versionInfo.Arguments.Jvm.Count > 0)
        {
            ProcessArguments(versionInfo.Arguments.Jvm, args, placeholders, currentOs);
        }
        else
        {
            args.Add($"-Xmx{(int)installation.MemoryAllocationGb}G");
            args.Add($"-Xms{(int)installation.MemoryAllocationGb}G");
            args.Add("-Djava.library.path=" + placeholders["${nativedir}"]);
            args.Add("-cp");
            args.Add(placeholders["${classpath}"]);
        }

        if (!string.IsNullOrEmpty(installation.JavaArgs))
        {
            args.AddRange(SplitArguments(installation.JavaArgs));
        }

        args.Add(versionInfo.MainClass);

        if (versionInfo.Arguments?.Game != null && versionInfo.Arguments.Game.Count > 0)
        {
            ProcessArguments(versionInfo.Arguments.Game, args, placeholders, currentOs);
        }
        else if (!string.IsNullOrEmpty(versionInfo.MinecraftArguments))
        {
            string gameArgs = versionInfo.MinecraftArguments;
            foreach (var kv in placeholders)
            {
                gameArgs = gameArgs.Replace(kv.Key, kv.Value);
            }
            args.AddRange(SplitArguments(gameArgs));
        }
        else
        {
            args.Add("--username");
            args.Add(account.Username);
            args.Add("--version");
            args.Add(installation.Version);
            args.Add("--gameDir");
            args.Add(placeholders["${game_directory}"]);
            args.Add("--assetsDir");
            args.Add(placeholders["${assets_root}"]);
            args.Add("--assetIndex");
            args.Add(versionInfo.AssetIndex.Id);
            args.Add("--uuid");
            args.Add(placeholders["${auth_uuid}"]);
            args.Add("--accessToken");
            args.Add("0");
            args.Add("--userType");
            args.Add("legacy");
            args.Add("--versionType");
            args.Add(versionInfo.Type);
        }

        for (int i = 0; i < args.Count; i++)
        {
            args[i] = ReplaceAllPlaceholders(args[i], placeholders);
            
            if (args[i].Contains("${"))
            {
                int start;
                while ((start = args[i].IndexOf("${")) != -1)
                {
                    int end = args[i].IndexOf("}", start);
                    if (end != -1)
                    {
                        args[i] = args[i].Remove(start, end - start + 1).Insert(start, "0");
                    }
                    else break;
                }
            }
        }

        return args;
    }

    private static IEnumerable<string> SplitArguments(string args)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < args.Length; i++)
        {
            char c = args[i];
            if (c == '\"') inQuotes = !inQuotes;
            else if (c == ' ' && !inQuotes)
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
            }
            else current.Append(c);
        }

        if (current.Length > 0) result.Add(current.ToString());
        return result;
    }

    private static void ProcessArguments(List<object> source, List<string> target, Dictionary<string, string> placeholders, string currentOs)
    {
        foreach (var item in source)
        {
            if (item is string arg)
            {
                target.Add(arg);
            }
            else if (item is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.String)
                {
                    target.Add(element.GetString() ?? "");
                }
                else if (element.ValueKind == JsonValueKind.Object)
                {
                    if (element.TryGetProperty("rules", out var rulesNode))
                    {
                        var rules = JsonSerializer.Deserialize<List<Rule>>(rulesNode.GetRawText());
                        if (CheckRules(rules, currentOs))
                        {
                            if (element.TryGetProperty("value", out var valueNode))
                            {
                                if (valueNode.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var val in valueNode.EnumerateArray())
                                    {
                                        target.Add(val.GetString() ?? "");
                                    }
                                }
                                else
                                {
                                    target.Add(valueNode.GetString() ?? "");
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private static bool CheckRules(List<Rule>? rules, string currentOs)
    {
        if (rules == null || rules.Count == 0) return true;
        bool allow = false;
        foreach (var rule in rules)
        {
            bool matches = true;
            if (rule.Os != null && rule.Os.Name != null && rule.Os.Name != currentOs) matches = false;
            
            if (rule.Features != null)
            {
                foreach (var feature in rule.Features)
                {
                    if (feature.Value) { matches = false; break; }
                }
            }

            if (rule.Action == "allow")
            {
                if (matches) allow = true;
            }
            else if (rule.Action == "disallow")
            {
                if (matches) allow = false;
            }
        }
        return allow;
    }

    private static string ReplaceAllPlaceholders(string text, Dictionary<string, string> placeholders)
    {
        foreach (var kv in placeholders)
        {
            text = text.Replace(kv.Key, kv.Value);
        }
        return text;
    }

    private static string EscapePath(string path)
    {
        if (path.Contains(" ")) return "\"" + path + "\"";
        return path;
    }
}
