using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace JustLauncher.Converters
{
    public class AccountToSourceRectConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Account account && account.AccountType == "ElyBy")
            {
                return new Rect(8, 8, 8, 8);
            }
            return default(Rect);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
