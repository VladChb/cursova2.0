using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MediaVault.Helpers;
using MediaVault.Models;
using MediaVault.Services;
using MediaVault.Views;

namespace MediaVault.ViewModels
{
    public class SecurityScannerViewModel : ObservableObject
    {
        private readonly WindowsDefenderService _windowsDefenderService;
        private readonly VirusTotalService _virusTotalService;
        private readonly ObservableCollection<MediaItem> _mediaItems;
        private CancellationTokenSource _cancellationTokenSource;
        private TaskCompletionSource<bool> _pauseCompletionSource;
        private bool _isPaused;
        private SecurityItem _selectedScanItem;
        private string _currentFileStatus;
        private int _currentFileProgress;
        private bool _isCurrentFileProgressIndeterminate;

        public string CurrentFileStatus
        {
            get => _currentFileStatus;
            private set => SetProperty(ref _currentFileStatus, value);
        }

        public int CurrentFileProgress
        {
            get => _currentFileProgress;
            private set => SetProperty(ref _currentFileProgress, value);
        }

        public bool IsCurrentFileProgressIndeterminate
        {
            get => _isCurrentFileProgressIndeterminate;
            private set => SetProperty(ref _isCurrentFileProgressIndeterminate, value);
        }

        public SecurityItem SelectedScanItem
        {
            get => _selectedScanItem;
            set => SetProperty(ref _selectedScanItem, value);
        }

        public bool IsPaused
        {
            get => _isPaused;
            private set
            {
                if (SetProperty(ref _isPaused, value))
                {
                    OnPropertyChanged(nameof(CanResume));
                }
            }
        }

        public bool CanResume => IsScanning && IsPaused;

        public ObservableCollection<SecurityItem> ScanResults { get; set; } = new();
        public ObservableCollection<SecurityItem> FilteredResults { get; set; } = new();

        public int CompletedFilesCount => ScanResults.Count;
        public int SafeFilesCount => ScanResults.Count(item => item.Level == SecurityLevel.Safe);
        public int SuspiciousFilesCount => ScanResults.Count(item => item.Level == SecurityLevel.Suspicious);
        public int DangerousFilesCount => ScanResults.Count(item => item.Level == SecurityLevel.Dangerous);
        public int UnknownFilesCount => ScanResults.Count(item => item.Level == SecurityLevel.Unknown);
        public int VirusTotalFilesCount => ScanResults.Count(item => GetScannerDisplayName(item.ScannerName) == "VirusTotal");
        public int WindowsDefenderFilesCount => ScanResults.Count(item => GetScannerDisplayName(item.ScannerName) == "Windows Defender");
        public int UnattributedFilesCount => ScanResults.Count(item => GetScannerDisplayName(item.ScannerName) == "Не визначено");

        private bool _isScanning;
        public bool IsScanning
        {
            get => _isScanning;
            set => SetProperty(ref _isScanning, value);
        }

        private string _statusText = "Готово до сканування";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private int _currentProgress;
        public int CurrentProgress
        {
            get => _currentProgress;
            set => SetProperty(ref _currentProgress, value);
        }

        private int _totalFiles;
        public int TotalFiles
        {
            get => _totalFiles;
            set => SetProperty(ref _totalFiles, value);
        }

        private int _scannedFiles;
        public int ScannedFiles
        {
            get => _scannedFiles;
            set
            {
                if (SetProperty(ref _scannedFiles, value))
                {
                    CurrentProgress = TotalFiles > 0 ? (int)((double)ScannedFiles / TotalFiles * 100) : 0;
                }
            }
        }

        private SecurityLevel _filterLevel = SecurityLevel.Unknown;
        public SecurityLevel FilterLevel
        {
            get => _filterLevel;
            set
            {
                if (SetProperty(ref _filterLevel, value))
                    ApplyFilter();
            }
        }

        private bool _showSafeFiles = true;
        public bool ShowSafeFiles
        {
            get => _showSafeFiles;
            set
            {
                if (SetProperty(ref _showSafeFiles, value))
                    ApplyFilter();
            }
        }

        public class HashedFile
        {
            public string FilePath { get; set; }
            public string Sha256 { get; set; }
            public FileInfo Info { get; set; }
        }

        private bool _showSuspiciousFiles = true;
        public bool ShowSuspiciousFiles
        {
            get => _showSuspiciousFiles;
            set
            {
                if (SetProperty(ref _showSuspiciousFiles, value))
                    ApplyFilter();
            }
        }

        private bool _showDangerousFiles = true;
        public bool ShowDangerousFiles
        {
            get => _showDangerousFiles;
            set
            {
                if (SetProperty(ref _showDangerousFiles, value))
                    ApplyFilter();
            }
        }

        private bool _showUnknownFiles = true;
        public bool ShowUnknownFiles
        {
            get => _showUnknownFiles;
            set
            {
                if (SetProperty(ref _showUnknownFiles, value))
                    ApplyFilter();
            }
        }

        private string _selectedSortBy = "FileName";
        public string SelectedSortBy
        {
            get => _selectedSortBy;
            set
            {
                if (SetProperty(ref _selectedSortBy, value))
                    ApplyFilter();
            }
        }

        public RelayCommand ScanFolderCommand { get; }
        public RelayCommand ScanCollectionCommand { get; }
        public RelayCommand ClearResultsCommand { get; }
        public RelayCommand CancelScanCommand { get; }
        public RelayCommand PauseScanCommand { get; }
        public RelayCommand ResumeScanCommand { get; }
        public RelayCommand OpenDetailsCommand { get; }
        public RelayCommand ExportReportCommand { get; }
        public RelayCommand OpenInExplorerCommand { get; }

        public SecurityScannerViewModel(ObservableCollection<MediaItem> mediaItems, string virusTotalApiKey)
        {
            _mediaItems = mediaItems;
            _windowsDefenderService = new WindowsDefenderService();
            _virusTotalService = new VirusTotalService(virusTotalApiKey);
            ScanResults.CollectionChanged += (_, _) => RefreshSummary();

            ScanFolderCommand = new RelayCommand(_ => ScanFolderAsync().ConfigureAwait(false));
            ScanCollectionCommand = new RelayCommand(_ => ScanCollectionAsync(mediaItems).ConfigureAwait(false));
            ClearResultsCommand = new RelayCommand(_ => ClearResults());
            CancelScanCommand = new RelayCommand(_ => CancelScan());
            PauseScanCommand = new RelayCommand(_ => PauseScan());
            ResumeScanCommand = new RelayCommand(_ => ResumeScan());
            OpenDetailsCommand = new RelayCommand(param =>
            {
                if (param is SecurityItem item)
                    OpenDetails(item);
            });
            ExportReportCommand = new RelayCommand(_ => ExportReportAsync().ConfigureAwait(false));
            OpenInExplorerCommand = new RelayCommand(param =>
            {
                if (param is SecurityItem item)
                    OpenInExplorer(item);
            });

            // ===== ДІАГНОСТИКА VT ПРИ СТВОРЕННІ =====
            System.Diagnostics.Debug.WriteLine(
                $"[SecurityScanner] VT HasApiKey={_virusTotalService.HasApiKey}, " +
                $"CachedResults={_virusTotalService.GetCachedResultsCount()}");
        }

        private void RefreshSummary()
        {
            OnPropertyChanged(nameof(CompletedFilesCount));
            OnPropertyChanged(nameof(SafeFilesCount));
            OnPropertyChanged(nameof(SuspiciousFilesCount));
            OnPropertyChanged(nameof(DangerousFilesCount));
            OnPropertyChanged(nameof(UnknownFilesCount));
            OnPropertyChanged(nameof(VirusTotalFilesCount));
            OnPropertyChanged(nameof(WindowsDefenderFilesCount));
            OnPropertyChanged(nameof(UnattributedFilesCount));
        }

        private static string GetScannerDisplayName(string scannerName)
        {
            if (string.IsNullOrWhiteSpace(scannerName))
                return "Не визначено";

            if (scannerName.StartsWith("Windows Defender", StringComparison.OrdinalIgnoreCase))
                return "Windows Defender";

            if (scannerName.StartsWith("VirusTotal", StringComparison.OrdinalIgnoreCase))
                return "VirusTotal";

            return scannerName;
        }

        private async Task ScanFolderAsync()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            var files = EnumerateFilesRecursively(dialog.SelectedPath);
            await PerformScanAsync(files);
        }

        private static List<string> EnumerateFilesRecursively(string rootPath)
        {
            var files = new List<string>();
            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(rootPath);

            while (pendingDirectories.Count > 0)
            {
                var currentDirectory = pendingDirectories.Pop();

                try
                {
                    files.AddRange(Directory.EnumerateFiles(currentDirectory, "*", SearchOption.TopDirectoryOnly));
                }
                catch (UnauthorizedAccessException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Skipped inaccessible directory {currentDirectory}: {ex.Message}");
                }
                catch (IOException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Skipped unreadable directory {currentDirectory}: {ex.Message}");
                }

                try
                {
                    foreach (var subdirectory in Directory.EnumerateDirectories(currentDirectory, "*", SearchOption.TopDirectoryOnly))
                    {
                        try
                        {
                            if ((File.GetAttributes(subdirectory) & FileAttributes.ReparsePoint) == 0)
                                pendingDirectories.Push(subdirectory);
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Skipped inaccessible subdirectory {subdirectory}: {ex.Message}");
                        }
                        catch (IOException ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Skipped unreadable subdirectory {subdirectory}: {ex.Message}");
                        }
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Cannot enumerate subdirectories of {currentDirectory}: {ex.Message}");
                }
                catch (IOException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Cannot enumerate subdirectories of {currentDirectory}: {ex.Message}");
                }
            }

            files.Sort(StringComparer.OrdinalIgnoreCase);
            return files;
        }

        private async Task ScanCollectionAsync(ObservableCollection<MediaItem> mediaItems)
        {
            var files = mediaItems
                .Where(m => File.Exists(m.FilePath))
                .Select(m => m.FilePath)
                .ToList();

            await PerformScanAsync(files);
        }

        

        private async Task WaitIfPausedAsync(CancellationToken cancellationToken)
        {
            if (!IsPaused)
                return;

            _pauseCompletionSource ??= new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            await _pauseCompletionSource.Task.WaitAsync(cancellationToken);
        }

        private void ClearResults()
        {
            ScanResults.Clear();
            FilteredResults.Clear();
            StatusText = "Готово до сканування";
            CurrentProgress = 0;
            ScannedFiles = 0;
            TotalFiles = 0;
        }

        private void CancelScan()
        {
            _cancellationTokenSource?.Cancel();
            _pauseCompletionSource?.TrySetResult(true);
        }

        private void PauseScan()
        {
            if (!IsScanning || IsPaused)
                return;

            IsPaused = true;
            StatusText = $"Сканування призупинено. Перевірено {ScannedFiles} з {TotalFiles} файлів.";
        }

        private void ResumeScan()
        {
            if (!IsPaused)
                return;

            IsPaused = false;
            _pauseCompletionSource?.TrySetResult(true);
            _pauseCompletionSource = null;
            StatusText = $"Сканування продовжено. Перевірено {ScannedFiles} з {TotalFiles} файлів.";
        }

        private static bool CanReadFile(string filePath)
        {
            try
            {
                using var stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);

                if (stream.Length > 0)
                    stream.ReadByte();

                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private void OpenDetails(SecurityItem item)
        {
            var mediaItem = _mediaItems?.FirstOrDefault(
                media => string.Equals(media.FilePath, item.FilePath, StringComparison.OrdinalIgnoreCase))
                ?? new MediaItem
                {
                    FilePath = item.FilePath,
                    FileName = item.FileName,
                    Title = Path.GetFileNameWithoutExtension(item.FileName)
                };

            new DetailsWindow(mediaItem).ShowDialog();
        }

        private void ApplyFilter()
        {
            FilteredResults.Clear();

            var filtered = ScanResults.Where(item =>
                (ShowSafeFiles && item.Level == SecurityLevel.Safe) ||
                (ShowSuspiciousFiles && item.Level == SecurityLevel.Suspicious) ||
                (ShowDangerousFiles && item.Level == SecurityLevel.Dangerous) ||
                (ShowUnknownFiles && item.Level == SecurityLevel.Unknown)
            );

            var sorted = SelectedSortBy switch
            {
                "FileName" => filtered.OrderBy(x => x.FileName),
                "FileSize" => filtered.OrderByDescending(x => x.FileSize),
                "ThreatLevel" => filtered.OrderByDescending(x => (int)x.Level),
                "ScannedTime" => filtered.OrderByDescending(x => x.ScannedAt),
                _ => filtered.OrderBy(x => x.FileName)
            };

            foreach (var item in sorted)
            {
                FilteredResults.Add(item);
            }
        }
        private void AddToFilteredResults(SecurityItem item)
        {
            bool passesFilter =
                (ShowSafeFiles && item.Level == SecurityLevel.Safe) ||
                (ShowSuspiciousFiles && item.Level == SecurityLevel.Suspicious) ||
                (ShowDangerousFiles && item.Level == SecurityLevel.Dangerous) ||
                (ShowUnknownFiles && item.Level == SecurityLevel.Unknown);

            if (!passesFilter)
                return;

            FilteredResults.Add(item);
        }


        private async Task ExportReportAsync()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"SecurityReport_{DateTime.Now:yyyyMMdd_HHmmss}",
                DefaultExt = ".html",
                Filter = "HTML Files (*.html)|*.html|CSV Files (*.csv)|*.csv"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                if (dialog.FileName.EndsWith(".csv"))
                {
                    ExportAsCSV(dialog.FileName);
                }
                else
                {
                    ExportAsHTML(dialog.FileName);
                }

                MessageBox.Show($"Звіт експортовано: {dialog.FileName}", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при експорті: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportAsHTML(string filePath)
        {
            var results = ScanResults.ToList();
            var dangerousItems = results
                .Where(item => item.Level == SecurityLevel.Dangerous)
                .OrderBy(item => item.FileName)
                .ToList();
            var sourceCounts = results
                .GroupBy(item => GetScannerDisplayName(item.ScannerName))
                .OrderBy(group => group.Key)
                .ToList();

            var html = new System.Text.StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html lang='uk'>");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset='utf-8'/>");
            html.AppendLine("<title>MediaVault - Звіт безпеки</title>");
            html.AppendLine("<style>");
            html.AppendLine("body{font-family:Segoe UI,Arial,sans-serif;max-width:1100px;margin:36px auto;padding:0 24px;color:#20242a;background:#f4f6f8}");
            html.AppendLine("h1{margin-bottom:4px;color:#17212b}.muted{color:#667085}.cards{display:grid;grid-template-columns:repeat(4,1fr);gap:12px;margin:24px 0}");
            html.AppendLine(".card{background:#fff;border:1px solid #e1e6eb;border-radius:12px;padding:18px}.number{font-size:28px;font-weight:700;margin-top:8px}.danger{color:#c62828}.safe{color:#16803c}.section{margin-top:28px}");
            html.AppendLine("table{width:100%;border-collapse:collapse;background:#fff;margin-top:12px;border-radius:10px;overflow:hidden}th,td{border-bottom:1px solid #e6e9ed;padding:11px;text-align:left;vertical-align:top}th{background:#202a35;color:#fff}tr:last-child td{border-bottom:0}.empty{padding:18px;background:#fff;border-radius:10px}");
            html.AppendLine("@media print{body{background:#fff;margin:0;max-width:none}.card,table{break-inside:avoid}}");
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            html.AppendLine("<h1>🛡️ Звіт сканування безпеки MediaVault</h1>");
            html.AppendLine($"<p class='muted'>Створено: {DateTime.Now:dd.MM.yyyy HH:mm:ss}</p>");
            html.AppendLine("<div class='cards'>");
            AppendSummaryCard(html, "Перевірено файлів", results.Count, "");
            AppendSummaryCard(html, "Чисті", results.Count(item => item.Level == SecurityLevel.Safe), "safe");
            AppendSummaryCard(html, "Підозрілі", results.Count(item => item.Level == SecurityLevel.Suspicious), "");
            AppendSummaryCard(html, "Небезпечні", dangerousItems.Count, "danger");
            html.AppendLine("</div>");
            html.AppendLine($"<p>Невідомий статус: <strong>{results.Count(item => item.Level == SecurityLevel.Unknown)}</strong></p>");

            html.AppendLine("<section class='section'><h2>Джерела перевірки</h2><table><thead><tr><th>Антивірусний рушій</th><th>Файлів</th></tr></thead><tbody>");
            foreach (var source in sourceCounts)
            {
                html.AppendLine($"<tr><td>{System.Web.HttpUtility.HtmlEncode(source.Key)}</td><td>{source.Count()}</td></tr>");
            }
            html.AppendLine("</tbody></table></section>");

            html.AppendLine("<section class='section'><h2>Виявлені небезпечні файли</h2>");
            if (dangerousItems.Count == 0)
            {
                html.AppendLine("<p class='empty'>Небезпечних файлів не виявлено.</p>");
            }
            else
            {
                html.AppendLine("<table><thead><tr><th>Файл</th><th>Шлях</th><th>Загроза</th><th>Деталі виявлення</th><th>Джерело</th><th>Детекції</th></tr></thead><tbody>");
                foreach (var item in dangerousItems)
                {
                    html.AppendLine("<tr>");
                    html.AppendLine($"<td>{System.Web.HttpUtility.HtmlEncode(item.FileName)}</td>");
                    html.AppendLine($"<td>{System.Web.HttpUtility.HtmlEncode(item.FilePath)}</td>");
                    html.AppendLine($"<td>{System.Web.HttpUtility.HtmlEncode(item.ThreatName ?? "Загрозу виявлено")}</td>");
                    html.AppendLine($"<td>{System.Web.HttpUtility.HtmlEncode(item.ThreatDetails ?? "—")}</td>");
                    html.AppendLine($"<td>{System.Web.HttpUtility.HtmlEncode(GetScannerDisplayName(item.ScannerName))}</td>");
                    html.AppendLine($"<td>{System.Web.HttpUtility.HtmlEncode(item.DetectionRatio)}</td>");
                    html.AppendLine("</tr>");
                }
                html.AppendLine("</tbody></table>");
            }

            html.AppendLine("</section>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");

            File.WriteAllText(filePath, html.ToString());
        }

        private static void AppendSummaryCard(System.Text.StringBuilder html, string label, int value, string valueClass)
        {
            html.AppendLine($"<div class='card'><div class='muted'>{System.Web.HttpUtility.HtmlEncode(label)}</div><div class='number {valueClass}'>{value}</div></div>");
        }

        private void ExportAsCSV(string filePath)
        {
            var csv = new System.Text.StringBuilder();
            var results = ScanResults.ToList();
            var dangerousItems = results.Where(item => item.Level == SecurityLevel.Dangerous).ToList();

            csv.AppendLine("ЗВІТ СКАНУВАННЯ БЕЗПЕКИ");
            csv.AppendLine($"Дата,{CsvEscape(DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"))}");
            csv.AppendLine();
            csv.AppendLine("ПІДСУМОК");
            csv.AppendLine("Показник,Кількість");
            csv.AppendLine($"Перевірено файлів,{results.Count}");
            csv.AppendLine($"Чисті,{results.Count(item => item.Level == SecurityLevel.Safe)}");
            csv.AppendLine($"Підозрілі,{results.Count(item => item.Level == SecurityLevel.Suspicious)}");
            csv.AppendLine($"Небезпечні,{dangerousItems.Count}");
            csv.AppendLine($"Невідомий статус,{results.Count(item => item.Level == SecurityLevel.Unknown)}");
            csv.AppendLine();
            csv.AppendLine("ДЖЕРЕЛА ПЕРЕВІРКИ");
            csv.AppendLine("Антивірусний рушій,Кількість файлів");

            foreach (var source in results
                .GroupBy(item => GetScannerDisplayName(item.ScannerName))
                .OrderBy(group => group.Key))
            {
                csv.AppendLine($"{CsvEscape(source.Key)},{source.Count()}");
            }

            csv.AppendLine();
            csv.AppendLine("НЕБЕЗПЕЧНІ ФАЙЛИ");
            csv.AppendLine("Назва файлу,Шлях,Загроза,Деталі,Джерело,Детекції,SHA-256");
            foreach (var item in dangerousItems)
            {
                csv.AppendLine(string.Join(",", new[]
                {
                    CsvEscape(item.FileName),
                    CsvEscape(item.FilePath),
                    CsvEscape(item.ThreatName ?? "Загрозу виявлено"),
                    CsvEscape(item.ThreatDetails),
                    CsvEscape(GetScannerDisplayName(item.ScannerName)),
                    CsvEscape(item.DetectionRatio),
                    CsvEscape(item.Sha256Hash)
                }));
            }

            File.WriteAllText(filePath, csv.ToString());
        }

        private static string CsvEscape(string value)
        {
            return $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
        }

        private void OpenInExplorer(SecurityItem item)
        {
            try
            {
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select, \"{item.FilePath}\"",
                    UseShellExecute = true
                };

                System.Diagnostics.Process.Start(processInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не вдалося відкрити в каталозі: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async Task PerformScanAsync(List<string> files)
        {
            if (files.Count == 0)
            {
                MessageBox.Show("Немає файлів для сканування.", "Інформація", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            IsScanning = true;
            IsPaused = false;
            _pauseCompletionSource = null;
            _cancellationTokenSource = new CancellationTokenSource();
            TotalFiles = files.Count;
            ScannedFiles = 0;
            ScanResults.Clear();
            FilteredResults.Clear();

            bool vtAvailable = _virusTotalService.HasApiKey;
            int vtCached = _virusTotalService.GetCachedResultsCount();

            System.Diagnostics.Debug.WriteLine(
                $"[SecurityScanner] START: Files={files.Count}, VT available={vtAvailable}, VT cache={vtCached}");

            if (!vtAvailable)
            {
                MessageBox.Show(
                    "API-ключ VirusTotal не знайдено.\n\n" +
                    "Перевірте файл .env (VIRUSTOTAL_API_KEY) або введіть ключ у Налаштуваннях.\n\n" +
                    "Зараз буде використано тільки Windows Defender.",
                    "VirusTotal недоступний",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            try
            {
                // ===== ЕТАП 1: ХЕШУВАННЯ =====
                StatusText = $"Хешування {files.Count} файлів...";
                var hashedFiles = new List<HashedFile>();

                for (int i = 0; i < files.Count; i++)
                {
                    await WaitIfPausedAsync(_cancellationTokenSource.Token);
                    _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                    var filePath = files[i];
                    try
                    {
                        if (!File.Exists(filePath) || !CanReadFile(filePath))
                            continue;

                        var fileInfo = new FileInfo(filePath);
                        CurrentFileStatus = $"Хешування [{i + 1}/{files.Count}]: {fileInfo.Name}";
                        StatusText = $"Хешування [{i + 1}/{files.Count}]: {fileInfo.Name}";
                        IsCurrentFileProgressIndeterminate = true;
                        CurrentFileProgress = (int)((double)(i + 1) / files.Count * 100);

                        var sha256 = await FileHashService.ComputeSha256Async(
                            filePath, _cancellationTokenSource.Token, null);

                        hashedFiles.Add(new HashedFile
                        {
                            FilePath = filePath,
                            Sha256 = sha256,
                            Info = fileInfo
                        });
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Hash error {filePath}: {ex.Message}");
                    }
                }

                _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                if (hashedFiles.Count == 0)
                {
                    StatusText = "Немає файлів для сканування.";
                    return;
                }

                // ===== ЕТАП 2: DEFENDER ПАРАЛЕЛЬНО З ПРОГРЕСОМ =====
                StatusText = $"Defender: паралельне сканування {hashedFiles.Count} файлів...";
                IsCurrentFileProgressIndeterminate = true;

                var filePaths = hashedFiles.Select(h => h.FilePath).ToList();
                var hashedLookup = hashedFiles.ToDictionary(h => h.FilePath, h => h, StringComparer.OrdinalIgnoreCase);

                int defenderCompleted = 0;
                var defenderProgress = new Progress<VirusScanResult>(result =>
                {
                    if (!hashedLookup.TryGetValue(result.FilePath, out var hashedFile))
                        return;

                    ScanResults.Add(new SecurityItem
                    {
                        FilePath = hashedFile.FilePath,
                        FileName = hashedFile.Info.Name,
                        FileExtension = hashedFile.Info.Extension,
                        FileSize = hashedFile.Info.Length,
                        Sha256Hash = hashedFile.Sha256,
                        Level = result.Status,
                        ThreatName = result.ThreatName,
                        ThreatDetails = result.ThreatDetails,
                        MaliciousCount = result.MaliciousCount,
                        SuspiciousCount = result.SuspiciousCount,
                        TotalEngines = result.TotalEngines,
                        ScannedAt = DateTime.Now,
                        ScanDetails = result.Error,
                        ScannerName = result.ScannerName
                    });

                    defenderCompleted++;
                    ScannedFiles = defenderCompleted;

                    StatusText = $"Defender [{defenderCompleted}/{hashedFiles.Count}]: {hashedFile.Info.Name}";
                    CurrentFileStatus = $"Defender: {hashedFile.Info.Name}";

                    // Швидке додавання в FilteredResults (без повного пересортування)
                    bool passesFilter =
                        (ShowSafeFiles && result.Status == SecurityLevel.Safe) ||
                        (ShowSuspiciousFiles && result.Status == SecurityLevel.Suspicious) ||
                        (ShowDangerousFiles && result.Status == SecurityLevel.Dangerous) ||
                        (ShowUnknownFiles && result.Status == SecurityLevel.Unknown);

                    if (passesFilter)
                        FilteredResults.Add(ScanResults[ScanResults.Count - 1]);
                });

                var defenderResults = await _windowsDefenderService.ScanFilesBatchAsync(
                    filePaths, defenderProgress, _cancellationTokenSource.Token);

                _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                // Перевірка, що всі файли додані
                if (ScanResults.Count < hashedFiles.Count)
                {
                    var alreadyAdded = new HashSet<string>(ScanResults.Select(r => r.FilePath), StringComparer.OrdinalIgnoreCase);
                    foreach (var hashedFile in hashedFiles)
                    {
                        if (alreadyAdded.Contains(hashedFile.FilePath))
                            continue;

                        if (!defenderResults.TryGetValue(hashedFile.FilePath, out var defenderResult))
                        {
                            defenderResult = new VirusScanResult
                            {
                                FilePath = hashedFile.FilePath,
                                FileName = hashedFile.Info.Name,
                                Status = SecurityLevel.Unknown,
                                Error = "Defender не повернув результат",
                                ScannerName = "Windows Defender"
                            };
                        }

                        ScanResults.Add(new SecurityItem
                        {
                            FilePath = hashedFile.FilePath,
                            FileName = hashedFile.Info.Name,
                            FileExtension = hashedFile.Info.Extension,
                            FileSize = hashedFile.Info.Length,
                            Sha256Hash = hashedFile.Sha256,
                            Level = defenderResult.Status,
                            ThreatName = defenderResult.ThreatName,
                            ThreatDetails = defenderResult.ThreatDetails,
                            MaliciousCount = defenderResult.MaliciousCount,
                            SuspiciousCount = defenderResult.SuspiciousCount,
                            TotalEngines = defenderResult.TotalEngines,
                            ScannedAt = DateTime.Now,
                            ScanDetails = defenderResult.Error,
                            ScannerName = defenderResult.ScannerName
                        });
                        ScannedFiles++;
                    }
                }

                // Повне сортування після Defender
                ApplyFilter();

                // ===== ЕТАП 3: VIRUSTOTAL ДЛЯ SUSPICIOUS/DANGEROUS =====
                int vtUsedCount = 0;

                if (vtAvailable)
                {
                    var suspiciousItems = ScanResults
                        .Where(r => r.Level == SecurityLevel.Suspicious || r.Level == SecurityLevel.Dangerous)
                        .ToList();

                    System.Diagnostics.Debug.WriteLine(
                        $"[SecurityScanner] Files for VT: {suspiciousItems.Count} " +
                        $"(Dangerous={suspiciousItems.Count(x => x.Level == SecurityLevel.Dangerous)}, " +
                        $"Suspicious={suspiciousItems.Count(x => x.Level == SecurityLevel.Suspicious)})");

                    if (suspiciousItems.Count > 0)
                    {
                        int vtDone = 0;
                        foreach (var item in suspiciousItems)
                        {
                            await WaitIfPausedAsync(_cancellationTokenSource.Token);
                            _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                            vtDone++;
                            StatusText = $"VirusTotal [{vtDone}/{suspiciousItems.Count}]: {item.FileName}";
                            CurrentFileStatus = $"VT: {item.FileName}";
                            IsCurrentFileProgressIndeterminate = true;

                            try
                            {
                                var vtResult = await _virusTotalService.ScanFileHashAsync(
                                    item.FilePath, item.Sha256Hash, _cancellationTokenSource.Token);

                                vtUsedCount++;

                                System.Diagnostics.Debug.WriteLine(
                                    $"[VT] {item.FileName}: Status={vtResult.Status}, " +
                                    $"Error={vtResult.Error ?? "none"}, " +
                                    $"ShouldUseDefender={vtResult.ShouldUseWindowsDefender}, " +
                                    $"RateLimited={vtResult.IsRateLimited}");

                                if (!vtResult.ShouldUseWindowsDefender)
                                {
                                    item.Level = vtResult.Status;
                                    item.ThreatName = vtResult.ThreatName ?? item.ThreatName;
                                    item.ThreatDetails =
                                        $"Defender: {item.ThreatDetails} | VT: {vtResult.ThreatDetails}";
                                    item.MaliciousCount = vtResult.MaliciousCount;
                                    item.SuspiciousCount = vtResult.SuspiciousCount;
                                    item.TotalEngines = vtResult.TotalEngines;
                                    item.ScannerName = "VirusTotal + Defender";
                                }
                            }
                            catch (OperationCanceledException) { throw; }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine(
                                    $"[VT] Error for {item.FileName}: {ex.Message}");
                            }
                        }

                        ApplyFilter();
                    }
                }

                StatusText = $"Сканування завершено. {ScannedFiles} файлів " +
                             $"(Defender: {ScannedFiles}, VT: {vtUsedCount}).";

                System.Diagnostics.Debug.WriteLine(
                    $"[SecurityScanner] DONE: Defender={ScannedFiles}, VT={vtUsedCount}");
            }
            catch (OperationCanceledException)
            {
                StatusText = $"Сканування зупинено. Перевірено {ScannedFiles} з {TotalFiles} файлів.";
            }
            finally
            {
                IsPaused = false;
                IsScanning = false;
                _pauseCompletionSource?.TrySetResult(true);
                _pauseCompletionSource = null;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                ApplyFilter();
            }
        }

    }
}