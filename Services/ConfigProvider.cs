using System;
using System.Collections.Generic;
using System.IO;

namespace JustLauncher.Services;

public static class ConfigProvider
{
    private static readonly Dictionary<string, string> _envVars = new();
    private static bool _initialized = false;

    private static void Initialize()
    {
        if (_initialized) return;

        string envPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env");
        
        if (!File.Exists(envPath))
        {
            envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
        }

        if (File.Exists(envPath))
        {
            foreach (var line in File.ReadAllLines(envPath))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                var parts = line.Split('=', 2);
                if (parts.Length == 2)
                {
                    _envVars[parts[0].Trim()] = parts[1].Trim();
                }
            }
        }

        _initialized = true;
    }

    public static string? Get(string key)
    {
        Initialize();

        if (_envVars.TryGetValue(key, out var value))
        {
            return value;
        }

        return Environment.GetEnvironmentVariable(key);
    }
}
