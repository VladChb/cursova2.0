using System;
using System.Linq;
using System.Windows;

namespace MediaVault.Services
{
    public static class ThemeService
    {
        private static string _currentTheme = "Light";

        public static string CurrentTheme => _currentTheme;

        public static event EventHandler ThemeChanged;

        public static void ApplyTheme(string theme)
        {
            if (string.IsNullOrEmpty(theme) ||
                (theme != "Light" && theme != "Dark"))
                theme = "Light";

            _currentTheme = theme;

            var app = Application.Current;
            if (app == null) return;

            var toRemove = app.Resources.MergedDictionaries
                .Where(d => d.Source != null &&
                    (d.Source.OriginalString.Contains("LightTheme", StringComparison.OrdinalIgnoreCase) ||
                     d.Source.OriginalString.Contains("DarkTheme", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            foreach (var oldDict in toRemove)
                app.Resources.MergedDictionaries.Remove(oldDict);

            var newDict = new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/Themes/{theme}Theme.xaml", UriKind.Absolute)
            };

            app.Resources.MergedDictionaries.Insert(0, newDict);

            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }
    }
}
