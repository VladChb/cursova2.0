using System.Threading.Tasks;
using MediaVault.Models;

namespace MediaVault.Services
{
    public class MusicMetadataService
    {
        private readonly AcoustIdService _acoustId;
        private readonly ITunesMusicService _itunes;

        public MusicMetadataService()
        {
            _acoustId = new AcoustIdService();
            _itunes = new ITunesMusicService();
        }

        /// <summary>
        /// Шукає метадані: спочатку AcoustID (за звуком), потім iTunes (за назвою).
        /// </summary>
        public async Task<MusicMetadataResult> SearchAsync(string filePath, string title, string artist)
        {
            // Крок 1: AcoustID (найточніший метод, якщо є в базі)
            var acoustResult = await _acoustId.IdentifyAsync(filePath);

            if (acoustResult != null)
            {
                // Якщо AcoustID знайшов, використовуємо його дані для пошуку обкладинки в iTunes
                var itunesResult = await _itunes.SearchTrackAsync(acoustResult.Title, acoustResult.Artist);

                return new MusicMetadataResult
                {
                    Title = acoustResult.Title,
                    Artist = acoustResult.Artist,
                    Album = itunesResult?.Album,
                    Genre = itunesResult?.Genre,
                    Year = itunesResult?.Year,
                    CoverUrl = itunesResult?.CoverUrl,
                    TrackUrl = itunesResult?.TrackUrl
                };
            }

            // Крок 2: Якщо AcoustID не знайшов — fallback на iTunes (за назвою)
            var itunesFallback = await _itunes.SearchTrackAsync(title, artist);
            if (itunesFallback != null)
            {
                return new MusicMetadataResult
                {
                    Title = itunesFallback.Title,
                    Artist = itunesFallback.Artist,
                    Album = itunesFallback.Album,
                    Genre = itunesFallback.Genre,
                    Year = itunesFallback.Year,
                    CoverUrl = itunesFallback.CoverUrl,
                    TrackUrl = itunesFallback.TrackUrl
                };
            }

            return null;
        }
    }

    public class MusicMetadataResult
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