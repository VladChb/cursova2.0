using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MediaVault.Services
{
    /// <summary>
    /// Шукає ISBN у PDF (сканує сирі байти — ISBN часто лежить як звичайний текст).
    /// </summary>
    public static class PdfIsbnExtractor
    {
        private static readonly Regex IsbnRegex = new(
            @"ISBN(?:-1[03])?(?:\s*(?:Book)?\s*)?(?::|\s)+([0-9Xx][0-9Xx\-\s]{8,18}[0-9Xx])",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex LooseIsbn = new(
            @"\b(?:97[89][-\s]?)?(?:\d[-\s]?){9}[\dXx]\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string ExtractIsbn(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return null;

                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                if (ext != ".pdf")
                    return null;

                var fi = new FileInfo(filePath);
                // Читаємо початок файлу (титулка / copyright зазвичай на початку)
                int toRead = (int)Math.Min(fi.Length, 2 * 1024 * 1024);
                byte[] buffer = new byte[toRead];
                using (var fs = File.OpenRead(filePath))
                {
                    int read = fs.Read(buffer, 0, toRead);
                    if (read <= 0) return null;
                    if (read < buffer.Length)
                        Array.Resize(ref buffer, read);
                }

                string text = Encoding.Latin1.GetString(buffer);
                string isbn = FindIsbn(text);
                if (!string.IsNullOrEmpty(isbn))
                    return isbn;

                // Спроба UTF-16 LE шматків у PDF
                text = Encoding.Unicode.GetString(buffer);
                return FindIsbn(text);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ISBN extract: {ex.Message}");
                return null;
            }
        }

        private static string FindIsbn(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            var m = IsbnRegex.Match(text);
            if (m.Success)
            {
                string normalized = NormalizeIsbn(m.Groups[1].Value);
                if (IsValidIsbn(normalized))
                    return normalized;
            }

            foreach (Match match in LooseIsbn.Matches(text))
            {
                string normalized = NormalizeIsbn(match.Value);
                if (IsValidIsbn(normalized))
                    return normalized;
            }

            return null;
        }

        public static string NormalizeIsbn(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            return Regex.Replace(raw, @"[-\s]", "").ToUpperInvariant();
        }

        public static bool IsValidIsbn(string isbn)
        {
            if (string.IsNullOrEmpty(isbn))
                return false;

            if (isbn.Length == 10)
                return ValidateIsbn10(isbn);
            if (isbn.Length == 13 && (isbn.StartsWith("978") || isbn.StartsWith("979")))
                return ValidateIsbn13(isbn);

            return false;
        }

        private static bool ValidateIsbn10(string isbn)
        {
            int sum = 0;
            for (int i = 0; i < 9; i++)
            {
                if (!char.IsDigit(isbn[i])) return false;
                sum += (isbn[i] - '0') * (10 - i);
            }

            char check = isbn[9];
            int checkVal = check == 'X' ? 10 : (char.IsDigit(check) ? check - '0' : -1);
            if (checkVal < 0) return false;
            return (sum + checkVal) % 11 == 0;
        }

        private static bool ValidateIsbn13(string isbn)
        {
            if (!isbn.All(char.IsDigit)) return false;
            int sum = 0;
            for (int i = 0; i < 12; i++)
                sum += (isbn[i] - '0') * (i % 2 == 0 ? 1 : 3);
            int check = (10 - (sum % 10)) % 10;
            return check == (isbn[12] - '0');
        }
    }
}
