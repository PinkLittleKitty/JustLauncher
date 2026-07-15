using System.Reflection;

namespace JustLauncher;

public static class AppVersion
{
    public static string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    public const string Name = "JustLauncher";

    public static string FullName => $"{Name} {Version}";

    public const string Copyright = "© 2026 JustNeki • Open Source";
}
