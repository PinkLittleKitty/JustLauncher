using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace JustLauncher.Services;

/// <summary>
/// Service for managing application localization and language switching.
/// </summary>
public class LocalizationService : INotifyPropertyChanged
{
    private static LocalizationService? _instance;
    public static LocalizationService Instance => _instance ??= new LocalizationService();

    public event PropertyChangedEventHandler? PropertyChanged;
    
    /// <summary>
    /// Event raised when the language changes.
    /// </summary>
    public event EventHandler? LanguageChanged;

    /// <summary>
    /// Available languages in the application.
    /// </summary>
    public List<LanguageInfo> AvailableLanguages { get; } = new()
    {
        new LanguageInfo("en", "English"),
        new LanguageInfo("es", "Español")
    };

    /// <summary>
    /// Gets the current culture code (e.g., "en", "es").
    /// </summary>
    public string CurrentLanguage => Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;

    /// <summary>
    /// Indexer for accessing localized strings dynamically.
    /// This enables binding like {Binding [Nav_Play]}
    /// </summary>
    public string this[string key]
    {
        get
        {
            try
            {
                var value = Resources.Strings.ResourceManager.GetString(key, Thread.CurrentThread.CurrentUICulture);
                return value ?? key;
            }
            catch
            {
                return key;
            }
        }
    }

    /// <summary>
    /// Changes the application language.
    /// </summary>
    /// <param name="cultureName">Culture code (e.g., "en", "es")</param>
    public void ChangeLanguage(string cultureName)
    {
        try
        {
            var culture = new CultureInfo(cultureName);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
            
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (CultureNotFoundException)
        {
            var fallback = new CultureInfo("en");
            Thread.CurrentThread.CurrentCulture = fallback;
            Thread.CurrentThread.CurrentUICulture = fallback;
            CultureInfo.CurrentCulture = fallback;
            CultureInfo.CurrentUICulture = fallback;
        }
    }

    /// <summary>
    /// Gets the display name for a language code.
    /// </summary>
    public string GetLanguageDisplayName(string cultureName)
    {
        return AvailableLanguages.FirstOrDefault(l => l.Code == cultureName)?.DisplayName ?? cultureName;
    }

    /// <summary>
    /// Gets the best matching supported language based on the system's current culture.
    /// </summary>
    /// <returns>ISO language code (e.g., "en", "es")</returns>
    public string GetBestMatchLanguage()
    {
        var systemLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        
        // Check if we support the system language
        if (AvailableLanguages.Any(l => l.Code == systemLanguage))
        {
            return systemLanguage;
        }

        // Fallback to English
        return "en";
    }
}

/// <summary>
/// Information about an available language.
/// </summary>
public class LanguageInfo
{
    public string Code { get; set; }
    public string DisplayName { get; set; }

    public LanguageInfo(string code, string displayName)
    {
        Code = code;
        DisplayName = displayName;
    }
}
