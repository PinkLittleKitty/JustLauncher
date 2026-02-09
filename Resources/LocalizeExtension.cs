using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Metadata;
using System;
using JustLauncher.Services;

namespace JustLauncher.Resources;

/// <summary>
/// Markup extension for dynamic localization that updates when language changes.
/// Usage: Text="{loc:Localize Nav_Play}" or Text="{loc:Localize Key=Nav_Play}"
/// </summary>
public class LocalizeExtension : MarkupExtension
{
    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public LocalizeExtension() { }

    public LocalizeExtension(string key)
    {
        Key = key;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Avalonia.Data.Binding($"[{Key}]")
        {
            Source = LocalizationService.Instance,
            Mode = BindingMode.OneWay
        };

        return binding;
    }
}
