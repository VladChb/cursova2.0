using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using MediaVault.Models;
using MediaVault.Services;

namespace MediaVault.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly string _dataFolder;

        public SettingsWindow()
        {
            InitializeComponent();

            _dataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MediaVault");

            DataPathText.Text = _dataFolder;

            TmdbKeyTextBox.Text = EnvService.Get("TMDB_API_KEY", "");
            LastFmKeyTextBox.Text = EnvService.Get("LASTFM_API_KEY", "");

            // Завантажуємо збережені налаштування
            var settings = LoadSettings();
            AutoSaveCheckBox.IsChecked = settings.AutoSave;
            AutoScanCheckBox.IsChecked = settings.AutoScan;
            ConfirmDeleteCheckBox.IsChecked = settings.ConfirmDelete;

            // Тема — з ThemeService (він уже знає поточну)
            LoadCurrentTheme();

            // Підписка ПІСЛЯ LoadCurrentTheme — щоб не викликати ApplyTheme двічі
            LightThemeRadio.Checked += (s, e) => ThemeService.ApplyTheme("Light");
            DarkThemeRadio.Checked += (s, e) => ThemeService.ApplyTheme("Dark");
        }

        private void LoadCurrentTheme()
        {
            if (ThemeService.CurrentTheme == "Dark")
                DarkThemeRadio.IsChecked = true;
            else
                LightThemeRadio.IsChecked = true;
        }

        private AppSettings LoadSettings()
        {
            try
            {
                string settingsPath = Path.Combine(_dataFolder, "settings.json");
                if (File.Exists(settingsPath))
                {
                    string json = File.ReadAllText(settingsPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Помилка читання налаштувань: {ex.Message}");
            }
            return new AppSettings();
        }

        private static string LoadSavedTheme()
        {
            try
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MediaVault", "settings.json");
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    return settings?.Theme ?? "Dark";
                }
            }
            catch { }
            return "Dark";
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            }
            else
            {
                DragMove();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string envPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env");

                string envContent =
                    $"TMDB_API_KEY={TmdbKeyTextBox.Text}\n" +
                    $"LASTFM_API_KEY={LastFmKeyTextBox.Text}\n";

                File.WriteAllText(envPath, envContent);

                // Зберігаємо ВСІ налаштування, включно з темою
                SaveSettings(new AppSettings
                {
                    Theme = ThemeService.CurrentTheme,
                    AutoSave = AutoSaveCheckBox.IsChecked == true,
                    AutoScan = AutoScanCheckBox.IsChecked == true,
                    ConfirmDelete = ConfirmDeleteCheckBox.IsChecked == true
                });

                MessageBox.Show("Налаштування збережено!\n\nЗміни API-ключів набудуть чинності після перезапуску.",
                                "Успіх",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);

                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не вдалося зберегти налаштування:\n{ex.Message}",
                                "Помилка",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        private void SaveSettings(AppSettings settings)
        {
            try
            {
                string settingsPath = Path.Combine(_dataFolder, "settings.json");

                if (!Directory.Exists(_dataFolder))
                    Directory.CreateDirectory(_dataFolder);

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(settingsPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Помилка збереження налаштувань: {ex.Message}");
            }
        }

        private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!Directory.Exists(_dataFolder))
                    Directory.CreateDirectory(_dataFolder);

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _dataFolder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не вдалося відкрити папку:\n{ex.Message}");
            }
        }

        private void ClearCache_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Очистити кеш обкладинок?\n\nЦе не вплине на вашу колекцію.",
                "Підтвердження",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                string coversFolder = Path.Combine(_dataFolder, "Covers");

                if (Directory.Exists(coversFolder))
                {
                    Directory.Delete(coversFolder, true);
                    Directory.CreateDirectory(coversFolder);
                }

                MessageBox.Show("Кеш очищено!", "Готово",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка очищення кешу:\n{ex.Message}");
            }
        }
    }
}