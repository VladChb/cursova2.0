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

        public bool IsEmpty => FilteredMediaItems.Count == 0;
        private readonly DataStorageService _storage;

        private readonly FileNameCleaner _cleaner;

        public ObservableCollection<MediaItem> AllMediaItems { get; set; } = new();

        public int DocumentCount => AllMediaItems.Count(x => x.Type == MediaType.Document);

        public ObservableCollection<MediaItem> FilteredMediaItems { get; set; } = new();

        public RelayCommand SortCommand { get; }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    CurrentPage = 1;
                    ApplyFilter();
                }
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

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private int _scanProgress;
        public int ScanProgress
        {
            get => _scanProgress;
            set => SetProperty(ref _scanProgress, value);
        }

        private string _sortBy = "Title";
        public string SortBy
        {
            get => _sortBy;
            set
            {
                if (SetProperty(ref _sortBy, value))
                    ApplyFilter();
            }
        }
      

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

        private void NextPage()
        {
            if (CanGoNext)
                CurrentPage++;
        }

        private void PrevPage()
        {
            if (CanGoPrev)
                CurrentPage--;
        }

        private void GoToPage(object parameter)
        {
            if (parameter is int page)
            {
                if (page >= 1 && page <= TotalPages)
                    CurrentPage = page;
            }
            else if (parameter is string pageStr && int.TryParse(pageStr, out int pageNum))
            {
                if (pageNum >= 1 && pageNum <= TotalPages)
                    CurrentPage = pageNum;
            }
        }

        private void ChangePageSize(object parameter)
        {
            if (parameter is string sizeStr && int.TryParse(sizeStr, out int sizeNum))
            {
                PageSize = sizeNum;
            }
        }

        public RelayCommand AddFolderCommand { get; }
        public RelayCommand SaveCommand { get; }
        public RelayCommand ClearCommand { get; }
        public RelayCommand FilterCommand { get; }

 
        public MainViewModel()
        {
            _scanner = new FileScanner();
            _storage = new DataStorageService();
            _cleaner = new FileNameCleaner();

            AddFolderCommand = new RelayCommand(async _ => await AddFolderAsync());
            SaveCommand = new RelayCommand(async _ => await SaveAsync());
            ClearCommand = new RelayCommand(async _ => await ClearAsync());
            FilterCommand = new RelayCommand(FilterByType);
            SortCommand = new RelayCommand(SortItems);

            NextPageCommand = new RelayCommand(_ => NextPage(), _ => CanGoNext);
            PrevPageCommand = new RelayCommand(_ => PrevPage(), _ => CanGoPrev);
            GoToPageCommand = new RelayCommand(GoToPage);
            ChangePageSizeCommand = new RelayCommand(ChangePageSize);

            _ = LoadSavedCollectionAsync();
        }

        private async Task LoadSavedCollectionAsync()
        {
            var saved = await _storage.LoadMediaItemsAsync();

            if (saved.Count > 0)
            {
                AllMediaItems.Clear();
                foreach (var item in saved)
                    AllMediaItems.Add(item);

                CurrentPage = 1;
                ApplyFilter();
                UpdateStats();
                StatusText = $"Завантажено {saved.Count} файлів з колекції";
            }
        }

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
            IsLoading = true;
            StatusText = "Сканування...";
            ScanProgress = 0;

            try
            {
                var dispatcher = System.Windows.Application.Current.Dispatcher;

                var files = await _scanner.ScanFolderAsync(folderPath, (percent) =>
                {
                    dispatcher.Invoke(() =>
                    {
                        ScanProgress = percent;
                        StatusText = $"Сканування... {percent}%";
                    }, System.Windows.Threading.DispatcherPriority.Background);
                });

                StatusText = "Додавання файлів...";

                int added = 0;
                int skipped = 0;

                foreach (var file in files)
                {
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

                await SaveAsync();

                StatusText = $"Додано {added} файлів" + (skipped > 0 ? $", пропущено {skipped}" : "");
            }
            catch (Exception ex)
            {
                StatusText = $"Помилка: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
                IsLoading = false;
                ScanProgress = 0;
            }
        }

        private MediaItem CreateMediaItem(string filePath)
        {
            var fileInfo = new FileInfo(filePath);

            string cleanTitle = _cleaner.CleanFileName(filePath);
            int? year = _cleaner.ExtractYear(filePath);

            return new MediaItem
            {
                FilePath = filePath,
                FileName = fileInfo.Name,
                FileExtension = fileInfo.Extension.ToLowerInvariant(),
                FileSize = fileInfo.Length,
                Type = _scanner.GetMediaType(filePath),
                Title = cleanTitle ?? Path.GetFileNameWithoutExtension(filePath),
                Year = year,
                DateAdded = DateTime.Now
            };
        }

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

        private void FilterByType(object parameter)
        {
            CurrentPage = 1;

            if (parameter is string typeStr)
            {
                if (typeStr == "Favorite")
                {
                    FilteredMediaItems.Clear();
                    var favorites = AllMediaItems.Where(x => x.IsFavorite).ToList();
                    TotalFilteredCount = favorites.Count;

                    foreach (var item in favorites.Skip((CurrentPage - 1) * PageSize).Take(PageSize))
                        FilteredMediaItems.Add(item);
                    return;
                }

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

        private void SortItems(object parameter)
        {
            CurrentPage = 1; 

            if (parameter is string sortBy)
                SortBy = sortBy;
        }

        private void ApplyFilter()
        {
            var query = AllMediaItems.AsEnumerable();

            if (SelectedFilter != MediaType.Unknown)
                query = query.Where(x => x.Type == SelectedFilter);

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string search = SearchText.ToLowerInvariant();
                query = query.Where(x =>
                    (x.Title?.ToLowerInvariant().Contains(search) ?? false) ||
                    (x.FileName?.ToLowerInvariant().Contains(search) ?? false) ||
                    (x.Author?.ToLowerInvariant().Contains(search) ?? false) ||
                    (x.Genre?.ToLowerInvariant().Contains(search) ?? false));
            }

            query = SortBy switch
            {
                "Title" => query.OrderBy(x => x.Title),
                "TitleDesc" => query.OrderByDescending(x => x.Title),
                "Size" => query.OrderByDescending(x => x.FileSize),
                "SizeAsc" => query.OrderBy(x => x.FileSize),
                "Date" => query.OrderByDescending(x => x.DateAdded),
                "DateAsc" => query.OrderBy(x => x.DateAdded),
                "Rating" => query.OrderByDescending(x => x.Rating),
                _ => query.OrderBy(x => x.Title)
            };

            TotalFilteredCount = query.Count();

            var pagedQuery = query
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize);

            FilteredMediaItems.Clear();
            foreach (var item in pagedQuery)
                FilteredMediaItems.Add(item);

            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(PageInfo));
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(CanGoPrev));
        }

        public async Task SaveChangesAsync()
        {
            await SaveAsync();
            UpdateStats();
        }

        public async Task RemoveItemAsync(MediaItem item)
        {
            if (item == null) return;

            AllMediaItems.Remove(item);
            FilteredMediaItems.Remove(item);

            await SaveAsync();
            UpdateStats();
        }

        private void UpdateStats()
        {
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(FilmCount));
            OnPropertyChanged(nameof(BookCount));
            OnPropertyChanged(nameof(MusicCount));
            OnPropertyChanged(nameof(ImageCount));
            OnPropertyChanged(nameof(DocumentCount));    
            OnPropertyChanged(nameof(FavoriteCount));
            OnPropertyChanged(nameof(TotalSize));
        }

        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                if (SetProperty(ref _currentPage, value))
                {
                    ApplyFilter();
                    OnPropertyChanged(nameof(PageInfo));
                    OnPropertyChanged(nameof(CanGoNext));
                    OnPropertyChanged(nameof(CanGoPrev));
                }
            }
        }

        private int _pageSize = 30;
        public int PageSize
        {
            get => _pageSize;
            set
            {
                if (SetProperty(ref _pageSize, value))
                {
                    CurrentPage = 1;
                    ApplyFilter();
                    OnPropertyChanged(nameof(PageInfo));
                    OnPropertyChanged(nameof(TotalPages));
                }
            }
        }

        private int _totalFilteredCount;
        public int TotalFilteredCount
        {
            get => _totalFilteredCount;
            set
            {
                if (SetProperty(ref _totalFilteredCount, value))
                {
                    OnPropertyChanged(nameof(TotalPages));
                    OnPropertyChanged(nameof(PageInfo));
                    OnPropertyChanged(nameof(CanGoNext));
                }
            }
        }

        public int TotalPages => TotalFilteredCount == 0
            ? 1
            : (int)Math.Ceiling((double)TotalFilteredCount / PageSize);

        public bool CanGoNext => CurrentPage < TotalPages;
        public bool CanGoPrev => CurrentPage > 1;

        public string PageInfo => $"Сторінка {CurrentPage} з {TotalPages}";

        public RelayCommand NextPageCommand { get; }
        public RelayCommand PrevPageCommand { get; }
        public RelayCommand GoToPageCommand { get; }
        public RelayCommand ChangePageSizeCommand { get; }
    }
}