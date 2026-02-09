using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace JustLauncher;

public static class PlatformManager
{
    public static string GetCurrentOsName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "osx";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "linux";
        return "windows";
    }

    public static bool IsArchitectureMatch(string classifier, string currentOs)
    {
        var arch = RuntimeInformation.OSArchitecture;
        bool is64 = arch == Architecture.X64;
        bool isX86 = arch == Architecture.X86;
        bool isArm64 = arch == Architecture.Arm64;

        // If classifier doesn't contain a specific arch suffix, assume x64 for Windows/Linux
        // Modern MC uses natives-windows (x64), natives-windows-x86, natives-windows-arm64
        
        string baseNatives = $"natives-{currentOs}";
        if (classifier == baseNatives) 
        {
            // Special case: for macOS, the generic one is often universal
            if (currentOs == "osx") return true;
            return is64;
        }

        // Exact matches/suffixes for various architectures
        if (classifier.EndsWith("-x86") || classifier.EndsWith("-i386")) return isX86;
        if (classifier.EndsWith("-x64") || classifier.EndsWith("-x86_64") || classifier.EndsWith("-amd64")) return is64;
        if (classifier.EndsWith("-arm64") || classifier.EndsWith("-aarch64")) return isArm64;

        // Contains check for some edge cases
        if (currentOs == "osx" && (classifier.Contains("arm64") || classifier.Contains("aarch64"))) return isArm64;

        return true; // Fallback for unknown patterns (e.g. jtracy-natives-linux)
    }

    public static string GetMinecraftDirectory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".minecraft");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "minecraft");
        }
        
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".minecraft");
    }

    public static string GetJavaExecutableName()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "java.exe" : "java";
    }

    public static string[] GetCommonJavaSearchPaths()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Java"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Java"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Eclipse Adoptium")
            };
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new[]
            {
                "/usr/lib/jvm",
                "/usr/java",
                "/usr/lib/jvm/default",
                "/usr/bin"
            };
        }
        
        return Array.Empty<string>();
    }

    public static void OpenBrowser(string url)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
        }
        catch
        {
            // Ignore browser open failures
        }
    }

    public static string GetJavaExecutable()
    {
        // For now, return what we find in PATH
        return GetJavaExecutableName();
    }

    public static async System.Threading.Tasks.Task<(string? version, string? path)> FindJavaInstallationAsync(int requiredMajorVersion)
    {
        // First, try the PATH
        var pathVersion = await GetJavaVersionFromPathAsync(GetJavaExecutableName());
        if (pathVersion != null)
        {
            int major = ExtractMajorVersion(pathVersion);
            if (major >= requiredMajorVersion)
            {
                return (pathVersion, GetJavaExecutableName());
            }
        }

        // Search common installation directories
        var searchPaths = GetCommonJavaSearchPaths();
        foreach (var basePath in searchPaths)
        {
            if (!Directory.Exists(basePath)) continue;

            try
            {
                // Look for Java installations in subdirectories
                var dirs = Directory.GetDirectories(basePath);
                foreach (var dir in dirs)
                {
                    var javaExe = Path.Combine(dir, "bin", GetJavaExecutableName());
                    if (File.Exists(javaExe))
                    {
                        var version = await GetJavaVersionFromPathAsync(javaExe);
                        if (version != null)
                        {
                            int major = ExtractMajorVersion(version);
                            if (major >= requiredMajorVersion)
                            {
                                return (version, javaExe);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors when searching directories
            }
        }

        // Return the PATH version even if it doesn't meet requirements
        return (pathVersion, pathVersion != null ? GetJavaExecutableName() : null);
    }

    public static int ExtractMajorVersion(string versionString)
    {
        var parts = versionString.Split('.');
        if (parts.Length > 0 && int.TryParse(parts[0], out int major))
        {
            // Handle old version formats like 1.8.x
            if (major == 1 && parts.Length > 1 && int.TryParse(parts[1], out int second))
            {
                return second;
            }
            return major;
        }
        return 0;
    }

    private static async System.Threading.Tasks.Task<string?> GetJavaVersionFromPathAsync(string javaPath)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = javaPath,
                Arguments = "-version",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return null;

            string output = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            // Try different patterns for java version output
            var match = System.Text.RegularExpressions.Regex.Match(output, @"version ""([^""]+)""");
            if (match.Success) return match.Groups[1].Value;

            // Fallback for some OpenJDK outputs that don't say "version"
            match = System.Text.RegularExpressions.Regex.Match(output, @"openjdk (\d+\.\d+\.\d+)");
            if (match.Success) return match.Groups[1].Value;

            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Java check error: {ex.Message}");
            return null;
        }
    }

    public static async System.Threading.Tasks.Task<string?> GetJavaVersionAsync()
    {
        var result = await GetJavaVersionFromPathAsync(GetJavaExecutableName());
        return result;
    }
}
