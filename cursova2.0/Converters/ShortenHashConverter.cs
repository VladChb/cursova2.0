using System;
using System.Globalization;
using System.Windows.Data;

namespace MediaVault.Converters
{
    public class ShortenHashConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string hash && hash.Length > 16)
                return hash.Substring(0, 16) + "...";
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
