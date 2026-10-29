using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace MediaVault.Services
{
    public class OpenLibraryService
    {
        private static readonly HttpClient _http = new()
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        static OpenLibraryService()
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("MediaVault/2.0 (cursover project)");
        }

        public async Task<BookSearchResult> SearchByIsbnAsync(string isbn)
        {
            isbn = PdfIsbnExtractor.NormalizeIsbn(isbn);
            if (!PdfIsbnExtractor.IsValidIsbn(isbn))
                return null;

            try
            {
                string url = $"https://openlibrary.org/api/books?bibkeys=ISBN:{isbn}&format=json&jscmd=data";
                string json = await _http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);

                string key = $"ISBN:{isbn}";
                if (!doc.RootElement.TryGetProperty(key, out var book))
                    return null;

                string title = book.TryGetProperty("title", out var t) ? t.GetString() : null;
                string author = null;
                if (book.TryGetProperty("authors", out var authors) && authors.GetArrayLength() > 0)
                {
                    var a0 = authors[0];
                    if (a0.TryGetProperty("name", out var an))
                        author = an.GetString();
                }

                int? year = null;
                if (book.TryGetProperty("publish_date", out var pd))
                {
                    string date = pd.GetString();
                    if (!string.IsNullOrEmpty(date))
                    {
                        var m = System.Text.RegularExpressions.Regex.Match(date, @"\b(19|20)\d{2}\b");
                        if (m.Success && int.TryParse(m.Value, out int y))
                            year = y;
                    }
                }

                string posterUrl = null;
                if (book.TryGetProperty("cover", out var cover))
                {
                    if (cover.TryGetProperty("large", out var large))
                        posterUrl = large.GetString();
                    else if (cover.TryGetProperty("medium", out var med))
                        posterUrl = med.GetString();
                }

                if (string.IsNullOrEmpty(posterUrl))
                    posterUrl = $"https://covers.openlibrary.org/b/isbn/{isbn}-L.jpg";

                string description = null;
                if (book.TryGetProperty("excerpts", out var excerpts) && excerpts.GetArrayLength() > 0 &&
                    excerpts[0].TryGetProperty("text", out var exText))
                    description = Truncate(exText.GetString(), 2000);

                if (string.IsNullOrEmpty(description) && book.TryGetProperty("notes", out var notes))
                {
                    description = notes.ValueKind == JsonValueKind.String
                        ? Truncate(notes.GetString(), 2000)
                        : notes.TryGetProperty("value", out var nv) ? Truncate(nv.GetString(), 2000) : null;
                }

                return new BookSearchResult
                {
                    Title = title,
                    Author = author,
                    Year = year,
                    Description = description,
                    PosterUrl = posterUrl,
                    Isbn = isbn,
                    OpenLibraryKey = book.TryGetProperty("key", out var k) ? k.GetString() : null
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Open Library ISBN: {ex.Message}");
                return null;
            }
        }

        public async Task<BookSearchResult> SearchBookAsync(string title, string author = null)
        {
            if (string.IsNullOrWhiteSpace(title))
                return null;

            try
            {
                string query = Uri.EscapeDataString(title.Trim());
                string url = $"https://openlibrary.org/search.json?title={query}&limit=5";

                if (!string.IsNullOrWhiteSpace(author))
                    url += $"&author={Uri.EscapeDataString(author.Trim())}";

                string json = await _http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("docs", out var docs) || docs.GetArrayLength() == 0)
                    return null;

                var first = docs[0];
                string bookTitle = first.TryGetProperty("title", out var t) ? t.GetString() : title;

                string bookAuthor = null;
                if (first.TryGetProperty("author_name", out var authors) && authors.GetArrayLength() > 0)
                    bookAuthor = authors[0].GetString();

                int? year = null;
                if (first.TryGetProperty("first_publish_year", out var y) && y.ValueKind == JsonValueKind.Number)
                    year = y.GetInt32();

                string posterUrl = null;
                if (first.TryGetProperty("cover_i", out var coverId) && coverId.ValueKind == JsonValueKind.Number)
                    posterUrl = $"https://covers.openlibrary.org/b/id/{coverId.GetInt32()}-L.jpg";

                string isbn = null;
                if (first.TryGetProperty("isbn", out var isbns) && isbns.GetArrayLength() > 0)
                    isbn = PdfIsbnExtractor.NormalizeIsbn(isbns[0].GetString());

                string description = null;
                if (first.TryGetProperty("key", out var workKey))
                {
                    string key = workKey.GetString();
                    if (!string.IsNullOrEmpty(key))
                        description = await FetchWorkDescriptionAsync(key);
                }

                if (string.IsNullOrEmpty(description) &&
                    first.TryGetProperty("first_sentence", out var sentence))
                {
                    description = sentence.ValueKind == JsonValueKind.Array && sentence.GetArrayLength() > 0
                        ? sentence[0].GetString()
                        : sentence.GetString();
                }

                return new BookSearchResult
                {
                    Title = bookTitle,
                    Author = bookAuthor,
                    Year = year,
                    Description = description,
                    PosterUrl = posterUrl,
                    Isbn = isbn,
                    OpenLibraryKey = first.TryGetProperty("key", out var k) ? k.GetString() : null
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Open Library помилка: {ex.Message}");
                return null;
            }
        }

        private async Task<string> FetchWorkDescriptionAsync(string workKey)
        {
            try
            {
                string url = $"https://openlibrary.org{workKey}.json";
                string json = await _http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("description", out var desc))
                    return null;

                if (desc.ValueKind == JsonValueKind.String)
                    return Truncate(desc.GetString(), 2000);

                if (desc.ValueKind == JsonValueKind.Object &&
                    desc.TryGetProperty("value", out var value))
                    return Truncate(value.GetString(), 2000);

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = text.Trim();
            return text.Length <= max ? text : text[..max] + "…";
        }
    }

    public class BookSearchResult
    {
        public string Title { get; set; }
        public string Author { get; set; }
        public int? Year { get; set; }
        public string Description { get; set; }
        public string PosterUrl { get; set; }
        public string Isbn { get; set; }
        public string OpenLibraryKey { get; set; }
    }
}
