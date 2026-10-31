using System;
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
        public async Task<VirusScanResult> ScanFileAsync(
            string filePath,
            string sha256Hash,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
            {
                return new VirusScanResult
                {
                    Status = SecurityLevel.Unknown,
                    FileName = Path.GetFileName(filePath),
                    FilePath = filePath,
                    Error = "Файл не знайдено"
                };
            }

            var executablePath = FindMpCmdRun();
            if (executablePath == null)
            {
                return new VirusScanResult
                {
                    Status = SecurityLevel.Unknown,
                    FileName = Path.GetFileName(filePath),
                    FilePath = filePath,
                    Error = "Windows Defender MpCmdRun.exe не знайдено"
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
                    catch (InvalidOperationException)
                    {
                    }
                });

                var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
                await process.WaitForExitAsync(cancellationToken);

                var output = await outputTask;
                var error = await errorTask;
                var details = string.IsNullOrWhiteSpace(error) ? output : $"{output}\n{error}";
                var scanSucceeded = process.ExitCode == 0;

                return new VirusScanResult
                {
                    Sha256Hash = sha256Hash,
                    FileName = Path.GetFileName(filePath),
                    FilePath = filePath,
                    Status = scanSucceeded ? SecurityLevel.Safe : SecurityLevel.Unknown,
                    ThreatName = null,
                    ThreatDetails = details.Trim(),
                    TotalEngines = 1,
                    MaliciousCount = 0,
                    Error = scanSucceeded ? null : $"Windows Defender завершився з кодом {process.ExitCode}; результат сканування не підтверджено.",
                    ScannedAt = DateTime.Now
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
                    Error = ex.Message
                };
            }
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
    }
}
