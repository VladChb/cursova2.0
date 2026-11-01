using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using MediaVault.Models;

namespace MediaVault.Services
{
    public static class ThemeService
    {
        private static readonly string DataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MediaVault");

        private static readonly string SettingsPath = Path.Combine(DataFolder, "settings.json");

        public static string CurrentTheme { get; private set; } = "Dark";

        /// <summary>
        /// Застосовує тему до всього застосунку.
        /// </summary>
        public static void ApplyTheme(string themeName)
        {
            if (string.IsNullOrWhiteSpace(themeName))
                themeName = "Dark";

            var app = Application.Current;
            if (app == null) return;

            var dicts = app.Resources.MergedDictionaries;

            // Знаходимо старий словник теми
            var old = dicts.FirstOrDefault(d =>
                d.Source != null &&
                (d.Source.OriginalString.Contains("DarkTheme") ||
                 d.Source.OriginalString.Contains("LightTheme")));

            // Створюємо новий
            var newDict = new ResourceDictionary
            {
                Source = new Uri($"Themes/{themeName}Theme.xaml", UriKind.Relative)
            };

            // Замінюємо
            if (old != null)
                dicts.Remove(old);

            dicts.Insert(0, newDict); // на початок, щоб інші ресурси могли перекривати

            CurrentTheme = themeName;
        }

        /// <summary>
        /// Перемикає тему між Dark та Light.
        /// </summary>
        public static void Toggle()
        {
            ApplyTheme(CurrentTheme == "Dark" ? "Light" : "Dark");
        }

        /// <summary>
        /// Читає збережену тему з settings.json.
        /// </summary>
        public static string LoadSavedTheme()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    return settings?.Theme ?? "Dark";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadSavedTheme: {ex.Message}");
            }
            return "Dark";
        }

        /// <summary>
        /// Ініціалізація при старті: читає збережену тему і застосовує.
        /// </summary>
        public static void Initialize()
        {
            var theme = LoadSavedTheme();
            ApplyTheme(theme);
        }
    }
}