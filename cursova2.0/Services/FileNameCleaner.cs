using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MediaVault.Services
{
    public class FileNameCleaner
    {
        private static readonly string[] JunkPatterns =
        {
            @"\[.*?\]",
            @"\(.*?\)",
            @"\{.*?\}",
            @"\b\d{3,4}p\b",
            @"\b\d{3,4}x\d{3,4}\b",
            @"\bWEB[.\-]?DL\b",
            @"\bWEB[.\-]?Rip\b",
            @"\bBlu[.\-]?Ray\b",
            @"\bBDRip\b",
            @"\bBRRip\b",
            @"\bHDRip\b",
            @"\bDVDRip\b",
            @"\bHDTV\b",
            @"\bPDTV\b",
            @"\bCAM\b",
            @"\bTS\b",
            @"\bTC\b",
            @"\bx264\b", @"\bx265\b", @"\bh264\b", @"\bh265\b",
            @"\bHEVC\b", @"\bAVC\b", @"\bAV1\b",
            @"\b10[.\-]?bit\b", @"\b8[.\-]?bit\b",
            @"\bAAC\b", @"\bAC3\b", @"\bDTS\b", @"\bTrueHD\b", @"\bAtmos\b",
            @"\bFLAC\b", @"\bMP3\b",
            @"\bMULTI\b", @"\bDual[.\-]?Audio\b",
            @"\bUkr\b", @"\bUKR\b", @"\bENG\b", @"\bRUS\b", @"\bUA\b", @"\bEN\b",
            @"\bSubbed\b", @"\bSubs\b", @"\bProper\b", @"\bRepack\b", @"\bExtended\b",
            @"\bRemastered\b", @"\bDirectors?[.\-]?Cut\b",
            @"\bYIFY\b", @"\bYTS\b", @"\bRARBG\b", @"\bSparks\b", @"\bFGT\b",
            @"\bAMZN\b", @"\bNF\b", @"\bDSNP\b", @"\bHMAX\b",
            @"\bREMUX\b", @"\bINTERNAL\b",
            @"\bScene\b",
            @"\bSample\b",
            @"\bwww[.\-]\S+",
            @"\b\d{1,2}[.\-]bit\b",
            @"\btorrent\b",                // ⬅️ ДОДАНО
            @"\b\d{5,}\b"                  // ⬅️ ДОДАНО
        };

        public string CleanFileName(string filePath)
        {
            string name = Path.GetFileNameWithoutExtension(filePath);
            return CleanTitle(name);
        }

        public string CleanTitle(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            name = name.Replace('.', ' ').Replace('_', ' ').Replace('-', ' ');

            foreach (var pattern in JunkPatterns)
                name = Regex.Replace(name, pattern, " ", RegexOptions.IgnoreCase);

            // Прибираємо рік з назви (він окремо в ExtractYear)
            name = Regex.Replace(name, @"\b(19|20)\d{2}\b", " ");

            // Хвостові коди релізів (часто великі літери/цифри)
            name = Regex.Replace(name, @"\b[A-Z0-9]{2,}\b$", " ", RegexOptions.IgnoreCase);

            name = Regex.Replace(name, @"\s+", " ").Trim();
            name = name.Trim(' ', '-', '.', '_');

            // ⬇️ ДОДАНО: якщо назва занадто коротка — повертаємо null
            return name.Length < 2 ? null : name;
        }

        public int? ExtractYear(string filePath)
        {
            string name = Path.GetFileNameWithoutExtension(filePath);
            var matches = Regex.Matches(name, @"\b((?:19|20)\d{2})\b");
            if (matches.Count == 0)
                return null;

            for (int i = matches.Count - 1; i >= 0; i--)
            {
                if (int.TryParse(matches[i].Groups[1].Value, out int year) &&
                    year >= 1900 && year <= DateTime.Now.Year + 1)
                    return year;
            }

            return null;
        }

        public List<string> GetSearchQueries(string filePath, string currentTitle = null)
        {
            var queries = new List<string>();

            void Add(string q)
            {
                if (string.IsNullOrWhiteSpace(q)) return;
                q = q.Trim();
                if (!queries.Contains(q, StringComparer.OrdinalIgnoreCase))
                    queries.Add(q);
            }

            Add(CleanFileName(filePath));
            Add(currentTitle);

            string raw = Path.GetFileNameWithoutExtension(filePath)?
                .Replace('.', ' ').Replace('_', ' ') ?? "";
            raw = Regex.Replace(raw, @"\s+", " ").Trim();
            Add(raw);

            var dash = Regex.Split(raw, @"\s+[–—-]\s+");
            if (dash.Length >= 2)
            {
                Add(CleanTitle(dash[^1]));
                Add(CleanTitle(dash[0]));
            }

            var yearCut = Regex.Match(raw, @"^(.*?)[\s.\-_]+(?:19|20)\d{2}\b");
            if (yearCut.Success)
                Add(CleanTitle(yearCut.Groups[1].Value));

            return queries.Where(q => q.Length >= 2).Take(5).ToList();
        }

        public (string Artist, string Title) ParseMusicFileName(string filePath)
        {
            string raw = Path.GetFileNameWithoutExtension(filePath) ?? "";
            raw = raw.Replace('_', ' ');
            raw = Regex.Replace(raw, @"^\d{1,3}[\s.\-_]+", "");

            var parts = Regex.Split(raw, @"\s+[–—-]\s+");
            if (parts.Length >= 2)
            {
                string artist = CleanTitle(parts[0]);
                string title = CleanTitle(string.Join(" - ", parts.Skip(1)));
                return (artist, title);
            }

            return (null, CleanFileName(filePath));
        }

        public string ExtractQuality(string filePath)
        {
            string name = Path.GetFileNameWithoutExtension(filePath);
            var match = Regex.Match(name, @"\b(2160p|1080p|720p|480p|4K|UHD)\b", RegexOptions.IgnoreCase);
            return match.Success ? match.Value.ToUpper() : null;
        }
    }
}