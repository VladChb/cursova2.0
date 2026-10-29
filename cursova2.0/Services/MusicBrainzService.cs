using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace MediaVault.Services
{
    public class MusicBrainzService
    {
        private static readonly HttpClient _http = new HttpClient();

        static MusicBrainzService()
        {
            _http.DefaultRequestHeaders.Add("User-Agent",
                "MediaVault/1.0 (https://github.com/your-username/MediaVault)");
            _http.Timeout = TimeSpan.FromSeconds(10);
        }

        public async Task<MusicBrainzResult> SearchTrackAsync(string title, string artist = null)
        {
            if (string.IsNullOrWhiteSpace(title))
                return null;

            try
            {
                // MusicBrainz підтримує Lucene-синтаксис
                string query = string.IsNullOrWhiteSpace(artist)
                    ? $"recording:\"{title}\""
                    : $"recording:\"{title}\" AND artist:\"{artist}\"";

                string url = $"https://musicbrainz.org/ws/2/recording/?query={HttpUtility.UrlEncode(query)}&fmt=json&limit=5";

                var response = await _http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(response);
                var recordings = doc.RootElement.GetProperty("recordings");

                if (recordings.GetArrayLength() == 0)
                    return null;

                var first = recordings[0];

                string recordingTitle = first.GetProperty("title").GetString();
                string artistName = null;

                if (first.TryGetProperty("artist-credit", out var credit) && credit.GetArrayLength() > 0)
                {
                    artistName = credit[0].TryGetProperty("name", out var n) ? n.GetString() : null;
                }

                return new MusicBrainzResult
                {
                    Title = recordingTitle,
                    Artist = artistName
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MusicBrainz: {ex.Message}");
                return null;
            }
        }
    }

    public class MusicBrainzResult
    {
        public string Title { get; set; }
        public string Artist { get; set; }
    }
}