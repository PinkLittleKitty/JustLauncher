using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace JustLauncher
{
    public class AccountTypeToIconConverter : IValueConverter
    {
        public static AccountTypeToIconConverter Instance { get; } = new AccountTypeToIconConverter();

        public AccountTypeToIconConverter() { }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value?.ToString() switch
            {
                "Microsoft" => "🔷",
                "Mojang" => "🟫",
                "Offline" => "👤",
                _ => "👤"
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class AccountActiveStatusConverter : IValueConverter
    {
        public static AccountActiveStatusConverter Instance { get; } = new AccountActiveStatusConverter();

        public AccountActiveStatusConverter() { }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isActive)
            {
                return isActive ? "ACTIVE" : "INACTIVE";
            }
            return "INACTIVE";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class AccountBoolToColorConverter : IValueConverter
    {
        public static AccountBoolToColorConverter Instance { get; } = new AccountBoolToColorConverter();

        public AccountBoolToColorConverter() { }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isActive)
            {
                return new SolidColorBrush(isActive ? Color.FromRgb(0, 200, 81) : Color.FromRgb(158, 158, 158));
            }
            return new SolidColorBrush(Color.FromRgb(158, 158, 158));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}