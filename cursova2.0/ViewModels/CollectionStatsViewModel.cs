using System;
using System.Collections.Generic;
using System.Linq;
using MediaVault.Helpers;
using MediaVault.Models;

namespace MediaVault.ViewModels
{
    public class CollectionStats
    {
        public int TotalFiles { get; set; }
        public long TotalSize { get; set; }
        public double AverageRating { get; set; }
        public int FilesWithoutRating { get; set; }
        public int FilesWithoutMetadata { get; set; }
        public DateTime? OldestFile { get; set; }
        public DateTime? NewestFile { get; set; }
        public int DuplicateCount { get; set; }

        // By Type
        public Dictionary<string, int> FilesByType { get; set; } = new();
        public Dictionary<string, long> SizeByType { get; set; } = new();

        // Top genres
        public List<(string Genre, int Count)> TopGenres { get; set; } = new();
        public List<(string Author, int Count)> TopAuthors { get; set; } = new();

        // Rating distribution
        public Dictionary<int, int> RatingDistribution { get; set; } = new();
    }

    public class CollectionStatsViewModel : ObservableObject
    {
        private CollectionStats _stats = new();
        public CollectionStats Stats
        {
            get => _stats;
            set => SetProperty(ref _stats, value);
        }

        private string _formattedTotalSize = "0 B";
        public string FormattedTotalSize
        {
            get => _formattedTotalSize;
            set => SetProperty(ref _formattedTotalSize, value);
        }

        private string _averageRatingText = "0.0";
        public string AverageRatingText
        {
            get => _averageRatingText;
            set => SetProperty(ref _averageRatingText, value);
        }

        public void GenerateStats(List<MediaItem> items)
        {
            if (items == null || items.Count == 0)
            {
                Stats = new CollectionStats();
                FormattedTotalSize = "0 B";
                AverageRatingText = "0.0";
                return;
            }

            var stats = new CollectionStats();

            // Total files and size
            stats.TotalFiles = items.Count;
            stats.TotalSize = items.Sum(x => x.FileSize);
            FormattedTotalSize = FormatSize(stats.TotalSize);

            // Rating stats
            var ratedItems = items.Where(x => x.Rating > 0).ToList();
            stats.AverageRating = ratedItems.Any() ? ratedItems.Average(x => x.Rating) : 0;
            AverageRatingText = stats.AverageRating.ToString("F1");
            stats.FilesWithoutRating = items.Count(x => x.Rating == 0);

            // Metadata stats
            stats.FilesWithoutMetadata = items.Count(x =>
                string.IsNullOrEmpty(x.Title) ||
                string.IsNullOrEmpty(x.Author) ||
                string.IsNullOrEmpty(x.Genre) ||
                string.IsNullOrEmpty(x.Description));

            // Date stats
            stats.OldestFile = items.Min(x => x.DateAdded);
            stats.NewestFile = items.Max(x => x.DateAdded);

            // Files by type
            var typeGroups = items.GroupBy(x => x.Type);
            foreach (var group in typeGroups)
            {
                string typeName = group.Key switch
                {
                    MediaType.Film => "Фільми",
                    MediaType.ShortVideo => "Короткі відео",
                    MediaType.Book => "Книги",
                    MediaType.Music => "Музика",
                    MediaType.Image => "Зображення",
                    MediaType.Document => "Документи",
                    MediaType.Program => "Програми",
                    _ => "Інше"
                };
                stats.FilesByType[typeName] = group.Count();
                stats.SizeByType[typeName] = group.Sum(x => x.FileSize);
            }

            // Top genres
            stats.TopGenres = items
                .Where(x => !string.IsNullOrEmpty(x.Genre))
                .GroupBy(x => x.Genre)
                .OrderByDescending(x => x.Count())
                .Take(5)
                .Select(x => (x.Key, x.Count()))
                .ToList();

            // Top authors
            stats.TopAuthors = items
                .Where(x => !string.IsNullOrEmpty(x.Author))
                .GroupBy(x => x.Author)
                .OrderByDescending(x => x.Count())
                .Take(5)
                .Select(x => (x.Key, x.Count()))
                .ToList();

            // Rating distribution
            for (int i = 0; i <= 5; i++)
                stats.RatingDistribution[i] = items.Count(x => x.Rating == i);

            // Duplicates (by SHA256)
            var duplicates = items
                .GroupBy(x => x.Sha256Hash)
                .Where(x => x.Count() > 1)
                .ToList();
            stats.DuplicateCount = duplicates.Sum(x => x.Count() - 1);

            Stats = stats;
        }

        private string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }
    }
}
