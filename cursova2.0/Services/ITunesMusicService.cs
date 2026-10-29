using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using FuzzySharp;

namespace MediaVault.Services
{
    public class ITunesMusicService
    {
        private readonly HttpClient _httpClient;

        public ITunesMusicService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "MediaVault/1.0");
        }

        public async Task<ITunesResult> SearchTrackAsync(string title, string artist = null)
        {
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(artist))
                return null;

            try
            {
                string query = string.IsNullOrWhiteSpace(artist)
                    ? title.Trim()
                    : $"{artist.Trim()} {title?.Trim()}".Trim();

                string url = $"https://itunes.apple.com/search?term={HttpUtility.UrlEncode(query)}&entity=musicTrack&limit=15";

                var response = await _httpClient.GetStringAsync(url);
                using var doc = JsonDocument.Parse(response);
                var results = doc.RootElement.GetProperty("results");

                if (results.GetArrayLength() == 0)
                    return null;

                JsonElement? bestMatch = null;
                double bestScore = 0;
                bool exactTitleMatch = false;

                foreach (var item in results.EnumerateArray())
                {
                    string trackName = item.GetProperty("trackName").GetString() ?? "";
                    string artistName = item.GetProperty("artistName").GetString() ?? "";

                    // 1. Перевіряємо, чи є точний збіг назви (без урахування регістру)
                    if (trackName.Equals(title, StringComparison.OrdinalIgnoreCase))
                    {
                        exactTitleMatch = true;
                        bestMatch = item;
                        break;
                    }

                    // 2. Якщо точного збігу немає — рахуємо схожість
                    double titleScore = Fuzz.Ratio(title.ToLower(), trackName.ToLower());
                    double artistScore = string.IsNullOrWhiteSpace(artist)
                        ? 100
                        : Fuzz.Ratio(artist.ToLower(), artistName.ToLower());

                    double totalScore = (titleScore + artistScore) / 2.0;

                    if (totalScore > bestScore)
                    {
                        bestScore = totalScore;
                        bestMatch = item;
                    }
                }

                // Якщо є точний збіг — беремо його
                if (exactTitleMatch && bestMatch != null)
                {
                    return ExtractResult(bestMatch.Value);
                }

                // Якщо точного збігу немає, але схожість висока (>= 60) — беремо
                if (bestMatch != null && bestScore >= 60)
                {
                    return ExtractResult(bestMatch.Value);
                }

                // Інакше — не знайдено
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"iTunes: {ex.Message}");
                return null;
            }
        }

        private ITunesResult ExtractResult(JsonElement item)
        {
            string coverUrl = null;
            if (item.TryGetProperty("artworkUrl100", out var art))
                coverUrl = art.GetString()?.Replace("100x100", "500x500");

            int? year = null;
            if (item.TryGetProperty("releaseDate", out var dateProp) &&
                DateTime.TryParse(dateProp.GetString(), out DateTime dt))
                year = dt.Year;

            string trackUrl = null;
            if (item.TryGetProperty("trackViewUrl", out var trackUrlProp))
                trackUrl = trackUrlProp.GetString();

            return new ITunesResult
            {
                Title = item.GetProperty("trackName").GetString(),
                Artist = item.GetProperty("artistName").GetString(),
                Album = item.TryGetProperty("collectionName", out var alb) ? alb.GetString() : null,
                Genre = item.TryGetProperty("primaryGenreName", out var g) ? g.GetString() : null,
                Year = year,
                CoverUrl = coverUrl,
                TrackUrl = trackUrl
            };
        }
    }

    public class ITunesResult
    {
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public string Genre { get; set; }
        public int? Year { get; set; }
        public string CoverUrl { get; set; }
        public string TrackUrl { get; set; }
    }
}