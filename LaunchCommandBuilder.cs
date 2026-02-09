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

        // JVM Arguments
        args.Add($"-Xmx{(int)settings.MemoryAllocationGb}G");
        args.Add($"-Xms{(int)settings.MemoryAllocationGb}G");
        
        string nativesDir = Path.Combine(mcDir, "versions", installation.Version, "natives");
        args.Add("-Djava.library.path=" + EscapePath(nativesDir));
        
        args.Add("-Dminecraft.launcher.brand=JustLauncher");
        args.Add("-Dminecraft.launcher.version=1.0");

        // Classpath
        var classpath = new List<string>();
        string currentOs = "linux";
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)) currentOs = "windows";
        else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX)) currentOs = "osx";

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
                    if (entry.Key.Contains($"natives-{currentOs}"))
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
        args.Add("0"); // Offline token
        args.Add("--userType");
        args.Add("legacy");
        args.Add("--versionType");
        args.Add(versionInfo.Type);

        return string.Join(" ", args);
    }

    private static string EscapePath(string path)
    {
        if (path.Contains(" ")) return "\"" + path + "\"";
        return path;
    }
}
