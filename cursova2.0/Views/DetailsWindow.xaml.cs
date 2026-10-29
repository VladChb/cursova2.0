using MediaVault.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace MediaVault.Views
{
    public partial class DetailsWindow : Window
    {
        private readonly MediaItem _item;
        private bool _isPreviewVisible = false;

        public bool IsDeleted { get; private set; }

        public bool IsPreviewable => _item.Type == MediaType.Film
                                   || _item.Type == MediaType.ShortVideo
                                   || _item.Type == MediaType.Music
                                   || _item.Type == MediaType.Image
                                   || _item.Type == MediaType.Book;

        public DetailsWindow(MediaItem item)
        {
            InitializeComponent();
            _item = item;
            DataContext = item;

            this.KeyDown += DetailsWindow_KeyDown;
        }

        private void DetailsWindow_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    Close_Click(sender, e);
                    break;
                case Key.Space:
                    if (MediaPlayer.Visibility == Visibility.Visible)
                    {
                        if (MediaPlayer.CanPause)
                            MediaPlayer.Pause();
                        else
                            MediaPlayer.Play();
                    }
                    break;
            }
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

        // ============================================================
        // КНОПКИ ВІКНА
        // ============================================================

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _item.FilePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не вдалося відкрити файл:\n{ex.Message}",
                                "Помилка",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        private void OpenTmdb_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_item.TmdbUrl))
                return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _item.TmdbUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не вдалося відкрити посилання:\n{ex.Message}");
            }
        }

        private void OpenTrackUrl_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_item.TrackUrl))
                return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _item.TrackUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не вдалося відкрити посилання:\n{ex.Message}");
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                $"Видалити \"{_item.Title}\" з колекції?\n(Файл на диску залишиться)",
                "Підтвердження",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                IsDeleted = true;
                DialogResult = true;
                Close();
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        // ============================================================
        // ВИБІР ОБКЛАДИНКИ
        // ============================================================

        private void ChooseCover_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Виберіть обкладинку",
                Filter = "Зображення|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp|Всі файли|*.*"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                string appDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MediaVault",
                    "Covers");

                if (!Directory.Exists(appDataFolder))
                    Directory.CreateDirectory(appDataFolder);

                string extension = Path.GetExtension(dialog.FileName);
                string newFileName = $"{_item.Id}{extension}";
                string newFilePath = Path.Combine(appDataFolder, newFileName);

                File.Copy(dialog.FileName, newFilePath, overwrite: true);

                _item.PosterUrl = newFilePath;

                DataContext = null;
                DataContext = _item;

                MessageBox.Show("Обкладинку встановлено!", "Успіх",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не вдалося встановити обкладинку:\n{ex.Message}",
                                "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DownloadCover_Click(object sender, RoutedEventArgs e)
        {
            string url = CoverUrlTextBox.Text?.Trim();

            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show("Введіть URL обкладинки", "Помилка",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var httpClient = new System.Net.Http.HttpClient();
                byte[] imageBytes = await httpClient.GetByteArrayAsync(url);

                string appDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MediaVault",
                    "Covers");

                if (!Directory.Exists(appDataFolder))
                    Directory.CreateDirectory(appDataFolder);

                string extension = Path.GetExtension(new Uri(url).AbsolutePath);
                if (string.IsNullOrEmpty(extension))
                    extension = ".jpg";

                string newFileName = $"{_item.Id}{extension}";
                string newFilePath = Path.Combine(appDataFolder, newFileName);

                await File.WriteAllBytesAsync(newFilePath, imageBytes);

                _item.PosterUrl = newFilePath;

                DataContext = null;
                DataContext = _item;

                MessageBox.Show("Обкладинку завантажено!", "Успіх",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не вдалося завантажити обкладинку:\n{ex.Message}",
                                "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ============================================================
        // ПЕРЕМИКАННЯ МІЖ ІНФОРМАЦІЄЮ ТА ПЕРЕГЛЯДОМ
        // ============================================================

        private void ShowInfo_Click(object sender, RoutedEventArgs e)
        {
            _isPreviewVisible = false;
            InfoPanel.Visibility = Visibility.Visible;
            PreviewPanel.Visibility = Visibility.Collapsed;

            InfoTabButton.Background = (System.Windows.Media.Brush)FindResource("AccentColor");
            InfoTabButton.Foreground = System.Windows.Media.Brushes.White;

            PreviewTabButton.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F0F0F5"));
            PreviewTabButton.Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryLight");

            StopAllMedia();
        }

        private void ShowPreview_Click(object sender, RoutedEventArgs e)
        {
            _isPreviewVisible = true;
            InfoPanel.Visibility = Visibility.Collapsed;
            PreviewPanel.Visibility = Visibility.Visible;

            PreviewTabButton.Background = (System.Windows.Media.Brush)FindResource("AccentColor");
            PreviewTabButton.Foreground = System.Windows.Media.Brushes.White;

            InfoTabButton.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F0F0F5"));
            InfoTabButton.Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryLight");

            LoadPreview();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            StopAllMedia();
        }

        private async void LoadPreview()
        {


            StopAllMedia();

            try
            {
                switch (_item.Type)
                {
                    case MediaType.Film:
                    case MediaType.ShortVideo:
                    case MediaType.Music:
                        MediaPlayer.Source = new Uri(_item.FilePath, UriKind.Absolute);
                        MediaPlayer.Visibility = Visibility.Visible;
                        MediaPlayer.Play();
                        break;

                    case MediaType.Image:
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(_item.FilePath, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();

                        ImageViewer.Source = bitmap;
                        ImageViewer.Visibility = Visibility.Visible;
                        break;

                    case MediaType.Book:
                        if (_item.FileExtension.ToLower() == ".pdf")
                        {
                            await PdfViewer.EnsureCoreWebView2Async();
                            PdfViewer.CoreWebView2.Navigate(new Uri(_item.FilePath, UriKind.Absolute).ToString());
                            PdfViewer.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            MessageBox.Show("Перегляд цього формату книги не підтримується.\nПідтримується лише PDF.",
                                            "Інформація",
                                            MessageBoxButton.OK,
                                            MessageBoxImage.Information);
                        }
                        break;

                    default:
                        MessageBox.Show("Перегляд цього типу файлів не підтримується.",
                                        "Інформація",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Information);
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не вдалося завантажити перегляд:\n{ex.Message}",
                                "Помилка",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        // ============================================================
        // КЕРУВАННЯ МЕДІА
        // ============================================================

        private void PlayMedia_Click(object sender, RoutedEventArgs e)
        {
            MediaPlayer.Play();
        }

        private void PauseMedia_Click(object sender, RoutedEventArgs e)
        {
            MediaPlayer.Pause();
        }

        private void StopMedia_Click(object sender, RoutedEventArgs e)
        {
            MediaPlayer.Stop();
        }

        private void StopAllMedia()
        {
            try
            {
                MediaPlayer.Stop();
                MediaPlayer.Source = null;
                MediaPlayer.Visibility = Visibility.Collapsed;

                ImageViewer.Source = null;
                ImageViewer.Visibility = Visibility.Collapsed;

                PdfViewer.Visibility = Visibility.Collapsed;
            }
            catch { }
        }

        // ============================================================
        // ПОВНОЕКРАННИЙ РЕЖИМ
        // ============================================================

        private void Fullscreen_Click(object sender, RoutedEventArgs e)
        {
            var fullscreenWindow = new Window
            {
                WindowStyle = WindowStyle.None,
                WindowState = WindowState.Maximized,
                Background = System.Windows.Media.Brushes.Black,
                Title = _item.Title,
                Owner = this
            };

            if (MediaPlayer.Visibility == Visibility.Visible && MediaPlayer.Source != null)
            {
                var media = new MediaElement
                {
                    Source = MediaPlayer.Source,
                    LoadedBehavior = MediaState.Manual,
                    Stretch = System.Windows.Media.Stretch.Uniform
                };
                fullscreenWindow.Content = media;
                media.Play();
            }
            else if (ImageViewer.Visibility == Visibility.Visible && ImageViewer.Source != null)
            {
                var image = new System.Windows.Controls.Image
                {
                    Source = ImageViewer.Source,
                    Stretch = System.Windows.Media.Stretch.Uniform
                };
                fullscreenWindow.Content = image;
            }
            else
            {
                MessageBox.Show("Немає що показувати на весь екран.", "Інформація",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            fullscreenWindow.KeyDown += (s, args) =>
            {
                if (args.Key == Key.Escape)
                    fullscreenWindow.Close();
            };

            fullscreenWindow.ShowDialog();
        }

        private void Window_Closed(object sender, EventArgs e)
        {

        }
    }
}