using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaVault.Models;

namespace MediaVault.Services
{
    public sealed class WindowsDefenderService
    {
        /// <summary>
        /// Сканує один файл через MpCmdRun.exe.
        /// Повертає точний результат на основі ExitCode:
        ///   0   = Safe
        ///   2   = Dangerous (загрозу знайдено)
        ///   інші = Unknown (помилка сканування)
        /// </summary>
        public async Task<VirusScanResult> ScanFileAsync(
            string filePath,
            string sha256Hash,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
            {
                return new VirusScanResult
                {
                    Sha256Hash = sha256Hash,
                    Status = SecurityLevel.Unknown,
                    FileName = Path.GetFileName(filePath),
                    FilePath = filePath,
                    Error = "Файл не знайдено",
                    ScannerName = "Windows Defender"
                };
            }

            var executablePath = FindMpCmdRun();
            if (executablePath == null)
            {
                return new VirusScanResult
                {
                    Sha256Hash = sha256Hash,
                    Status = SecurityLevel.Unknown,
                    FileName = Path.GetFileName(filePath),
                    FilePath = filePath,
                    Error = "Windows Defender MpCmdRun.exe не знайдено",
                    ScannerName = "Windows Defender"
                };
            }

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = $"-Scan -ScanType 3 -File {QuoteArgument(filePath)} -DisableRemediation",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };

            try
            {
                process.Start();
                using var registration = cancellationToken.Register(() =>
                {
                    try
                    {
                        if (!process.HasExited)
                            process.Kill(entireProcessTree: true);
                    }
                    catch (InvalidOperationException) { }
                });

                var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
                await process.WaitForExitAsync(cancellationToken);

                var output = await outputTask;
                var error = await errorTask;
                var details = string.IsNullOrWhiteSpace(error) ? output : $"{output}\n{error}";

                // ===== ТОЧНА ІНТЕРПРЕТАЦІЯ EXITCODE =====
                // MpCmdRun.exe:
                //   0 = clean
                //   2 = threat found
                //   інші = error
                var exitCode = process.ExitCode;
                SecurityLevel status;
                string threatName = null;
                string scanError = null;

                switch (exitCode)
                {
                    case 0:
                        status = SecurityLevel.Safe;
                        break;
                    case 2:
                        status = SecurityLevel.Dangerous;
                        threatName = ExtractThreatName(details) ?? "Загрозу виявлено";
                        break;
                    default:
                        status = SecurityLevel.Unknown;
                        scanError = $"MpCmdRun ExitCode={exitCode}";
                        break;
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[Defender Single] {Path.GetFileName(filePath)}: " +
                    $"ExitCode={exitCode}, Status={status}, Threat={threatName ?? "none"}");

                return new VirusScanResult
                {
                    Sha256Hash = sha256Hash,
                    FileName = Path.GetFileName(filePath),
                    FilePath = filePath,
                    Status = status,
                    ThreatName = threatName,
                    ThreatDetails = details.Trim(),
                    TotalEngines = 1,
                    MaliciousCount = status == SecurityLevel.Dangerous ? 1 : 0,
                    Error = scanError,
                    ScannedAt = DateTime.Now,
                    ScannerName = "Windows Defender"
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new VirusScanResult
                {
                    Sha256Hash = sha256Hash,
                    FileName = Path.GetFileName(filePath),
                    FilePath = filePath,
                    Status = SecurityLevel.Unknown,
                    Error = ex.Message,
                    ScannerName = "Windows Defender"
                };
            }
        }

        /// <summary>
        /// ПАРАЛЕЛЬНИЙ РЕЖИМ: запускає N MpCmdRun.exe одночасно.
        /// Кожен файл сканується окремо → точний результат.
        /// Прискорення в 4-8 разів проти послідовного (N = CPU count, max 8).
        /// </summary>
        public async Task<Dictionary<string, VirusScanResult>> ScanFilesBatchAsync(
    List<string> filePaths,
    IProgress<VirusScanResult> progress = null,
    CancellationToken cancellationToken = default)
        {
            var results = new Dictionary<string, VirusScanResult>(StringComparer.OrdinalIgnoreCase);
            var existingFiles = filePaths
                .Where(f => !string.IsNullOrWhiteSpace(f) && File.Exists(f))
                .ToList();

            // Неіснуючі — одразу Unknown + репорт
            foreach (var missing in filePaths.Except(existingFiles, StringComparer.OrdinalIgnoreCase))
            {
                var missingResult = new VirusScanResult
                {
                    FilePath = missing,
                    FileName = Path.GetFileName(missing),
                    Status = SecurityLevel.Unknown,
                    Error = "Файл не знайдено",
                    ScannerName = "Windows Defender"
                };
                results[missing] = missingResult;
                progress?.Report(missingResult);
            }

            if (existingFiles.Count == 0)
                return results;

            if (FindMpCmdRun() == null)
            {
                foreach (var f in existingFiles)
                {
                    var errResult = new VirusScanResult
                    {
                        FilePath = f,
                        FileName = Path.GetFileName(f),
                        Status = SecurityLevel.Unknown,
                        Error = "Windows Defender MpCmdRun.exe не знайдено",
                        ScannerName = "Windows Defender"
                    };
                    results[f] = errResult;
                    progress?.Report(errResult);
                }
                return results;
            }

            int maxParallel = Math.Min(Environment.ProcessorCount, 8);
            System.Diagnostics.Debug.WriteLine(
                $"[Defender Parallel] {existingFiles.Count} files, maxParallel={maxParallel}");

            using var semaphore = new SemaphoreSlim(maxParallel, maxParallel);
            int completed = 0;
            int total = existingFiles.Count;
            var resultsLock = new object();

            var tasks = existingFiles.Select(async file =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var result = await ScanFileAsync(file, null, cancellationToken);

                    // Додаємо в загальний словник під lock
                    lock (resultsLock)
                    {
                        results[file] = result;
                    }

                    // ===== РЕПОРТ ОДРАЗУ =====
                    progress?.Report(result);

                    int done = Interlocked.Increment(ref completed);
                    if (done % 25 == 0 || done == total)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[Defender Parallel] {done}/{total} done");
                    }

                    return result;
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToList();

            await Task.WhenAll(tasks);

            int safeCount = results.Values.Count(r => r.Status == SecurityLevel.Safe);
            int dangerousCount = results.Values.Count(r => r.Status == SecurityLevel.Dangerous);
            int unknownCount = results.Values.Count(r => r.Status == SecurityLevel.Unknown);

            System.Diagnostics.Debug.WriteLine(
                $"[Defender Parallel] DONE: Safe={safeCount}, Dangerous={dangerousCount}, Unknown={unknownCount}");

            return results;
        }

        private static string FindMpCmdRun()
        {
            var platformRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Microsoft", "Windows Defender", "Platform");

            if (!Directory.Exists(platformRoot))
                return null;

            return Directory.EnumerateDirectories(platformRoot)
                .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path => Path.Combine(path, "MpCmdRun.exe"))
                .FirstOrDefault(File.Exists);
        }

        private static string QuoteArgument(string value)
        {
            return $"\"{value.Replace("\\\"", "\\\\\"")}\"";
        }

        /// <summary>
        /// Витягує назву загрози з output MpCmdRun.exe.
        /// </summary>
        private static string ExtractThreatName(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
                return null;

            // Шукаємо "Threat: ..." або "Name: ..." або "Trojan:Win32/..."
            var patterns = new[]
            {
                @"Threat\s*:\s*(.+)",
                @"Name\s*:\s*(.+)",
                @"(Trojan:\w+/\S+)",
                @"(Virus:\w+/\S+)",
                @"(Worm:\w+/\S+)",
                @"(Backdoor:\w+/\S+)",
            };

            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(
                    output, pattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    var value = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(value) && value.Length < 200)
                        return value;
                }
            }

            return null;
        }
    }
}