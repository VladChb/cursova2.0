using System.IO;
using System.Text.RegularExpressions;

namespace MediaVault.Services
{
    public class FileNameCleaner
    {
        public string CleanFileName(string filePath)
        {
            string name = Path.GetFileNameWithoutExtension(filePath);

            name = name.Replace('.', ' ').Replace('_', ' ');

            var patterns = new[]
            {
                @"\[.*?\]",           
                @"\(.*?\)",           
                @"\b\d{3,4}p\b",      
                @"\bWEB[.-]?DL\b",
                @"\bBluRay\b",
                @"\bHDRip\b",
                @"\bx264\b", @"\bx265\b",
                @"\bHEVC\b",
                @"\bAAC\b", @"\bAC3\b",
                @"\bMULTI\b",
                @"\bUkr\b", @"\bENG\b",
                @"\bYIFY\b",
                @"\bRARBG\b"
            };

            foreach (var pattern in patterns)
            {
                name = Regex.Replace(name, pattern, "", RegexOptions.IgnoreCase);
            }

            name = Regex.Replace(name, @"\s+", " ").Trim();

            return string.IsNullOrWhiteSpace(name) ? null : name;
        }

        public int? ExtractYear(string filePath)
        {
            string name = Path.GetFileNameWithoutExtension(filePath);

            var match = Regex.Match(name, @"\b(19|20)\d{2}\b");
            if (match.Success && int.TryParse(match.Value, out int year))
            {
                return year;
            }

            return null;
        }
    }
}