using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace MediaVault.Services
{
    public class AcoustIdService
    {
        private static readonly HttpClient _http = new HttpClient();
        private readonly string _apiKey;

        public AcoustIdService()
        {
            _apiKey = EnvService.Get("ACOUSTID_API_KEY");
        }

        public async Task<AcoustIdResult> IdentifyAsync(string filePath)
        {
            if (string.IsNullOrEmpty(_apiKey))
                return null;

            try
            {
                // 1. Запускаємо fpcalc.exe
                string fpcalcPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fpcalc.exe");
                if (!System.IO.File.Exists(fpcalcPath))
                    return null;

                var startInfo = new ProcessStartInfo
                {
                    FileName = fpcalcPath,
                    Arguments = $"-json \"{filePath}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                string output = await process.StandardOutput.ReadToEndAsync();
                process.WaitForExit();

                // 2. Парсимо JSON від fpcalc
                using var doc = JsonDocument.Parse(output);
                var root = doc.RootElement;

                if (!root.TryGetProperty("fingerprint", out var fpProp) ||
                    !root.TryGetProperty("duration", out var durProp))
                    return null;

                string fingerprint = fpProp.GetString();
                double duration = durProp.GetDouble();

                // 3. Запит до AcoustID API
                string url = $"https://api.acoustid.org/v2/lookup?client={_apiKey}&meta=recordings&duration={duration}&fingerprint={fingerprint}";

                var response = await _http.GetStringAsync(url);
                using var apiDoc = JsonDocument.Parse(response);
                var apiRoot = apiDoc.RootElement;

                if (apiRoot.GetProperty("status").GetString() != "ok")
                    return null;

                var results = apiRoot.GetProperty("results");
                if (results.GetArrayLength() == 0)
                    return null;

                var first = results[0];
                if (!first.TryGetProperty("recordings", out var recordings) || recordings.GetArrayLength() == 0)
                    return null;

                var recording = recordings[0];
                string title = recording.GetProperty("title").GetString();
                string artist = null;

                if (recording.TryGetProperty("artists", out var artists) && artists.GetArrayLength() > 0)
                {
                    artist = artists[0].GetProperty("name").GetString();
                }

                return new AcoustIdResult { Title = title, Artist = artist };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AcoustID помилка: {ex.Message}");
                return null;
            }
        }
    }

    public class AcoustIdResult
    {
        public string Title { get; set; }
        public string Artist { get; set; }
    }
}