using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace JustLauncher;

public static class LaunchCommandBuilder
{
    public static string BuildArguments(Installation installation, Account account, VersionInfo versionInfo, LauncherSettings settings)
    {
        var args = new List<string>();
        string mcDir = PlatformManager.GetMinecraftDirectory();

        args.Add($"-Xmx{(int)installation.MemoryAllocationGb}G");
        args.Add($"-Xms{(int)installation.MemoryAllocationGb}G");
        
        args.Add("-XX:+UseG1GC");
        args.Add("-XX:+ParallelRefProcEnabled");
        args.Add("-XX:MaxGCPauseMillis=200");
        args.Add("-XX:+UnlockExperimentalVMOptions");
        args.Add("-XX:+DisableExplicitGC");
        args.Add("-XX:+AlwaysPreTouch");
        args.Add("-XX:G1NewSizePercent=30");
        args.Add("-XX:G1MaxNewSizePercent=40");
        args.Add("-XX:G1HeapRegionSize=8M");
        args.Add("-XX:G1ReservePercent=20");
        args.Add("-XX:G1HeapWastePercent=5");
        args.Add("-XX:G1MixedGCCountTarget=4");
        args.Add("-XX:InitiatingHeapOccupancyPercent=15");
        args.Add("-XX:G1MixedGCLiveThresholdPercent=90");
        args.Add("-XX:G1RSetUpdatingPauseTimePercent=5");
        args.Add("-XX:SurvivorRatio=32");
        args.Add("-XX:+PerfDisableSharedMem");
        args.Add("-XX:MaxTenuringThreshold=1");
        
        args.Add("-Dusing.aikars.flags=https://mcflags.emc.gs");
        args.Add("-Daikars.new.flags=true");
        
        if (PlatformManager.IsWindows())
        {
            args.Add("-XX:HeapDumpPath=MojangTricksIntelDriversForPerformance_javaw.exe_minecraft.exe.heapdump");
        }
        
        string nativesDir = Path.Combine(mcDir, "versions", installation.Version, "natives");
        args.Add("-Djava.library.path=" + EscapePath(nativesDir));
        
        args.Add("-Dminecraft.launcher.brand=JustLauncher");
        args.Add("-Dminecraft.launcher.version=1.0.0");
        
        if (account.AccountType == "ElyBy")
        {
            string toolsDir = Path.Combine(mcDir, "tools");
            string injectorPath = Path.Combine(toolsDir, "authlib-injector.jar");
            if (File.Exists(injectorPath))
            {
                string agentArg = $"-javaagent:{EscapePath(injectorPath)}=https://authserver.ely.by";
                args.Add(agentArg);
            }
            else
            {
            }
        }

        var classpath = new List<string>();
        string currentOs = PlatformManager.GetCurrentOsName();

        foreach (var lib in versionInfo.Libraries)
        {
            if (!lib.IsAllowed(currentOs)) continue;

            if (lib.Downloads.Artifact != null && !string.IsNullOrEmpty(lib.Downloads.Artifact.Path))
            {
                classpath.Add(Path.Combine(mcDir, "libraries", lib.Downloads.Artifact.Path));
            }

            if (lib.Natives != null && lib.Natives.TryGetValue(currentOs, out string? classifier))
            {
                if (lib.Downloads.Classifiers != null && lib.Downloads.Classifiers.TryGetValue(classifier, out var nativeArtifact))
                {
                    classpath.Add(Path.Combine(mcDir, "libraries", nativeArtifact.Path));
                }
            }

            if (lib.Downloads.Classifiers != null)
            {
                foreach (var entry in lib.Downloads.Classifiers)
                {
                    if (entry.Key.Contains($"natives-{currentOs}") && PlatformManager.IsArchitectureMatch(entry.Key, currentOs))
                    {
                        classpath.Add(Path.Combine(mcDir, "libraries", entry.Value.Path));
                    }
                }
            }
        }
        classpath.Add(Path.Combine(mcDir, "versions", installation.Version, installation.Version + ".jar"));
        
        args.Add("-cp");
        args.Add(string.Join(Path.PathSeparator, classpath));

        args.Add(versionInfo.MainClass);

        if (!string.IsNullOrEmpty(versionInfo.MinecraftArguments))
        {
            string gameArgs = versionInfo.MinecraftArguments;
            gameArgs = gameArgs.Replace("${auth_player_name}", account.Username);
            gameArgs = gameArgs.Replace("${version_name}", installation.Version);
            gameArgs = gameArgs.Replace("${game_directory}", EscapePath(installation.GameDirectory));
            gameArgs = gameArgs.Replace("${assets_root}", EscapePath(Path.Combine(mcDir, "assets")));
            gameArgs = gameArgs.Replace("${assets_index_name}", versionInfo.AssetIndex.Id);
            gameArgs = gameArgs.Replace("${auth_uuid}", account.Id.Replace("-", ""));
            gameArgs = gameArgs.Replace("${auth_access_token}", "0");
            gameArgs = gameArgs.Replace("${user_type}", "legacy");
            gameArgs = gameArgs.Replace("${version_type}", versionInfo.Type);
            gameArgs = gameArgs.Replace("${user_properties}", "{}");

            args.Add(gameArgs);
        }
        else
        {
            args.Add("--username");
            args.Add(account.Username);
            args.Add("--version");
            args.Add(installation.Version);
            args.Add("--gameDir");
            args.Add(EscapePath(installation.GameDirectory));
            args.Add("--assetsDir");
            args.Add(EscapePath(Path.Combine(mcDir, "assets")));
            args.Add("--assetIndex");
            args.Add(versionInfo.AssetIndex.Id);
            args.Add("--uuid");
            args.Add(account.Id.Replace("-", ""));
            args.Add("--accessToken");
            args.Add("0");
            args.Add("--userType");
            args.Add("legacy");
            args.Add("--versionType");
            args.Add(versionInfo.Type);
            
            args.Add("--userProperties");
            args.Add("{}");
        }

        return string.Join(" ", args);
    }

    private static string EscapePath(string path)
    {
        if (path.Contains(" ")) return "\"" + path + "\"";
        return path;
    }
}
