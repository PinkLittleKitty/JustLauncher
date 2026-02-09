using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace JustLauncher;

public static class PlatformManager
{
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

    public static async System.Threading.Tasks.Task<string?> GetJavaVersionAsync()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = GetJavaExecutableName(),
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
}
