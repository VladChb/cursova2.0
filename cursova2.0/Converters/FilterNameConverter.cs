using System;
using System.Globalization;
using System.Windows.Data;
using MediaVault.Models;

namespace MediaVault
{
    public class FilterNameConverter : IValueConverter
    {
        public static FilterNameConverter Instance { get; } = new();
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
        {
            MediaType.Unknown => "Усі файли",
            MediaType.Film => "Фільми",
            MediaType.ShortVideo => "Короткі відео",
            MediaType.Book => "Книги",
            MediaType.Music => "Музика",
            MediaType.Image => "Зображення",
            MediaType.Document => "Документи",
            MediaType.Program => "Програми",
            _ => "Усі файли"
        };
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}