using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JustLauncher.Services;

public class JavaInfo
{
    public string Path { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public int MajorVersion { get; set; }
    public string Architecture { get; set; } = string.Empty;
    public bool IsJre { get; set; }
    
    private string? _displayName;
    public string DisplayName 
    { 
        get => _displayName ?? $"Java {MajorVersion} ({Version})";
        set => _displayName = value;
    }
    
    public override string ToString() => DisplayName;
    
    public override bool Equals(object? obj)
    {
        if (obj is JavaInfo other) return Path == other.Path;
        return false;
    }
    
    public override int GetHashCode() => Path.GetHashCode();
}

public class JavaManager
{
    private static readonly string JavaInstallDir = Path.Combine(PlatformManager.GetMinecraftDirectory(), "runtime");
    private readonly HttpClient _httpClient = new();

    public JavaManager()
    {
        if (!Directory.Exists(JavaInstallDir)) Directory.CreateDirectory(JavaInstallDir);
    }

    public async Task<List<JavaInfo>> GetInstalledJavaVersionsAsync()
    {
        var runtimes = new HashSet<JavaInfo>();

        if (Directory.Exists(JavaInstallDir))
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(JavaInstallDir))
                {
                    var javaPath = FindJavaExecutable(dir);
                    if (!string.IsNullOrEmpty(javaPath) && File.Exists(javaPath))
                    {
                        var info = await GetJavaVersionInfoAsync(javaPath);
                        if (info != null) runtimes.Add(info);
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine($"Error scanning Java install dir: {ex}"); }
        }

        var systemPaths = GetSystemJavaPaths();
        foreach (var path in systemPaths)
        {
            try
            {
                if (File.Exists(path))
                {
                    var info = await GetJavaVersionInfoAsync(path);
                    if (info != null) runtimes.Add(info);
                }
                else if (Directory.Exists(path))
                {
                     var javaPath = FindJavaExecutable(path);
                     if (!string.IsNullOrEmpty(javaPath))
                     {
                         var info = await GetJavaVersionInfoAsync(javaPath);
                         if (info != null) runtimes.Add(info);
                     }
                }
            }
            catch (Exception ex) { Console.WriteLine($"Error checking system path {path}: {ex}"); }
        }
        
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(javaHome))
        {
            var path = FindJavaExecutable(javaHome);
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                var info = await GetJavaVersionInfoAsync(path);
                if (info != null) runtimes.Add(info);
            }
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            var paths = pathEnv.Split(Path.PathSeparator);
            foreach (var p in paths)
            {
                var javaExe = Path.Combine(p, PlatformManager.GetJavaExecutableName());
                if (File.Exists(javaExe))
                {
                    var info = await GetJavaVersionInfoAsync(javaExe);
                    if (info != null) runtimes.Add(info);
                }
            }
        }

        return runtimes.OrderByDescending(r => r.MajorVersion).ThenByDescending(r => r.Version).ToList();
    }

    public async Task<string?> DownloadJavaAsync(int majorVersion, IProgress<double>? progress = null)
    {
        string os = PlatformManager.IsWindows() ? "windows" : PlatformManager.IsLinux() ? "linux" : "mac";
        string arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "aarch64" : "x64";
        string imageType = "jre"; 
        
        string apiUrl = $"https://api.adoptium.net/v3/assets/latest/{majorVersion}/hotspot?os={os}&architecture={arch}&image_type={imageType}";
        
        try
        {
            var response = await _httpClient.GetStringAsync(apiUrl);
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;
            
            if (root.GetArrayLength() == 0) return null;
            
            var binary = root[0].GetProperty("binary");
            var package = binary.GetProperty("package");
            var downloadUrl = package.GetProperty("link").GetString();
            var name = package.GetProperty("name").GetString();
            
            if (string.IsNullOrEmpty(downloadUrl)) return null;

            string componentName = $"java-runtime-{majorVersion}";
            string targetDir = Path.Combine(JavaInstallDir, componentName);
            string tempFile = Path.Combine(Path.GetTempPath(), name ?? "java_download.tmp");

            if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true);
            if (File.Exists(tempFile)) File.Delete(tempFile);

            await DownloadFileAsync(downloadUrl, tempFile, progress);

            bool success = await ExtractArchiveAsync(tempFile, JavaInstallDir);
            File.Delete(tempFile);

            if (!success) return null;

            return await DetectAndMoveExtractedJava(JavaInstallDir, targetDir);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Java download failed: {ex.Message}");
            return null;
        }
    }

    private async Task<string?> DetectAndMoveExtractedJava(string extractionRoot, string finalTargetDir)
    {
        return null;
    }

    private async Task<JavaInfo?> GetJavaVersionInfoAsync(string path)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = path,
                Arguments = "-version",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return null;

            var output = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            string versionStr = "";
            int majorVer = 0;

            var match = Regex.Match(output, @"version ""([^""]+)""");
            if (!match.Success) match = Regex.Match(output, @"openjdk version ""([^""]+)""");
            
            if (match.Success)
            {
                versionStr = match.Groups[1].Value;
                majorVer = PlatformManager.ExtractMajorVersion(versionStr);
            }
            else
            {
                 match = Regex.Match(output, @"(\d+\.\d+\.\d+)");
                 if (match.Success)
                 {
                     versionStr = match.Groups[1].Value;
                     majorVer = PlatformManager.ExtractMajorVersion(versionStr);
                 }
            }

            if (majorVer == 0) return null;

            return new JavaInfo
            {
                Path = path,
                Version = versionStr,
                MajorVersion = majorVer,
                IsJre = !path.Contains("javac"),
                Architecture = output.Contains("64-Bit") ? "x64" : "x86"
            };
        }
        catch
        {
            return null;
        }
    }

    private string FindJavaExecutable(string dir)
    {
        if (!Directory.Exists(dir)) return string.Empty;
        
        var exeName = PlatformManager.GetJavaExecutableName();
        var binJava = Path.Combine(dir, "bin", exeName);
        if (File.Exists(binJava)) return binJava;
        
        var macJava = Path.Combine(dir, "Contents", "Home", "bin", exeName);
        if (File.Exists(macJava)) return macJava;
        
        if (File.Exists(Path.Combine(dir, exeName))) return Path.Combine(dir, exeName);

        try
        {
            foreach (var subDir in Directory.GetDirectories(dir))
            {
                binJava = Path.Combine(subDir, "bin", exeName);
                if (File.Exists(binJava)) return binJava;
            }
        }
        catch {}

        return string.Empty;
    }

    private List<string> GetSystemJavaPaths()
    {
        var paths = new List<string>(PlatformManager.GetCommonJavaSearchPaths());
        
        if (PlatformManager.IsWindows())
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            paths.Add(Path.Combine(programFiles, "Eclipse Adoptium"));
            paths.Add(Path.Combine(programFiles, "Microsoft"));
            paths.Add(Path.Combine(programFiles, "BellSoft"));
        }
        else if (PlatformManager.IsLinux())
        {
             if (Directory.Exists("/usr/lib/jvm"))
             {
                 try
                 {
                     paths.AddRange(Directory.GetDirectories("/usr/lib/jvm"));
                 }
                 catch (Exception ex) { Console.WriteLine($"Error listing /usr/lib/jvm: {ex}"); }
             }
        }
        
        return paths;
    }
    
    private async Task DownloadFileAsync(string url, string path, IProgress<double>? progress)
    {
         using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
         response.EnsureSuccessStatusCode();
         var totalBytes = response.Content.Headers.ContentLength ?? -1L;
         using var stream = await response.Content.ReadAsStreamAsync();
         using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
         var buffer = new byte[8192];
         var totalRead = 0L;
         int bytesRead;
         while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
         {
             await fileStream.WriteAsync(buffer, 0, bytesRead);
             totalRead += bytesRead;
             if (totalBytes != -1) progress?.Report((double)totalRead / totalBytes * 100);
         }
    }

    private async Task<bool> ExtractArchiveAsync(string archivePath, string destination)
    {
        try
        {
            if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ZipFile.ExtractToDirectory(archivePath, destination);
                return true;
            }
            else if (archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) || archivePath.EndsWith(".tgz"))
            {
                if (PlatformManager.IsWindows()) return false;
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = "tar",
                    Arguments = $"-xzf \"{archivePath}\" -C \"{destination}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                if (PlatformManager.IsWindows())
                {}

                using var process = Process.Start(startInfo);
                if (process == null) return false;
                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Extraction failed: {ex.Message}");
            return false;
        }
        return false;
    }
}
