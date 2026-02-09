using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace JustLauncher;

public static class ConfigManager
{
    private static readonly string MinecraftDir = PlatformManager.GetMinecraftDirectory();
    private static readonly string AccountsPath = Path.Combine(MinecraftDir, "launcher_accounts.json");
    private static readonly string InstallationsPath = Path.Combine(MinecraftDir, "launcher_installations.json");
    private static readonly string SettingsPath = Path.Combine(MinecraftDir, "launcher_settings.json");

    static ConfigManager()
    {
        if (!Directory.Exists(MinecraftDir)) Directory.CreateDirectory(MinecraftDir);
    }

    public static AccountsConfig LoadAccounts()
    {
        if (!File.Exists(AccountsPath)) return new AccountsConfig();
        try
        {
            string json = File.ReadAllText(AccountsPath);
            return JsonSerializer.Deserialize<AccountsConfig>(json) ?? new AccountsConfig();
        }
        catch { return new AccountsConfig(); }
    }

    public static void SaveAccounts(AccountsConfig config)
    {
        string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(AccountsPath, json);
    }

    public static InstallationsConfig LoadInstallations()
    {
        if (!File.Exists(InstallationsPath)) return new InstallationsConfig();
        try
        {
            string json = File.ReadAllText(InstallationsPath);
            return JsonSerializer.Deserialize<InstallationsConfig>(json) ?? new InstallationsConfig();
        }
        catch { return new InstallationsConfig(); }
    }

    public static void SaveInstallations(InstallationsConfig config)
    {
        string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(InstallationsPath, json);
    }

    public static LauncherSettings LoadSettings()
    {
        if (!File.Exists(SettingsPath)) return new LauncherSettings();
        try
        {
            string json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<LauncherSettings>(json) ?? new LauncherSettings();
        }
        catch { return new LauncherSettings(); }
    }

    public static void SaveSettings(LauncherSettings settings)
    {
        string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}

public class LauncherSettings
{
    public string JavaPath { get; set; } = string.Empty;
    public double MemoryAllocationGb { get; set; } = 2.0;
    public bool CloseOnLaunch { get; set; } = false;
    public bool UseSeparateGameDir { get; set; } = false;
    public string Theme { get; set; } = "System";
    public bool DarkMode { get; set; } = true;
}
