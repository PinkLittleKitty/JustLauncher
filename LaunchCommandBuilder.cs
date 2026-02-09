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

        // JVM Arguments - Optimized for Minecraft Performance
        // Based on Aikar's flags: https://mcflags.emc.gs
        
        // Memory allocation
        args.Add($"-Xmx{(int)settings.MemoryAllocationGb}G");
        args.Add($"-Xms{(int)settings.MemoryAllocationGb}G");
        
        // G1GC Garbage Collector - Optimized for low pause times
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
        
        // Aikar's flags metadata
        args.Add("-Dusing.aikars.flags=https://mcflags.emc.gs");
        args.Add("-Daikars.new.flags=true");
        
        // Platform-specific optimizations
        if (PlatformManager.IsWindows())
        {
            // Intel GPU driver optimization trick for Windows
            args.Add("-XX:HeapDumpPath=MojangTricksIntelDriversForPerformance_javaw.exe_minecraft.exe.heapdump");
        }
        
        string nativesDir = Path.Combine(mcDir, "versions", installation.Version, "natives");
        args.Add("-Djava.library.path=" + EscapePath(nativesDir));
        
        args.Add("-Dminecraft.launcher.brand=JustLauncher");
        args.Add("-Dminecraft.launcher.version=0.0.2");

        // Classpath
        var classpath = new List<string>();
        string currentOs = PlatformManager.GetCurrentOsName();

        foreach (var lib in versionInfo.Libraries)
        {
            if (!lib.IsAllowed(currentOs)) continue;

            // Main artifact
            if (lib.Downloads.Artifact != null && !string.IsNullOrEmpty(lib.Downloads.Artifact.Path))
            {
                classpath.Add(Path.Combine(mcDir, "libraries", lib.Downloads.Artifact.Path));
            }

            // Legacy Native artifact
            if (lib.Natives != null && lib.Natives.TryGetValue(currentOs, out string? classifier))
            {
                if (lib.Downloads.Classifiers != null && lib.Downloads.Classifiers.TryGetValue(classifier, out var nativeArtifact))
                {
                    classpath.Add(Path.Combine(mcDir, "libraries", nativeArtifact.Path));
                }
            }

            // Modern Native classifiers
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

        // Main Class
        args.Add(versionInfo.MainClass);

        // Game Arguments
        if (!string.IsNullOrEmpty(versionInfo.MinecraftArguments))
        {
            // Legacy argument string template
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
            // Modern hardcoded fallback (for versions without arguments list or template)
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
            
            // Helpful for some older versions that might not have the template but still need this
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
