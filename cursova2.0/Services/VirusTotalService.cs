using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MediaVault.Models;

namespace MediaVault.Services
{
    public class VirusTotalService
    {
        private const string BASE_URL = "https://www.virustotal.com/api/v3";
        private const string CACHE_FILENAME = "virustotal_cache.json";

        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _cacheFilePath;
        private Dictionary<string, VirusScanResult> _cache;

        public bool HasApiKey => !string.IsNullOrWhiteSpace(_apiKey);

        public VirusTotalService(string apiKey = null)
        {
            // Спробуємо прочитати з параметра, якщо нема - з .env
            _apiKey = (string.IsNullOrWhiteSpace(apiKey)
                ? EnvService.Get("VIRUSTOTAL_API_KEY", "")
                : apiKey)?.Trim();

            _httpClient = new HttpClient();
            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("x-apikey", _apiKey);
            }

            string cacheFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MediaVault");

            if (!Directory.Exists(cacheFolder))
                Directory.CreateDirectory(cacheFolder);

            _cacheFilePath = Path.Combine(cacheFolder, CACHE_FILENAME);
            _cache = LoadCache();
        }

        public async Task<VirusScanResult> ScanFileHashAsync(
            string filePath,
            string sha256Hash,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sha256Hash))
                return new VirusScanResult { Status = SecurityLevel.Unknown, FileName = Path.GetFileName(filePath) };

            // Check cache first
            if (_cache.TryGetValue(sha256Hash.ToLowerInvariant(), out var cachedResult) &&
                cachedResult.Status != SecurityLevel.Unknown)
            {
                cachedResult.ScannerName ??= "VirusTotal";
                return cachedResult;
            }

            try
            {
                var result = await GetFileReportAsync(sha256Hash, cancellationToken);
                result.FileName = Path.GetFileName(filePath);
                result.FilePath = filePath;

                if (!result.ShouldUseWindowsDefender)
                {
                    _cache[sha256Hash.ToLowerInvariant()] = result;
                    SaveCache();
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new VirusScanResult
                {
                    Status = SecurityLevel.Unknown,
                    FileName = Path.GetFileName(filePath),
                    FilePath = filePath,
                    Error = ex.Message,
                    ShouldUseWindowsDefender = true
                };
            }
        }

        private async Task<VirusScanResult> GetFileReportAsync(string sha256Hash, CancellationToken cancellationToken)
        {
            try
            {
                string url = $"{BASE_URL}/files/{sha256Hash}";
                using var response = await _httpClient.GetAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return new VirusScanResult
                        {
                            Status = SecurityLevel.Unknown,
                            Error = "Звіту VirusTotal немає; потрібна локальна перевірка.",
                            ShouldUseWindowsDefender = true
                        };
                    }

                    return new VirusScanResult
                    {
                        Status = SecurityLevel.Unknown,
                        Error = $"VirusTotal API: {(int)response.StatusCode} {response.ReasonPhrase}",
                        ShouldUseWindowsDefender = true,
                        IsRateLimited = response.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                    };
                }

                string content = await response.Content.ReadAsStringAsync(cancellationToken);
                var jsonDoc = JsonDocument.Parse(content);
                var data = jsonDoc.RootElement.GetProperty("data");
                var attributes = data.GetProperty("attributes");

                var result = new VirusScanResult
                {
                    Sha256Hash = sha256Hash
                };

                // Parse stats
                if (attributes.TryGetProperty("last_analysis_stats", out var stats))
                {
                    result.TotalEngines = stats.EnumerateObject()
                        .Sum(stat => stat.Value.GetInt32());

                    result.MaliciousCount = stats.TryGetProperty("malicious", out var mal) ? mal.GetInt32() : 0;
                    result.SuspiciousCount = stats.TryGetProperty("suspicious", out var sus) ? sus.GetInt32() : 0;

                    if (result.TotalEngines == 0)
                    {
                        result.Error = "VirusTotal звіт ще не містить результатів аналізу.";
                        result.ShouldUseWindowsDefender = true;
                        return result;
                    }
                }
                else
                {
                    result.Error = "VirusTotal звіт не містить результатів аналізу.";
                    result.ShouldUseWindowsDefender = true;
                    return result;
                }

                // Determine status
                if (result.MaliciousCount > 0)
                {
                    result.Status = SecurityLevel.Dangerous;
                    result.ThreatName = "Multiple threats detected";
                }
                else if (result.SuspiciousCount > 0)
                {
                    result.Status = SecurityLevel.Suspicious;
                    result.ThreatName = "Suspicious activity detected";
                }
                else
                {
                    result.Status = SecurityLevel.Safe;
                }

                // Get threat names
                if (attributes.TryGetProperty("last_analysis_results", out var results))
                {
                    var threats = new List<string>();
                    foreach (var engine in results.EnumerateObject())
                    {
                        if (engine.Value.TryGetProperty("category", out var category))
                        {
                            var cat = category.GetString();
                            if (cat != "undetected")
                            {
                                if (engine.Value.TryGetProperty("engine_name", out var engineName) &&
                                    engine.Value.TryGetProperty("result", out var threatName))
                                {
                                    threats.Add($"{engineName.GetString()}: {threatName.GetString()}");
                                }
                            }
                        }
                    }

                    if (threats.Count > 0)
                        result.ThreatDetails = string.Join("; ", threats.Take(3)); // Show first 3
                }

                result.ScannedAt = DateTime.Now;
                result.ScannerName = "VirusTotal";
                return result;
            }
            catch (HttpRequestException ex)
            {
                return new VirusScanResult
                {
                    Status = SecurityLevel.Unknown,
                    Error = $"Network error: {ex.Message}",
                    ShouldUseWindowsDefender = true
                };
            }
            catch (JsonException ex)
            {
                return new VirusScanResult
                {
                    Status = SecurityLevel.Unknown,
                    Error = $"Failed to parse API response: {ex.Message}",
                    ShouldUseWindowsDefender = true
                };
            }
        }

        private Dictionary<string, VirusScanResult> LoadCache()
        {
            try
            {
                if (File.Exists(_cacheFilePath))
                {
                    string json = File.ReadAllText(_cacheFilePath);
                    var list = JsonSerializer.Deserialize<List<VirusScanResult>>(json) ?? new List<VirusScanResult>();
                    return list
                        .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Sha256Hash))
                        .ToDictionary(x => x.Sha256Hash.ToLowerInvariant(), x => x);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading VirusTotal cache: {ex.Message}");
            }

            return new Dictionary<string, VirusScanResult>();
        }

        private void SaveCache()
        {
            try
            {
                var list = _cache.Values.ToList();
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(list, options);
                File.WriteAllText(_cacheFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving VirusTotal cache: {ex.Message}");
            }
        }

        public void ClearCache()
        {
            _cache.Clear();
            if (File.Exists(_cacheFilePath))
                File.Delete(_cacheFilePath);
        }

        public int GetCachedResultsCount()
        {
            return _cache.Count;
        }
    }

    public class VirusScanResult
    {
        [JsonPropertyName("sha256Hash")]
        public string Sha256Hash { get; set; }

        [JsonPropertyName("fileName")]
        public string FileName { get; set; }

        [JsonPropertyName("filePath")]
        public string FilePath { get; set; }

        [JsonPropertyName("status")]
        public SecurityLevel Status { get; set; } = SecurityLevel.Unknown;

        [JsonPropertyName("threatName")]
        public string ThreatName { get; set; }

        [JsonPropertyName("threatDetails")]
        public string ThreatDetails { get; set; }

        [JsonPropertyName("maliciousCount")]
        public int MaliciousCount { get; set; }

        [JsonPropertyName("suspiciousCount")]
        public int SuspiciousCount { get; set; }

        [JsonPropertyName("totalEngines")]
        public int TotalEngines { get; set; }

        [JsonPropertyName("scannedAt")]
        public DateTime ScannedAt { get; set; }

        [JsonPropertyName("error")]
        public string Error { get; set; }

        [JsonIgnore]
        public bool ShouldUseWindowsDefender { get; set; }

        [JsonIgnore]
        public bool IsRateLimited { get; set; }

        [JsonPropertyName("scannerName")]
        public string ScannerName { get; set; }

        public string DetectionRatio => TotalEngines > 0
            ? $"{MaliciousCount + SuspiciousCount}/{TotalEngines}"
            : "N/A";
    }
}
