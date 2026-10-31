
using System;

namespace MediaVault.Models
{
    public class SecurityItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public DateTime ScannedAt { get; set; } = DateTime.Now;

        // SHA-256
        public string Sha256Hash { get; set; } = string.Empty;

        // Результат перевірки
        public SecurityLevel Level { get; set; } = SecurityLevel.Unknown;

        public string ThreatName { get; set; } = string.Empty;
        public string ThreatDetails { get; set; } = string.Empty;

        // Результати антивірусного сканування
        public int MaliciousCount { get; set; }
        public int SuspiciousCount { get; set; }
        public int TotalEngines { get; set; }

        // Сумісність зі старою властивістю
        public int DetectionCount
        {
            get => MaliciousCount;
            set => MaliciousCount = value;
        }

        public string ScannerName { get; set; } = string.Empty;

        // Додаткова інформація про файл
        public string RealFileType { get; set; } = string.Empty;
        public bool IsTypeMismatch { get; set; }

        public string ScanDetails { get; set; } = string.Empty;

        // Форматований розмір файлу
        public string FormattedSize
        {
            get
            {
                return FormatFileSize(FileSize);
            }
        }

        // Відсоток виявлення загроз
        public string DetectionRatio
        {
            get
            {
                if (TotalEngines <= 0)
                    return "—";

                double percentage = (double)MaliciousCount / TotalEngines * 100;

                return $"{MaliciousCount}/{TotalEngines} ({percentage:F1}%)";
            }
        }

        // Текстовий статус для відображення в таблиці
        public string StatusBadge
        {
            get
            {
                return Level switch
                {
                    SecurityLevel.Safe => "Безпечно",
                    SecurityLevel.Suspicious => "Підозріло",
                    SecurityLevel.Dangerous => "Небезпечно",
                    _ => "Невідомо"
                };
            }
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "Б", "КБ", "МБ", "ГБ", "ТБ" };

            double size = bytes;
            int order = 0;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }
    }

    public enum SecurityLevel
    {
        Unknown,
        Safe,
        Suspicious,
        Dangerous
    }
}
