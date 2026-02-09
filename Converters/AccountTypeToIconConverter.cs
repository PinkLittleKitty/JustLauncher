using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace JustLauncher.Converters
{
    public class AccountTypeToIconConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string type)
            {
                return type switch
                {
                    "Microsoft" => "fa-brands fa-microsoft",
                    "Mojang" => "fa-solid fa-cube",
                    "Cracked" => "fa-solid fa-user-secret",
                    _ => "fa-solid fa-user"
                };
            }
            return "fa-solid fa-user";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
