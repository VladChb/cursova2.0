using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using TagLib;
using MediaVault.Helpers;
using MediaVault.Models;
using MediaVault.Services;


namespace MediaVault.ViewModels
{
    public enum DateFilterType
    {
        All,
        Today,
        Week,
        Month
    }

    public enum SizeFilterType
    {
        All,
        Small,      // < 10 MB
        Medium,     // 10 MB - 500 MB
        Large       // > 500 MB
    }

    public class MainViewModel : ObservableObject
    {
        private readonly FileScanner _scanner;

        public bool IsEmpty => FilteredMediaItems.Count == 0;
        private readonly DataStorageService _storage;

        private readonly FileNameCleaner _cleaner;

        private readonly MusicTagService _tagService;
        private readonly MusicMetadataService _musicService;
        private readonly OpenLibraryService _bookService;

        private readonly TmdbService _tmdbService;

        private readonly HtmlReportService _reportService;



        public ObservableCollection<MediaItem> AllMediaItems { get; set; } = new();

        public ObservableCollection<MediaItem> FilteredMediaItems { get; set; } = new();

        public RelayCommand SortCommand { get; }

        private readonly DuplicateFinder _duplicateFinder;


        public ObservableCollection<FolderNode> RootFolders { get; set; } = new();

        private FolderNode _selectedFolder;
        public FolderNode SelectedFolder
        {
            get => _selectedFolder;
            set { if (SetProperty(ref _selectedFolder, value)) { CurrentPage = 1; ApplyFilter(); } }
        }

        public RelayCommand SelectFolderNodeCommand { get; }
        public RelayCommand ClearFolderFilterCommand { get; }



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

        // ===== ФІЛЬТРИ =====
        private int _minRating = 0;
        public int MinRating
        {
            get => _minRating;
            set
            {
                if (SetProperty(ref _minRating, value))
                {
                    CurrentPage = 1;
                    ApplyFilter();
                }
            }
        }

        private DateFilterType _dateFilter = DateFilterType.All;
        public DateFilterType DateFilter
        {
            get => _dateFilter;
            set
            {
                if (SetProperty(ref _dateFilter, value))
                {
                    CurrentPage = 1;
                    ApplyFilter();
                }
            }
        }

        // String version for XAML binding
        private string _dateFilterString = "All";
        public string DateFilterString
        {
            get => _dateFilterString;
            set
            {
                if (SetProperty(ref _dateFilterString, value))
                {
                    DateFilter = value switch
                    {
                        "Today" => DateFilterType.Today,
                        "Week" => DateFilterType.Week,
                        "Month" => DateFilterType.Month,
                        _ => DateFilterType.All
                    };
                }
            }
        }

        private SizeFilterType _sizeFilter = SizeFilterType.All;
        public SizeFilterType SizeFilter
        {
            get => _sizeFilter;
            set
            {
                if (SetProperty(ref _sizeFilter, value))
                {
                    CurrentPage = 1;
                    ApplyFilter();
                }
            }
        }

        // String version for XAML binding
        private string _sizeFilterString = "All";
        public string SizeFilterString
        {
            get => _sizeFilterString;
            set
            {
                if (SetProperty(ref _sizeFilterString, value))
                {
                    SizeFilter = value switch
                    {
                        "Small" => SizeFilterType.Small,
                        "Medium" => SizeFilterType.Medium,
                        "Large" => SizeFilterType.Large,
                        _ => SizeFilterType.All
                    };
                }
            }
        }

        private bool _showOnlyWithoutMetadata = false;
        public bool ShowOnlyWithoutMetadata
        {
            get => _showOnlyWithoutMetadata;
            set
            {
                if (SetProperty(ref _showOnlyWithoutMetadata, value))
                {
                    CurrentPage = 1;
                    ApplyFilter();
                }
            }
        }

        private bool _showOnlyFavorites = false;
        public bool ShowOnlyFavorites
        {
            get => _showOnlyFavorites;
            set
            {
                if (SetProperty(ref _showOnlyFavorites, value))
                {
                    CurrentPage = 1;
                    ApplyFilter();
                }
            }
        }

        public void ResetFilters()
        {
            MinRating = 0;
            DateFilterString = "All";
            SizeFilterString = "All";
            ShowOnlyWithoutMetadata = false;
            ShowOnlyFavorites = false;
            SearchText = string.Empty;
        }


        public int TotalCount => AllMediaItems.Count;
        public int FilmCount => AllMediaItems.Count(x => x.Type == MediaType.Film);
        public int ShortVideoCount => AllMediaItems.Count(x => x.Type == MediaType.ShortVideo);
        public int BookCount => AllMediaItems.Count(x => x.Type == MediaType.Book);
        public int MusicCount => AllMediaItems.Count(x => x.Type == MediaType.Music);
        public int ImageCount => AllMediaItems.Count(x => x.Type == MediaType.Image);
        public int DocumentCount => AllMediaItems.Count(x => x.Type == MediaType.Document);
        public int ProgramCount => AllMediaItems.Count(x => x.Type == MediaType.Program);
        public int FavoriteCount => AllMediaItems.Count(x => x.IsFavorite);

        public string ToolsButtonText => IsToolsMode ? "📂 Категорії" : "🧰 Інструменти";

        private int _processedFiles;
        public int ProcessedFiles
        {
            get => _processedFiles;
            set => SetProperty(ref _processedFiles, value);
        }

        private int _totalFilesToProcess;
        public int TotalFilesToProcess
        {
            get => _totalFilesToProcess;
            set => SetProperty(ref _totalFilesToProcess, value);
        }

        public string ProgressInfo => TotalFilesToProcess > 0
            ? $"{ProcessedFiles} / {TotalFilesToProcess} файлів"
            : "";

        public RelayCommand FocusSearchCommand { get; }
        public RelayCommand OpenHelpCommand { get; }
        public RelayCommand OpenStatsCommand { get; }

        private bool _isToolsMode;
        public bool IsToolsMode
        {
            get => _isToolsMode;
            set
            {
                if (SetProperty(ref _isToolsMode, value))
                {
                    OnPropertyChanged(nameof(IsCategoriesMode));
                    OnPropertyChanged(nameof(ToolsButtonText));
                }
            }
        }

        public bool IsCategoriesMode => !IsToolsMode;

        public RelayCommand ToggleToolsCommand { get; }

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

        public RelayCommand FindDuplicatesCommand { get; }

        public RelayCommand ExportReportCommand { get; }

        public RelayCommand RefreshMetadataCommand { get; }

        public RelayCommand RefreshMetadataForCategoryCommand { get; }
        public RelayCommand RefreshMetadataForFolderCommand { get; }

        public RelayCommand ClearCategoryCommand { get; }

        public RelayCommand RemoveFolderCommand { get; }
        public RelayCommand VerifyCollectionCommand { get; }
        public RelayCommand ResetFiltersCommand { get; }




        public MainViewModel()
        {
            _scanner = new FileScanner();
            _storage = new DataStorageService();
            _cleaner = new FileNameCleaner();


            _duplicateFinder = new DuplicateFinder();
            FindDuplicatesCommand = new RelayCommand(async _ => await FindDuplicatesAsync());

            _tagService = new MusicTagService();
            _musicService = new MusicMetadataService();
            _bookService = new OpenLibraryService();

            _reportService = new HtmlReportService();
            ExportReportCommand = new RelayCommand(async _ => await ExportReportAsync());

            string tmdbApiKey = EnvService.Get("TMDB_API_KEY");
            _tmdbService = new TmdbService(tmdbApiKey);

            AddFolderCommand = new RelayCommand(async _ => await AddFolderAsync());
            SaveCommand = new RelayCommand(async _ => await SaveAsync());
            ClearCommand = new RelayCommand(async _ => await ClearAsync());
            FilterCommand = new RelayCommand(FilterByType);
            SortCommand = new RelayCommand(SortItems);

            NextPageCommand = new RelayCommand(_ => NextPage(), _ => CanGoNext);
            PrevPageCommand = new RelayCommand(_ => PrevPage(), _ => CanGoPrev);
            GoToPageCommand = new RelayCommand(GoToPage);
            ChangePageSizeCommand = new RelayCommand(ChangePageSize);
            RefreshMetadataCommand = new RelayCommand(async _ => await RefreshMetadataAsync());
            ToggleToolsCommand = new RelayCommand(_ => IsToolsMode = !IsToolsMode);

            FocusSearchCommand = new RelayCommand(_ => FocusSearch());
            OpenHelpCommand = new RelayCommand(_ => OpenHelp());
            ResetFiltersCommand = new RelayCommand(_ => ResetFilters());
            OpenStatsCommand = new RelayCommand(_ => OpenStats());
            ClearCategoryCommand = new RelayCommand(async param => await ClearCategoryAsync(param));

            RemoveFolderCommand = new RelayCommand(async _ => await RemoveFolderAsync());
            VerifyCollectionCommand = new RelayCommand(_ => VerifyCollectionFiles());

            SelectFolderNodeCommand = new RelayCommand(param => { if (param is FolderNode node) SelectedFolder = node; });
            ClearFolderFilterCommand = new RelayCommand(_ => SelectedFolder = null);

            RefreshMetadataForCategoryCommand = new RelayCommand(async param =>
    await RefreshMetadataForCategoryAsync(param));

            RefreshMetadataForFolderCommand = new RelayCommand(async _ =>
                await RefreshMetadataForFolderAsync());

            _ = LoadSavedCollectionAsync();
        }

        private async Task FindDuplicatesAsync()
        {
            if (AllMediaItems.Count == 0)
            {
                MessageBox.Show("Колекція порожня", "Інформація",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            IsLoading = true;
            StatusText = "Пошук дублікатів...";

            try
            {
                var progress = new Progress<int>(p =>
                {
                    ScanProgress = p;
                    StatusText = $"Пошук дублікатів... {p}%";
                });

                var groups = await _duplicateFinder.FindDuplicatesAsync(
                    AllMediaItems.ToList(), progress);

                await SaveAsync();

                if (groups.Count == 0)
                {
                    MessageBox.Show("Дублікатів не знайдено!", "Результат",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var window = new Views.DuplicatesWindow(groups, _duplicateFinder, AllMediaItems)
                {
                    Owner = System.Windows.Application.Current.MainWindow
                };
                window.ShowDialog();

                ApplyFilter();
                UpdateStats();
                await SaveAsync();

                StatusText = $"Знайдено {groups.Count} груп дублікатів";
            }
            catch (Exception ex)
            {
                StatusText = $"Помилка: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                ScanProgress = 0;
            }
        }

        private async Task RefreshMetadataAsync()
        {
            if (AllMediaItems.Count == 0)
            {
                MessageBox.Show("Колекція порожня", "Інформація",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                "Оновити метадані для всіх файлів?\n\n" +
                "Програма повторно звернеться до API (TMDb, MusicBrainz) для файлів, " +
                "де не знайдено обкладинку або опис.\n\n" +
                "Це може зайняти деякий час.",
                "Оновлення метаданих",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            var itemsToUpdate = AllMediaItems
                .Where(x => string.IsNullOrEmpty(x.PosterUrl) ||
                            string.IsNullOrEmpty(x.Description))
                .ToList();

            if (itemsToUpdate.Count == 0)
            {
                MessageBox.Show("Всі файли вже мають повні метадані!", "Інформація",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            IsLoading = true;
            StatusText = $"Оновлення метаданих... 0/{itemsToUpdate.Count}";
            ScanProgress = 0;

            int updated = 0;
            int failed = 0;

            try
            {
                for (int i = 0; i < itemsToUpdate.Count; i++)
                {
                    var item = itemsToUpdate[i];

                    try
                    {
                        await UpdateItemMetadataAsync(item);
                        updated++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Помилка оновлення {item.Title}: {ex.Message}");
                        failed++;
                    }

                    int percent = (int)((double)(i + 1) / itemsToUpdate.Count * 100);
                    ScanProgress = percent;
                    StatusText = $"Оновлення метаданих... {i + 1}/{itemsToUpdate.Count}";

                    await Task.Delay(200);
                }

                await SaveAsync();
                ApplyFilter();
                UpdateStats();

                string message = $"Оновлено: {updated}";
                if (failed > 0)
                    message += $"\nНе вдалося: {failed}";

                MessageBox.Show(message, "Готово",
                                MessageBoxButton.OK, MessageBoxImage.Information);

                StatusText = $"Оновлено {updated} файлів";
            }
            catch (Exception ex)
            {
                StatusText = $"Помилка: {ex.Message}";
                MessageBox.Show($"Помилка оновлення:\n{ex.Message}",
                                "Помилка",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                ScanProgress = 0;
            }
        }

        private void FocusSearch()
        {
            FocusSearchRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OpenHelp()
        {
            OpenHelpRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OpenStats()
        {
            OpenStatsRequested?.Invoke(this, EventArgs.Empty);
        }


        private void BuildFolderTree()
        {
            RootFolders.Clear();

            var allPaths = AllMediaItems
                .Select(x => Path.GetDirectoryName(x.FilePath))
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            var root = new FolderNode { Name = "Усі файли", FullPath = "" };
            RootFolders.Add(root);

            foreach (var path in allPaths)
            {
                var parts = path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                AddPathToTree(root, parts, 0, "");
            }
        }

        private void AddPathToTree(FolderNode parent, string[] parts, int index, string currentPath)
        {
            if (index >= parts.Length) return;

            string part = parts[index];

            string newPath = string.IsNullOrEmpty(currentPath)
                ? part
                : currentPath + Path.DirectorySeparatorChar + part;

            var child = parent.Children.FirstOrDefault(c => c.Name == part);
            if (child == null)
            {
                child = new FolderNode
                {
                    Name = part,
                    FullPath = newPath
                };
                parent.Children.Add(child);
            }

            AddPathToTree(child, parts, index + 1, newPath);
        }
        private async Task RemoveFolderAsync()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Виберіть папку, яку треба видалити з колекції"
            };

            if (dialog.ShowDialog() != true)
                return;

            string folderPath = dialog.FolderName;

            var itemsToRemove = AllMediaItems
                .Where(x => x.FilePath.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (itemsToRemove.Count == 0)
            {
                MessageBox.Show("У цій папці немає файлів у колекції.", "Інформація",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Видалити {itemsToRemove.Count} файлів з папки:\n{folderPath}?\n\n" +
                "Файли на диску залишаться, але зникнуть з колекції.",
                "Підтвердження",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            foreach (var item in itemsToRemove)
            {
                AllMediaItems.Remove(item);
                FilteredMediaItems.Remove(item);
            }

            await SaveAsync();
            ApplyFilter();
            UpdateStats();

            StatusText = $"Видалено {itemsToRemove.Count} файлів з папки";
        }

        public event EventHandler FocusSearchRequested;
        public event EventHandler OpenHelpRequested;
        public event EventHandler OpenStatsRequested;

        private async Task UpdateItemMetadataAsync(MediaItem item)
        {
            string cleanTitle = _cleaner.CleanFileName(item.FilePath);
            int? year = _cleaner.ExtractYear(item.FilePath);

            if (!string.IsNullOrEmpty(cleanTitle))
                item.Title = cleanTitle;

            ClassifyVideoByDuration(item);
            ApplyLocalVisuals(item);

            if (item.Type is MediaType.Film or MediaType.ShortVideo)
                await FillFilmMetadataAsync(item, year);
            else if (item.Type == MediaType.Book)
                await FillBookMetadataAsync(item);
            else if (item.Type == MediaType.Music)
                await FillMusicMetadataAsync(item);
        }

        private void ClassifyVideoByDuration(MediaItem item)
        {
            if (!_scanner.IsVideoFile(item.FilePath))
                return;

            if (!item.DurationSeconds.HasValue)
                item.DurationSeconds = _scanner.ReadDurationSeconds(item.FilePath);

            item.Type = _scanner.GetMediaType(item.FilePath, item.DurationSeconds);
        }

        private async Task EnsureHashAsync(MediaItem item)
        {
            if (!string.IsNullOrEmpty(item.Sha256Hash))
                return;

            try
            {
                if (System.IO.File.Exists(item.FilePath))
                    item.Sha256Hash = await FileHashService.ComputeSha256Async(item.FilePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Хеш {item.FilePath}: {ex.Message}");
            }
        }

        private async Task ExportReportAsync()
        {
            if (AllMediaItems.Count == 0)
            {
                MessageBox.Show("Колекція порожня", "Інформація",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Зберегти звіт",
                Filter = "HTML файл|*.html",
                FileName = $"MediaVault_Report_{DateTime.Now:yyyy-MM-dd}.html"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                StatusText = "Генерація звіту...";

                await Task.Run(() =>
                {
                    _reportService.GenerateReport(AllMediaItems.ToList(), dialog.FileName);
                });

                StatusText = $"Звіт збережено: {dialog.FileName}";

                var result = MessageBox.Show(
                    "Звіт для друку збережено.\n\nВідкрити в браузері (Ctrl+P — друк)?",
                    "Готово",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = dialog.FileName,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка генерації звіту:\n{ex.Message}",
                                "Помилка",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                StatusText = "Помилка генерації звіту";
            }
        }

        private async Task ClearCategoryAsync(object parameter)
        {
            if (parameter is not string categoryStr)
                return;

            MediaType? targetType = categoryStr switch
            {
                "Film" => MediaType.Film,
                "ShortVideo" => MediaType.ShortVideo,
                "Book" => MediaType.Book,
                "Music" => MediaType.Music,
                "Image" => MediaType.Image,
                "Document" => MediaType.Document,
                "Program" => MediaType.Program,
                _ => null
            };

            if (targetType == null)
            {
                MessageBox.Show("Невідома категорія", "Помилка");
                return;
            }

            var itemsToRemove = AllMediaItems.Where(x => x.Type == targetType).ToList();

            if (itemsToRemove.Count == 0)
            {
                MessageBox.Show($"У категорії \"{categoryStr}\" немає файлів.", "Інформація",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Видалити {itemsToRemove.Count} файлів з категорії \"{categoryStr}\"?\n\n" +
                "Файли на диску залишаться, але зникнуть з колекції.",
                "Підтвердження",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            foreach (var item in itemsToRemove)
            {
                AllMediaItems.Remove(item);
                FilteredMediaItems.Remove(item);
            }

            await SaveAsync();
            ApplyFilter();
            UpdateStats();

            StatusText = $"Видалено {itemsToRemove.Count} файлів з категорії \"{categoryStr}\"";
        }
        private async Task LoadSavedCollectionAsync()
        {
            var saved = await _storage.LoadMediaItemsAsync();

            if (saved.Count > 0)
            {
                AllMediaItems.Clear();
                foreach (var item in saved)
                    AllMediaItems.Add(item);

                await Task.Run(() =>
                {
                    foreach (var item in AllMediaItems)
                    {
                        if (_scanner.IsVideoFile(item.FilePath))
                            ClassifyVideoByDuration(item);
                    }
                });

                foreach (var item in AllMediaItems)
                    ApplyLocalVisuals(item);

                BuildFolderTree();
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

            IsLoading = true;
            StatusText = "Підрахунок файлів...";


            var fileCount = await Task.Run(() =>
            {
                var options = new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = true
                };
                return Directory.EnumerateFiles(folderPath, "*.*", options)
                    .Count(f => _scanner.IsSupportedFile(f));
            });

            if (fileCount > 2000)
            {
                MessageBox.Show($"У папці знайдено {fileCount} файлів. Це занадто багато.\nМаксимум — 2000 файлів.",
                                "Обмеження", MessageBoxButton.OK, MessageBoxImage.Warning);
                IsLoading = false;
                return;
            }

            TotalFilesToProcess = fileCount;
            ProcessedFiles = 0;
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

                int added = 0;
                int skipped = 0;

                foreach (var file in files)
                {
                    if (AllMediaItems.Any(x => x.FilePath == file))
                    {
                        skipped++;
                        ProcessedFiles++;
                        continue;
                    }

                    var item = await CreateMediaItemAsync(file);
                    AllMediaItems.Add(item);
                    added++;
                    ProcessedFiles++;

                    dispatcher.Invoke(() =>
                    {
                        StatusText = $"Додано {added} з {TotalFilesToProcess}...";
                    }, System.Windows.Threading.DispatcherPriority.Background);
                }

                ApplyFilter();
                BuildFolderTree();
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
                ProcessedFiles = 0;
                TotalFilesToProcess = 0;
            }
        }

        private async Task<MediaItem> CreateMediaItemAsync(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            string cleanTitle = _cleaner.CleanFileName(filePath);
            int? year = _cleaner.ExtractYear(filePath);

            double? duration = null;
            if (_scanner.IsVideoFile(filePath) ||
        new[] { ".mp3", ".flac", ".wav", ".aac", ".ogg", ".m4a", ".wma" }
            .Contains(fileInfo.Extension.ToLowerInvariant()))
                duration = _scanner.ReadDurationSeconds(filePath);

            var mediaType = _scanner.GetMediaType(filePath, duration);

            var item = new MediaItem
            {
                FilePath = filePath,
                FileName = fileInfo.Name,
                FileExtension = fileInfo.Extension.ToLowerInvariant(),
                FileSize = fileInfo.Length,
                Type = mediaType,
                Title = cleanTitle ?? Path.GetFileNameWithoutExtension(filePath),
                Year = year,
                DateAdded = DateTime.Now,
                DurationSeconds = duration,
                IsIdentified = false
            };

            ApplyLocalVisuals(item);

            if (mediaType == MediaType.Film || mediaType == MediaType.ShortVideo)
            {
                await FillFilmMetadataAsync(item, year);

                if (item.IsIdentified)
                    item.Type = MediaType.Film;
                else
                    item.Type = MediaType.ShortVideo;
            }
            else if (mediaType == MediaType.Book)
                await FillBookMetadataAsync(item);
            else if (mediaType == MediaType.Music)
                await FillMusicMetadataAsync(item);

            return item;
        }

        private void ApplyLocalVisuals(MediaItem item)
        {
            if (item.Type == MediaType.Image && System.IO.File.Exists(item.FilePath))
            {
                item.PosterUrl = ThumbnailService.GetThumbnailPath(item.FilePath);
                return;
            }

            if (item.Type == MediaType.Program && string.IsNullOrEmpty(item.PosterUrl))
            {
                string icon = IconExtractor.ExtractToPoster(item.FilePath, item.Id);
                if (!string.IsNullOrEmpty(icon))
                    item.PosterUrl = icon;
            }
        }

        private async Task FillFilmMetadataAsync(MediaItem item, int? year)
        {
            item.IsIdentified = false; 

            foreach (var query in _cleaner.GetSearchQueries(item.FilePath, item.Title))
            {
                var tmdbResult = await _tmdbService.SearchMovieAsync(query, year);
                if (tmdbResult == null)
                    continue;

                item.TmdbId = tmdbResult.TmdbId;
                item.Title = tmdbResult.Title ?? item.Title;
                item.Description = tmdbResult.Overview ?? item.Description;
                item.PosterPath = tmdbResult.PosterPath;
                item.PosterUrl = _tmdbService.GetPosterUrl(tmdbResult.PosterPath);
                item.VoteAverage = tmdbResult.VoteAverage;
                item.Year = tmdbResult.ReleaseDate?.Year ?? year;
                item.IsIdentified = true; 

                if (tmdbResult.Genres != null && tmdbResult.Genres.Count > 0)
                    item.Genre = string.Join(", ", tmdbResult.Genres.ConvertAll(g => g.Name));

                return;
            }
        }

        private async Task FillBookMetadataAsync(MediaItem item)
        {
            if (string.IsNullOrEmpty(item.Isbn))
                item.Isbn = PdfIsbnExtractor.ExtractIsbn(item.FilePath);

            BookSearchResult book = null;
            if (!string.IsNullOrEmpty(item.Isbn))
                book = await _bookService.SearchByIsbnAsync(item.Isbn);

            if (book == null && !string.IsNullOrEmpty(item.Title))
            {
                foreach (var query in _cleaner.GetSearchQueries(item.FilePath, item.Title))
                {
                    book = await _bookService.SearchBookAsync(query, item.Author);
                    if (book != null)
                        break;
                }
            }

            if (book == null)
                return;

            if (!string.IsNullOrEmpty(book.Title))
                item.Title = book.Title;
            if (!string.IsNullOrEmpty(book.Author))
                item.Author = book.Author;
            if (!string.IsNullOrEmpty(book.Description))
                item.Description = book.Description;
            if (!string.IsNullOrEmpty(book.PosterUrl))
                item.PosterUrl = book.PosterUrl;
            if (book.Year.HasValue)
                item.Year = book.Year;
            if (!string.IsNullOrEmpty(book.Isbn))
                item.Isbn = book.Isbn;
            item.IsIdentified = true;
        }

        private async Task FillMusicMetadataAsync(MediaItem item)
        {
            var tags = _tagService.ReadTags(item.FilePath);
            if (tags != null && !string.IsNullOrEmpty(tags.Title))
            {
                item.Title = tags.Title;
                item.Author = tags.Artist ?? item.Author;
                item.Description = tags.Album ?? item.Description;
                if (tags.Year > 0)
                    item.Year = tags.Year;
                if (!string.IsNullOrEmpty(tags.Genre))
                    item.Genre = tags.Genre;
            }
            else
            {
                var parsed = _cleaner.ParseMusicFileName(item.FilePath);
                if (!string.IsNullOrEmpty(parsed.Artist))
                    item.Author = parsed.Artist;
                if (!string.IsNullOrEmpty(parsed.Title))
                    item.Title = parsed.Title;
            }

            if (string.IsNullOrEmpty(item.Author))
                item.Author = "Невідомий виконавець";

            var queries = new List<(string Title, string Artist)>
            {
                (item.Title, item.Author)
            };

            foreach (var q in _cleaner.GetSearchQueries(item.FilePath, item.Title))
                queries.Add((q, item.Author == "Невідомий виконавець" ? "" : item.Author));

            foreach (var (title, artist) in queries.Distinct())
            {
                if (string.IsNullOrWhiteSpace(title))
                    continue;

                var result = await _musicService.SearchAsync(item.FilePath, title, artist ?? "");
                if (result == null)
                    continue;

                if (!string.IsNullOrEmpty(result.CoverUrl))
                    item.PosterUrl = result.CoverUrl;
                if (!string.IsNullOrEmpty(result.TrackUrl))
                    item.TrackUrl = result.TrackUrl;
                if (!string.IsNullOrEmpty(result.Album))
                    item.Description = result.Album;
                if (!string.IsNullOrEmpty(result.Title))
                    item.Title = result.Title;
                if (!string.IsNullOrEmpty(result.Artist))
                    item.Author = result.Artist;
                if (!string.IsNullOrEmpty(result.Genre))
                    item.Genre = result.Genre;
                if (result.Year.HasValue)
                    item.Year = result.Year;
                return;
            }
        }

        private void VerifyCollectionFiles()
        {
            var missingFiles = AllMediaItems
                .Where(item => string.IsNullOrWhiteSpace(item.FilePath) || !System.IO.File.Exists(item.FilePath))
                .ToList();

            if (missingFiles.Count == 0)
            {
                StatusText = "Перевірку завершено: відсутніх файлів не знайдено";
                MessageBox.Show(StatusText, "Перевірка колекції", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            const int maxDisplayedPaths = 25;
            string missingPaths = string.Join(Environment.NewLine, missingFiles
                .Take(maxDisplayedPaths)
                .Select(item => item.FilePath ?? "(шлях не вказано)"));

            string message = $"Знайдено відсутніх файлів: {missingFiles.Count}\n\n{missingPaths}";
            if (missingFiles.Count > maxDisplayedPaths)
                message += $"\n… та ще {missingFiles.Count - maxDisplayedPaths}";

            StatusText = $"Знайдено відсутніх файлів: {missingFiles.Count}";
            MessageBox.Show(message, "Перевірка колекції", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    ShowOnlyFavorites = !ShowOnlyFavorites;
                    return;
                }

                SelectedFilter = typeStr switch
                {
                    "Film" => MediaType.Film,
                    "ShortVideo" => MediaType.ShortVideo,
                    "Book" => MediaType.Book,
                    "Music" => MediaType.Music,
                    "Image" => MediaType.Image,
                    "Document" => MediaType.Document,
                    "Program" => MediaType.Program,
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

        public void ApplyFilter()
        {
            var query = AllMediaItems.AsEnumerable();

            // 1. Фільтр за категорією
            if (SelectedFilter != MediaType.Unknown)
                query = query.Where(x => x.Type == SelectedFilter);

            // 2. Фільтр за папкою (якщо вибрано в дереві)
            if (SelectedFolder != null && !string.IsNullOrEmpty(SelectedFolder.FullPath))
                query = query.Where(x => x.FilePath.StartsWith(SelectedFolder.FullPath, StringComparison.OrdinalIgnoreCase));

            // 3. Пошук
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string search = SearchText.Trim().ToLowerInvariant();
                string searchHash = search.Replace("-", "").Replace(" ", "");
                query = query.Where(x =>
                    (x.Title?.ToLowerInvariant().Contains(search) ?? false) ||
                    (x.FileName?.ToLowerInvariant().Contains(search) ?? false) ||
                    (x.Author?.ToLowerInvariant().Contains(search) ?? false) ||
                    (x.Genre?.ToLowerInvariant().Contains(search) ?? false) ||
                    (x.Description?.ToLowerInvariant().Contains(search) ?? false) ||
                    (!string.IsNullOrEmpty(x.Sha256Hash) &&
                     x.Sha256Hash.Contains(searchHash, StringComparison.OrdinalIgnoreCase)));
            }

            // 4. Фільтр за рейтингом
            if (MinRating > 0)
                query = query.Where(x => x.Rating >= MinRating);

            // 5. Фільтр за датою
            if (DateFilter != DateFilterType.All)
            {
                var now = DateTime.Now;
                DateTime filterDate = DateFilter switch
                {
                    DateFilterType.Today => now.Date,
                    DateFilterType.Week => now.AddDays(-7),
                    DateFilterType.Month => now.AddMonths(-1),
                    _ => DateTime.MinValue
                };
                query = query.Where(x => x.DateAdded >= filterDate);
            }

            // 6. Фільтр за розміром файлу
            if (SizeFilter != SizeFilterType.All)
            {
                query = SizeFilter switch
                {
                    SizeFilterType.Small => query.Where(x => x.FileSize < 10 * 1024 * 1024),      // < 10 MB
                    SizeFilterType.Medium => query.Where(x => x.FileSize >= 10 * 1024 * 1024 && x.FileSize < 500 * 1024 * 1024),
                    SizeFilterType.Large => query.Where(x => x.FileSize >= 500 * 1024 * 1024),    // > 500 MB
                    _ => query
                };
            }

            // 7. Фільтр за улюбленими
            if (ShowOnlyFavorites)
            {
                query = query.Where(x => x.IsFavorite);
            }

            // 8. Фільтр за наявністю метаданих (приховати файли без метаданих)
            if (ShowOnlyWithoutMetadata)
            {
                query = query.Where(x =>
                    (!string.IsNullOrEmpty(x.Title) && !string.IsNullOrWhiteSpace(x.Title)) ||
                    (!string.IsNullOrEmpty(x.Author) && !string.IsNullOrWhiteSpace(x.Author)) ||
                    (!string.IsNullOrEmpty(x.Genre) && !string.IsNullOrWhiteSpace(x.Genre)) ||
                    (!string.IsNullOrEmpty(x.Description) && !string.IsNullOrWhiteSpace(x.Description)));
            }

            // 9. Сортування
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

        private MusicTags ReadTagsFromFile(string filePath)
        {
            try
            {
                using var tfile = TagLib.File.Create(filePath);
                return new MusicTags
                {
                    Title = tfile.Tag.Title,
                    Artist = tfile.Tag.FirstPerformer ?? tfile.Tag.FirstAlbumArtist,
                    Album = tfile.Tag.Album,
                    Year = (int)tfile.Tag.Year,
                    Genre = tfile.Tag.FirstGenre
                };
            }
            catch
            {
                return null;
            }
        }

        public class MusicTags
        {
            public string Title { get; set; }
            public string Artist { get; set; }
            public string Album { get; set; }
            public int? Year { get; set; }
            public string Genre { get; set; }
        }

        public void UpdateStats()
        {
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(FilmCount));
            OnPropertyChanged(nameof(ShortVideoCount));
            OnPropertyChanged(nameof(BookCount));
            OnPropertyChanged(nameof(MusicCount));
            OnPropertyChanged(nameof(ImageCount));
            OnPropertyChanged(nameof(DocumentCount));
            OnPropertyChanged(nameof(ProgramCount));
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


        // ============================================================
        // ОНОВЛЕННЯ МЕТАДАНИХ ДЛЯ КОНКРЕТНОЇ КАТЕГОРІЇ
        // ============================================================
        private async Task RefreshMetadataForCategoryAsync(object parameter)
        {
            if (parameter is not string categoryStr)
                return;

            MediaType? targetType = categoryStr switch
            {
                "Film" => MediaType.Film,
                "ShortVideo" => MediaType.ShortVideo,
                "Book" => MediaType.Book,
                "Music" => MediaType.Music,
                "Image" => MediaType.Image,
                "Document" => MediaType.Document,
                "Program" => MediaType.Program,
                _ => null
            };

            if (targetType == null)
            {
                MessageBox.Show("Невідома категорія", "Помилка");
                return;
            }

            var itemsToUpdate = AllMediaItems
                .Where(x => x.Type == targetType)
                .Where(x => string.IsNullOrEmpty(x.PosterUrl) || string.IsNullOrEmpty(x.Description))
                .ToList();

            if (itemsToUpdate.Count == 0)
            {
                MessageBox.Show($"У категорії \"{categoryStr}\" немає файлів, які потребують оновлення.",
                                "Інформація", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Оновити метадані для {itemsToUpdate.Count} файлів у категорії \"{categoryStr}\"?",
                "Підтвердження",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            await UpdateMetadataBatchAsync(itemsToUpdate, $"категорії \"{categoryStr}\"");
        }

        // ============================================================
        // ОНОВЛЕННЯ МЕТАДАНИХ ДЛЯ КОНКРЕТНОЇ ПАПКИ
        // ============================================================
        private async Task RefreshMetadataForFolderAsync()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Виберіть папку для оновлення метаданих"
            };

            if (dialog.ShowDialog() != true)
                return;

            string folderPath = dialog.FolderName;

            var itemsToUpdate = AllMediaItems
                .Where(x => x.FilePath.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase))
                .Where(x => string.IsNullOrEmpty(x.PosterUrl) || string.IsNullOrEmpty(x.Description))
                .ToList();

            if (itemsToUpdate.Count == 0)
            {
                MessageBox.Show("У цій папці немає файлів, які потребують оновлення.",
                                "Інформація", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Оновити метадані для {itemsToUpdate.Count} файлів у папці:\n{folderPath}?",
                "Підтвердження",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            await UpdateMetadataBatchAsync(itemsToUpdate, $"папки \"{Path.GetFileName(folderPath)}\"");
        }

        // ============================================================
        // СПІЛЬНИЙ МЕТОД ДЛЯ ПАКЕТНОГО ОНОВЛЕННЯ
        // ============================================================
        private async Task UpdateMetadataBatchAsync(List<MediaItem> itemsToUpdate, string contextName)
        {
            IsLoading = true;
            StatusText = $"Оновлення метаданих ({contextName})... 0/{itemsToUpdate.Count}";
            ScanProgress = 0;
            TotalFilesToProcess = itemsToUpdate.Count;
            ProcessedFiles = 0;

            int updated = 0;
            int failed = 0;

            try
            {
                for (int i = 0; i < itemsToUpdate.Count; i++)
                {
                    var item = itemsToUpdate[i];

                    try
                    {
                        await UpdateItemMetadataAsync(item);
                        updated++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Помилка оновлення {item.Title}: {ex.Message}");
                        failed++;
                    }

                    ProcessedFiles = i + 1;
                    int percent = (int)((double)(i + 1) / itemsToUpdate.Count * 100);
                    ScanProgress = percent;
                    StatusText = $"Оновлення ({contextName})... {i + 1}/{itemsToUpdate.Count}";

                    await Task.Delay(200);
                }

                await SaveAsync();
                ApplyFilter();
                UpdateStats();

                string message = $"Оновлено: {updated}";
                if (failed > 0)
                    message += $"\nНе вдалося: {failed}";

                MessageBox.Show(message, "Готово", MessageBoxButton.OK, MessageBoxImage.Information);

                StatusText = $"Оновлено {updated} файлів ({contextName})";
            }
            catch (Exception ex)
            {
                StatusText = $"Помилка: {ex.Message}";
                MessageBox.Show($"Помилка оновлення:\n{ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                ScanProgress = 0;
                ProcessedFiles = 0;
                TotalFilesToProcess = 0;
            }
        }


        public class FolderNode : ObservableObject
        {
            public string Name { get; set; }
            public string FullPath { get; set; }
            public ObservableCollection<FolderNode> Children { get; set; } = new();
            private bool _isExpanded;
            public bool IsExpanded { get => _isExpanded; set => SetProperty(ref _isExpanded, value); }
            private bool _isSelected;
            public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
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