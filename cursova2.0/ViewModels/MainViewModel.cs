using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using MediaVault.Helpers;
using MediaVault.Models;
using MediaVault.Services;

namespace MediaVault.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly FileScanner _scanner;
        private readonly DataStorageService _storage;

        // ============================================================
        // КОЛЕКЦІЇ
        // ============================================================

        /// <summary>
        /// Повна колекція всіх медіа-файлів
        /// </summary>
        public ObservableCollection<MediaItem> AllMediaItems { get; set; } = new();

        /// <summary>
        /// Відфільтрована колекція (для відображення в UI)
        /// </summary>
        public ObservableCollection<MediaItem> FilteredMediaItems { get; set; } = new();

        // ============================================================
        // ВЛАСТИВОСТІ UI
        // ============================================================

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    ApplyFilter();
            }
        }

        private MediaType _selectedFilter = MediaType.Unknown;
        public MediaType SelectedFilter
        {
            get => _selectedFilter;
            set
            {
                if (SetProperty(ref _selectedFilter, value))
                    ApplyFilter();
            }
        }

        private string _statusText = "Готово";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private bool _isScanning;
        public bool IsScanning
        {
            get => _isScanning;
            set => SetProperty(ref _isScanning, value);
        }

        private int _scanProgress;
        public int ScanProgress
        {
            get => _scanProgress;
            set => SetProperty(ref _scanProgress, value);
        }

        // ============================================================
        // СТАТИСТИКА (для бокової панелі)
        // ============================================================

        public int TotalCount => AllMediaItems.Count;
        public int FilmCount => AllMediaItems.Count(x => x.Type == MediaType.Film);
        public int BookCount => AllMediaItems.Count(x => x.Type == MediaType.Book);
        public int MusicCount => AllMediaItems.Count(x => x.Type == MediaType.Music);
        public int ImageCount => AllMediaItems.Count(x => x.Type == MediaType.Image);
        public int FavoriteCount => AllMediaItems.Count(x => x.IsFavorite);

        public string TotalSize
        {
            get
            {
                long totalBytes = AllMediaItems.Sum(x => x.FileSize);

                if (totalBytes < 1024) return $"{totalBytes} B";
                if (totalBytes < 1024 * 1024) return $"{totalBytes / 1024.0:F1} KB";
                if (totalBytes < 1024L * 1024 * 1024) return $"{totalBytes / (1024.0 * 1024):F1} MB";
                return $"{totalBytes / (1024.0 * 1024 * 1024):F2} GB";
            }
        }

        // ============================================================
        // КОМАНДИ
        // ============================================================

        public RelayCommand AddFolderCommand { get; }
        public RelayCommand SaveCommand { get; }
        public RelayCommand ClearCommand { get; }
        public RelayCommand FilterCommand { get; }

        // ============================================================
        // КОНСТРУКТОР
        // ============================================================

        public MainViewModel()
        {
            _scanner = new FileScanner();
            _storage = new DataStorageService();

            AddFolderCommand = new RelayCommand(async _ => await AddFolderAsync());
            SaveCommand = new RelayCommand(async _ => await SaveAsync());
            ClearCommand = new RelayCommand(async _ => await ClearAsync());
            FilterCommand = new RelayCommand(FilterByType);

            // Завантажуємо збережену колекцію при старті
            _ = LoadSavedCollectionAsync();
        }

        // ============================================================
        // МЕТОДИ
        // ============================================================

        /// <summary>
        /// Завантажує збережену колекцію при старті програми
        /// </summary>
        private async Task LoadSavedCollectionAsync()
        {
            var saved = await _storage.LoadMediaItemsAsync();

            if (saved.Count > 0)
            {
                AllMediaItems.Clear();
                foreach (var item in saved)
                    AllMediaItems.Add(item);

                ApplyFilter();
                UpdateStats();
                StatusText = $"Завантажено {saved.Count} файлів з колекції";
            }
        }

        /// <summary>
        /// Відкриває діалог вибору папки та сканує її
        /// </summary>
        private async Task AddFolderAsync()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Виберіть папку з медіафайлами"
            };

            if (dialog.ShowDialog() != true)
                return;

            string folderPath = dialog.FolderName;

            IsScanning = true;
            StatusText = "Сканування...";
            ScanProgress = 0;

            try
            {
                var files = await _scanner.ScanFolderAsync(folderPath);

                int added = 0;
                int skipped = 0;

                foreach (var file in files)
                {
                    // Перевіряємо, чи файл вже є в колекції
                    if (AllMediaItems.Any(x => x.FilePath == file))
                    {
                        skipped++;
                        continue;
                    }

                    var item = CreateMediaItem(file);
                    AllMediaItems.Add(item);
                    added++;
                }

                ApplyFilter();
                UpdateStats();

                // Автоматично зберігаємо після сканування
                await SaveAsync();

                StatusText = $"Додано {added} файлів" + (skipped > 0 ? $", пропущено {skipped}" : "");
            }
            catch (Exception ex)
            {
                StatusText = $"Помилка: {ex.Message}";
                MessageBox.Show($"Помилка сканування:\n{ex.Message}",
                                "Помилка",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
            finally
            {
                IsScanning = false;
            }
        }

        /// <summary>
        /// Створює MediaItem з файлу
        /// </summary>
        private MediaItem CreateMediaItem(string filePath)
        {
            var fileInfo = new FileInfo(filePath);

            return new MediaItem
            {
                FilePath = filePath,
                FileName = fileInfo.Name,
                FileExtension = fileInfo.Extension.ToLowerInvariant(),
                FileSize = fileInfo.Length,
                Type = _scanner.GetMediaType(filePath),
                Title = Path.GetFileNameWithoutExtension(filePath),
                DateAdded = DateTime.Now
            };
        }

        /// <summary>
        /// Зберігає колекцію у JSON
        /// </summary>
        private async Task SaveAsync()
        {
            try
            {
                await _storage.SaveMediaItemsAsync(AllMediaItems.ToList());
                StatusText = $"Збережено {AllMediaItems.Count} файлів";
            }
            catch (Exception ex)
            {
                StatusText = $"Помилка збереження: {ex.Message}";
            }
        }

        /// <summary>
        /// Очищає всю колекцію
        /// </summary>
        private async Task ClearAsync()
        {
            var result = MessageBox.Show(
                "Ви впевнені, що хочете очистити всю колекцію?",
                "Підтвердження",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            AllMediaItems.Clear();
            FilteredMediaItems.Clear();
            await _storage.ClearCollectionAsync();
            UpdateStats();
            StatusText = "Колекцію очищено";
        }

        /// <summary>
        /// Фільтрує колекцію за типом
        /// </summary>
        private void FilterByType(object parameter)
        {
            if (parameter is string typeStr)
            {
                SelectedFilter = typeStr switch
                {
                    "Film" => MediaType.Film,
                    "Book" => MediaType.Book,
                    "Music" => MediaType.Music,
                    "Image" => MediaType.Image,
                    "Document" => MediaType.Document,
                    _ => MediaType.Unknown
                };
            }
        }

        /// <summary>
        /// Застосовує пошук та фільтр до колекції
        /// </summary>
        private void ApplyFilter()
        {
            var query = AllMediaItems.AsEnumerable();

            // Фільтр за типом
            if (SelectedFilter != MediaType.Unknown)
                query = query.Where(x => x.Type == SelectedFilter);

            // Пошук за текстом
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string search = SearchText.ToLowerInvariant();
                query = query.Where(x =>
                    (x.Title?.ToLowerInvariant().Contains(search) ?? false) ||
                    (x.FileName?.ToLowerInvariant().Contains(search) ?? false) ||
                    (x.Author?.ToLowerInvariant().Contains(search) ?? false) ||
                    (x.Genre?.ToLowerInvariant().Contains(search) ?? false));
            }

            FilteredMediaItems.Clear();
            foreach (var item in query)
                FilteredMediaItems.Add(item);
        }

        /// <summary>
        /// Оновлює статистику (викликає OnPropertyChanged для всіх властивостей)
        /// </summary>
        private void UpdateStats()
        {
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(FilmCount));
            OnPropertyChanged(nameof(BookCount));
            OnPropertyChanged(nameof(MusicCount));
            OnPropertyChanged(nameof(ImageCount));
            OnPropertyChanged(nameof(FavoriteCount));
            OnPropertyChanged(nameof(TotalSize));
        }
    }
}