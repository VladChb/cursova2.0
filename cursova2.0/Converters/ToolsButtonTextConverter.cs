using System;
using System.Globalization;
using System.Windows.Data;

namespace MediaVault.Converters
{
    public class ToolsButtonTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isToolsMode)
                return isToolsMode ? "📂 Категорії" : "🧰 Інструменти";
            return "🧰 Інструменти";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}