using System;
using System.Threading.Tasks;
using TMDbLib.Client;
using TMDbLib.Objects.Search;

namespace MediaVault.Services
{
    public class TmdbService
    {
        private readonly TMDbClient _client;

        public TmdbService(string apiKey)
        {
            _client = new TMDbClient(apiKey);
        }

        public async Task<TmdbResult> SearchMovieAsync(string title, int? year = null)
        {
            try
            {
                var searchResult = await _client.SearchMovieAsync(title);

                if (searchResult.Results.Count == 0)
                    return null;

                var movie = searchResult.Results[0];

                if (year.HasValue && movie.ReleaseDate.HasValue)
                {
                    foreach (var result in searchResult.Results)
                    {
                        if (result.ReleaseDate.HasValue &&
                            Math.Abs(result.ReleaseDate.Value.Year - year.Value) <= 2)
                        {
                            movie = result;
                            break;
                        }
                    }
                }

                var fullMovie = await _client.GetMovieAsync(movie.Id);

                return new TmdbResult
                {
                    TmdbId = fullMovie.Id,
                    Title = fullMovie.Title,
                    OriginalTitle = fullMovie.OriginalTitle,
                    Overview = fullMovie.Overview,
                    PosterPath = fullMovie.PosterPath,
                    ReleaseDate = fullMovie.ReleaseDate,
                    VoteAverage = fullMovie.VoteAverage,
                    Genres = fullMovie.Genres
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TMDb помилка: {ex.Message}");
                return null;
            }
        }

        public string GetPosterUrl(string posterPath, string size = "w500")
        {
            if (string.IsNullOrEmpty(posterPath))
                return null;

            return $"https://image.tmdb.org/t/p/{size}{posterPath}";
        }
    }

    public class TmdbResult
    {
        public int TmdbId { get; set; }
        public string Title { get; set; }
        public string OriginalTitle { get; set; }
        public string Overview { get; set; }
        public string PosterPath { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public double VoteAverage { get; set; }
        public System.Collections.Generic.List<TMDbLib.Objects.General.Genre> Genres { get; set; }
    }
}