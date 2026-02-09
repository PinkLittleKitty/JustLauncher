using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace JustLauncher.Converters
{
    public class AccountToSkinUrlConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is Account account)
            {
                int size = 64;
                if (parameter is string s && int.TryParse(s, out int pSize)) size = pSize;
                return account.GetAvatarUrl(size);
            }
            
            if (value is string username)
            {
                return $"https://minotar.net/avatar/{username}/64";
            }

            return "https://minotar.net/avatar/Steve/64";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
