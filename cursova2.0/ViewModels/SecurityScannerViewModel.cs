using System;
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

        private static System.Collections.Generic.List<string> EnumerateFilesRecursively(string rootPath)
        {
            var files = new System.Collections.Generic.List<string>();
            var pendingDirectories = new System.Collections.Generic.Stack<string>();
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
                            // Avoid junction/symlink loops while traversing the directory tree.
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
                .Where(m => System.IO.File.Exists(m.FilePath))
                .Select(m => m.FilePath)
                .ToList();

            await PerformScanAsync(files);
        }

        private async Task PerformScanAsync(System.Collections.Generic.List<string> files)
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

            try
            {
                StatusText = $"Сканування {files.Count} файлів...";

                foreach (var filePath in files)
                {
                    await WaitIfPausedAsync(_cancellationTokenSource.Token);
                    _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                    try
                    {
                        if (!System.IO.File.Exists(filePath))
                            continue;

                        if (!CanReadFile(filePath))
                        {
                            System.Diagnostics.Debug.WriteLine($"Skipped inaccessible file: {filePath}");
                            var unavailableFileInfo = new FileInfo(filePath);
                            ScanResults.Add(new SecurityItem
                            {
                                FilePath = filePath,
                                FileName = unavailableFileInfo.Name,
                                FileExtension = unavailableFileInfo.Extension,
                                FileSize = unavailableFileInfo.Exists ? unavailableFileInfo.Length : 0,
                                Level = SecurityLevel.Unknown,
                                ScannedAt = DateTime.Now,
                                ScanDetails = "Файл недоступний для читання. Перевірте, що OneDrive запущено та файл збережено локально."
                            });
                            ApplyFilter();
                            ScannedFiles++;
                            StatusText = $"Пропущено недоступний файл: {unavailableFileInfo.Name}";
                            continue;
                        }

                        var fileInfo = new FileInfo(filePath);
                        CurrentFileProgress = 0;
                        IsCurrentFileProgressIndeterminate = false;
                        CurrentFileStatus = $"Хешування {fileInfo.Name} (0%)";

                        var lastReportedPercent = -1;
                        var hashProgress = new Progress<long>(bytesRead =>
                        {
                            var percent = fileInfo.Length > 0
                                ? Math.Min(100, (int)(bytesRead * 100.0 / fileInfo.Length))
                                : 100;

                            if (percent != lastReportedPercent)
                            {
                                lastReportedPercent = percent;
                                CurrentFileProgress = percent;
                                CurrentFileStatus = $"Хешування {fileInfo.Name} ({percent}%)";
                            }
                        });

                        var sha256 = await FileHashService.ComputeSha256Async(
                            filePath,
                            _cancellationTokenSource.Token,
                            hashProgress);

                        _cancellationTokenSource.Token.ThrowIfCancellationRequested();

                        VirusScanResult scanResult;
                        if (_virusTotalService.HasApiKey)
                        {
                            IsCurrentFileProgressIndeterminate = true;
                            CurrentFileStatus = $"Перевірка VirusTotal: {fileInfo.Name}";
                            scanResult = await _virusTotalService.ScanFileHashAsync(
                                filePath,
                                sha256,
                                _cancellationTokenSource.Token);

                            if (scanResult.ShouldUseWindowsDefender)
                            {
                                var fallbackReason = scanResult.IsRateLimited
                                    ? "Ліміт VirusTotal вичерпано"
                                    : "VirusTotal не надав результат";
                                StatusText = $"{fallbackReason}; запускається Windows Defender.";
                                CurrentFileStatus = $"Перевірка Windows Defender: {fileInfo.Name}";

                                var virusTotalError = scanResult.Error;
                                scanResult = await _windowsDefenderService.ScanFileAsync(
                                    filePath,
                                    sha256,
                                    _cancellationTokenSource.Token);
                                scanResult.ScannerName = "Windows Defender (fallback)";
                                if (!string.IsNullOrWhiteSpace(virusTotalError))
                                {
                                    scanResult.Error = $"{virusTotalError}; локально перевірено Windows Defender.";
                                }
                            }
                        }
                        else
                        {
                            IsCurrentFileProgressIndeterminate = true;
                            CurrentFileStatus = $"Перевірка Windows Defender: {fileInfo.Name}";
                            scanResult = await _windowsDefenderService.ScanFileAsync(
                                filePath,
                                sha256,
                                _cancellationTokenSource.Token);
                            scanResult.ScannerName = "Windows Defender";
                        }

                        var item = new SecurityItem
                        {
                            FilePath = filePath,
                            FileName = fileInfo.Name,
                            FileExtension = fileInfo.Extension,
                            FileSize = fileInfo.Length,
                            Sha256Hash = sha256,
                            Level = scanResult.Status,
                            ThreatName = scanResult.ThreatName,
                            ThreatDetails = scanResult.ThreatDetails,
                            MaliciousCount = scanResult.MaliciousCount,
                            SuspiciousCount = scanResult.SuspiciousCount,
                            TotalEngines = scanResult.TotalEngines,
                            ScannedAt = DateTime.Now,
                            ScanDetails = scanResult.Error,
                            ScannerName = scanResult.ScannerName
                        };

                        ScanResults.Add(item);
                        ApplyFilter();

                        ScannedFiles++;
                        CurrentFileProgress = 100;
                        IsCurrentFileProgressIndeterminate = false;
                        CurrentFileStatus = $"Завершено: {fileInfo.Name}";
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error scanning file {filePath}: {ex.Message}");
                        var failedFileInfo = new FileInfo(filePath);
                        ScanResults.Add(new SecurityItem
                        {
                            FilePath = filePath,
                            FileName = failedFileInfo.Name,
                            FileExtension = failedFileInfo.Extension,
                            FileSize = failedFileInfo.Exists ? failedFileInfo.Length : 0,
                            Level = SecurityLevel.Unknown,
                            ScannedAt = DateTime.Now,
                            ScanDetails = ex.Message
                        });
                        ApplyFilter();
                        ScannedFiles++;
                        CurrentFileProgress = 0;
                        IsCurrentFileProgressIndeterminate = false;
                        CurrentFileStatus = $"Пропущено: {failedFileInfo.Name} — {ex.Message}";
                    }

                    _cancellationTokenSource.Token.ThrowIfCancellationRequested();
                }

                StatusText = $"Сканування завершено. Перевірено {ScannedFiles} файлів.";
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

                // Opening a OneDrive placeholder can succeed even when its contents
                // are unavailable. Read to force provider hydration before scanning.
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

            // Apply sorting
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

            System.IO.File.WriteAllText(filePath, html.ToString());
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

            System.IO.File.WriteAllText(filePath, csv.ToString());
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
    }
}
