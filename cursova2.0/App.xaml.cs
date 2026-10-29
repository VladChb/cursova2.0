using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using MediaVault.Models;
using MediaVault.Services;
using SkiaSharp;
using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace MediaVault
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            EnvService.Load();

            var settings = LoadSettings();
            ThemeService.ApplyTheme(settings?.Theme ?? "Light");

            LiveCharts.Configure(config =>
                config.HasTextSettings(new TextSettings
                {
                    DefaultTypeface = SKFontManager.Default.MatchCharacter('Я'),
                    FontBuilder = (paint, typeface, size) =>
                    {
                        return new SKFont(typeface, size)
                        {
                            Edging = SKFontEdging.SubpixelAntialias,
                            Hinting = SKFontHinting.Normal
                        };
                    }
                }));
        }

        private AppSettings LoadSettings()
        {
            try
            {
                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MediaVault",
                    "settings.json");

                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    return JsonSerializer.Deserialize<AppSettings>(json);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Помилка завантаження налаштувань: {ex.Message}");
            }

            return new AppSettings();
        }
    }
}