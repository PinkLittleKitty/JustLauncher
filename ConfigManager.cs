using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace JustLauncher;

public static class ConfigManager
{
    private static readonly string LauncherDir = PlatformManager.GetLauncherDirectory();
    private static readonly string AccountsPath = Path.Combine(LauncherDir, "launcher_accounts.json");
    private static readonly string InstallationsPath = Path.Combine(LauncherDir, "launcher_installations.json");
    private static readonly string SettingsPath = Path.Combine(LauncherDir, "launcher_settings.json");

    private static readonly System.Threading.SemaphoreSlim _accountsLock = new(1, 1);
    private static readonly System.Threading.SemaphoreSlim _installationsLock = new(1, 1);
    private static readonly System.Threading.SemaphoreSlim _settingsLock = new(1, 1);

    static ConfigManager()
    {
        if (!Directory.Exists(LauncherDir)) Directory.CreateDirectory(LauncherDir);
        
        MigrateOldConfigFiles();
    }

    private static void MigrateOldConfigFiles()
    {
        string oldDir = PlatformManager.GetMinecraftDirectory();
        
        var filesToMigrate = new[]
        {
            "launcher_accounts.json",
            "launcher_installations.json",
            "launcher_settings.json"
        };

        foreach (var fileName in filesToMigrate)
        {
            string oldPath = Path.Combine(oldDir, fileName);
            string newPath = Path.Combine(LauncherDir, fileName);
            
            if (File.Exists(oldPath) && !File.Exists(newPath))
            {
                try
                {
                    File.Move(oldPath, newPath);
                }
                catch
                {
                }
            }
        }
    }

    public static async Task<AccountsConfig> LoadAccountsAsync()
    {
        await _accountsLock.WaitAsync();
        try
        {
            if (!File.Exists(AccountsPath)) return new AccountsConfig();
            string json = await File.ReadAllTextAsync(AccountsPath);
            return JsonSerializer.Deserialize<AccountsConfig>(json) ?? new AccountsConfig();
        }
        catch (Exception ex)
        {
            ConsoleService.Instance.Log($"[Config] Error loading accounts: {ex.Message}");
            return new AccountsConfig();
        }
        finally
        {
            _accountsLock.Release();
        }
    }

    public static async Task SaveAccountsAsync(AccountsConfig config)
    {
        await _accountsLock.WaitAsync();
        try
        {
            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(AccountsPath, json);
        }
        catch (Exception ex)
        {
            ConsoleService.Instance.Log($"[Config] Error saving accounts: {ex.Message}");
            throw;
        }
        finally
        {
            _accountsLock.Release();
        }
    }

    public static async Task<InstallationsConfig> LoadInstallationsAsync()
    {
        await _installationsLock.WaitAsync();
        try
        {
            if (!File.Exists(InstallationsPath)) return new InstallationsConfig();
            string json = await File.ReadAllTextAsync(InstallationsPath);
            return JsonSerializer.Deserialize<InstallationsConfig>(json) ?? new InstallationsConfig();
        }
        catch (Exception ex)
        {
            ConsoleService.Instance.Log($"[Config] Error loading installations: {ex.Message}");
            return new InstallationsConfig();
        }
        finally
        {
            _installationsLock.Release();
        }
    }

    public static async Task SaveInstallationsAsync(InstallationsConfig config)
    {
        await _installationsLock.WaitAsync();
        try
        {
            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(InstallationsPath, json);
        }
        catch (Exception ex)
        {
            ConsoleService.Instance.Log($"[Config] Error saving installations: {ex.Message}");
            throw;
        }
        finally
        {
            _installationsLock.Release();
        }
    }

    public static async Task<LauncherSettings> LoadSettingsAsync()
    {
        await _settingsLock.WaitAsync();
        try
        {
            if (!File.Exists(SettingsPath)) return new LauncherSettings();
            string json = await File.ReadAllTextAsync(SettingsPath);
            return JsonSerializer.Deserialize<LauncherSettings>(json) ?? new LauncherSettings();
        }
        catch (Exception ex)
        {
            ConsoleService.Instance.Log($"[Config] Error loading settings: {ex.Message}");
            return new LauncherSettings();
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public static async Task SaveSettingsAsync(LauncherSettings settings)
    {
        await _settingsLock.WaitAsync();
        try
        {
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(SettingsPath, json);
        }
        catch (Exception ex)
        {
            ConsoleService.Instance.Log($"[Config] Error saving settings: {ex.Message}");
            throw;
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public static LauncherSettings LoadSettings()
    {
        _settingsLock.Wait();
        try
        {
            if (!File.Exists(SettingsPath)) return new LauncherSettings();
            string json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<LauncherSettings>(json) ?? new LauncherSettings();
        }
        catch (Exception ex)
        {
            ConsoleService.Instance.Log($"[Config] Error loading settings: {ex.Message}");
            return new LauncherSettings();
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public static void SaveSettings(LauncherSettings settings)
    {
        _settingsLock.Wait();
        try
        {
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            ConsoleService.Instance.Log($"[Config] Error saving settings: {ex.Message}");
        }
        finally
        {
            _settingsLock.Release();
        }
    }
}

public class LauncherSettings
{
    public string Language { get; set; } = "en";
    public string JavaPath { get; set; } = string.Empty;
    public double MemoryAllocationGb { get; set; } = 2.0;
    public bool CloseOnLaunch { get; set; } = false;
    public bool UseSeparateGameDir { get; set; } = true;
    public string Theme { get; set; } = "System";
    public bool DarkMode { get; set; } = true;
    public bool IsSakiEnabled { get; set; } = false;
    public string SakiSkin { get; set; } = "Steve";
    
    public bool CheckForUpdatesOnStartup { get; set; } = true;
    public DateTime LastUpdateCheck { get; set; } = DateTime.MinValue;
    public string SkippedVersion { get; set; } = string.Empty;
    
    public bool IsFirstRun { get; set; } = true;
}
