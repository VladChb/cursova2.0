using System;
using System.Collections.Generic;
using System.Linq;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.VisualElements;
using SkiaSharp;
using MediaVault.Models;

namespace MediaVault.ViewModels
{
    public class DashboardViewModel
    {
        public ISeries[] TypeSeries { get; set; }
        public LabelVisual TypeTitle { get; set; }

        public SolidColorPaint LegendTextPaint { get; set; }
        public SolidColorPaint TooltipTextPaint { get; set; }

        public ISeries[] YearSeries { get; set; }
        public Axis[] YearXAxis { get; set; }
        public Axis[] YearYAxis { get; set; }

        public ISeries[] GenreSeries { get; set; }
        public Axis[] GenreXAxis { get; set; }
        public Axis[] GenreYAxis { get; set; }

        public ISeries[] LargestSeries { get; set; }
        public Axis[] LargestXAxis { get; set; }
        public Axis[] LargestYAxis { get; set; }

        public int TotalFiles { get; set; }
        public int TotalFilms { get; set; }
        public int TotalShortVideos { get; set; }
        public int TotalBooks { get; set; }
        public int TotalMusic { get; set; }
        public int TotalImages { get; set; }
        public int TotalPrograms { get; set; }
        public string TotalSize { get; set; }

        public DashboardViewModel(List<MediaItem> items)
        {
            var typeface = SKTypeface.FromFamilyName("Segoe UI");

            LegendTextPaint = new SolidColorPaint(SKColor.Parse("#1D1D1F")) { SKTypeface = typeface };
            TooltipTextPaint = new SolidColorPaint(SKColor.Parse("#FFFFFF")) { SKTypeface = typeface };

            TotalFiles = items.Count;
            TotalFilms = items.Count(x => x.Type == MediaType.Film);
            TotalShortVideos = items.Count(x => x.Type == MediaType.ShortVideo);
            TotalBooks = items.Count(x => x.Type == MediaType.Book);
            TotalMusic = items.Count(x => x.Type == MediaType.Music);
            TotalImages = items.Count(x => x.Type == MediaType.Image);
            TotalPrograms = items.Count(x => x.Type == MediaType.Program);

            long totalBytes = items.Sum(x => x.FileSize);
            TotalSize = FormatSize(totalBytes);

            TypeSeries = new ISeries[]
            {
                new PieSeries<int> { Values = new[] { TotalFilms }, Name = "Фільми", Fill = new SolidColorPaint(SKColor.Parse("#1DB954")) { SKTypeface = typeface } },
                new PieSeries<int> { Values = new[] { TotalShortVideos }, Name = "Короткі", Fill = new SolidColorPaint(SKColor.Parse("#FF2D55")) { SKTypeface = typeface } },
                new PieSeries<int> { Values = new[] { TotalBooks }, Name = "Книги", Fill = new SolidColorPaint(SKColor.Parse("#007AFF")) { SKTypeface = typeface } },
                new PieSeries<int> { Values = new[] { TotalMusic }, Name = "Музика", Fill = new SolidColorPaint(SKColor.Parse("#FF9500")) { SKTypeface = typeface } },
                new PieSeries<int> { Values = new[] { TotalImages }, Name = "Зображення", Fill = new SolidColorPaint(SKColor.Parse("#AF52DE")) { SKTypeface = typeface } },
                new PieSeries<int> { Values = new[] { TotalPrograms }, Name = "Програми", Fill = new SolidColorPaint(SKColor.Parse("#5856D6")) { SKTypeface = typeface } }
            };

            TypeTitle = new LabelVisual
            {
                Text = "Distribution by Type",
                TextSize = 16,
                Paint = new SolidColorPaint(SKColor.Parse("#1D1D1F")) { SKTypeface = typeface }
            };

            var yearData = items
                .Where(x => x.Year.HasValue && x.Year > 1900)
                .GroupBy(x => x.Year.Value)
                .OrderBy(x => x.Key)
                .Select(g => new { Year = g.Key, Count = g.Count() })
                .ToList();

            YearSeries = new ISeries[]
            {
                new ColumnSeries<int>
                {
                    Values = yearData.Select(x => x.Count).ToArray(),
                    Name = "Count",
                    Fill = new SolidColorPaint(SKColor.Parse("#1DB954")) { SKTypeface = typeface }
                }
            };

            YearXAxis = new Axis[]
            {
                new Axis
                {
                    Labels = yearData.Select(x => x.Year.ToString()).ToArray(),
                    LabelsRotation = 45,
                    TextSize = 10,
                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#1D1D1F")) { SKTypeface = typeface }
                }
            };

            YearYAxis = new Axis[] { new Axis { TextSize = 10, LabelsPaint = new SolidColorPaint(SKColor.Parse("#1D1D1F")) { SKTypeface = typeface } } };

            var genreData = items
                .Where(x => !string.IsNullOrEmpty(x.Genre))
                .GroupBy(x => x.Genre)
                .OrderByDescending(g => g.Count())
                .Take(8)
                .Select(g => new { Genre = g.Key, Count = g.Count() })
                .ToList();

            GenreSeries = new ISeries[]
            {
                new RowSeries<int>
                {
                    Values = genreData.Select(x => x.Count).ToArray(),
                    Name = "Count",
                    Fill = new SolidColorPaint(SKColor.Parse("#007AFF")) { SKTypeface = typeface }
                }
            };

            GenreXAxis = new Axis[] { new Axis { TextSize = 10, LabelsPaint = new SolidColorPaint(SKColor.Parse("#1D1D1F")) { SKTypeface = typeface } } };

            GenreYAxis = new Axis[]
            {
                new Axis
                {
                    Labels = genreData.Select(x => x.Genre).ToArray(),
                    TextSize = 10,
                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#1D1D1F")) { SKTypeface = typeface }
                }
            };

            var largestData = items
                .OrderByDescending(x => x.FileSize)
                .Take(10)
                .Select(x => new { x.Title, x.FileSize })
                .ToList();

            LargestSeries = new ISeries[]
            {
                new RowSeries<double>
                {
                    Values = largestData.Select(x => (double)x.FileSize / (1024 * 1024)).ToArray(),
                    Name = "Size (MB)",
                    Fill = new SolidColorPaint(SKColor.Parse("#FF9500")) { SKTypeface = typeface }
                }
            };

            LargestXAxis = new Axis[] { new Axis { TextSize = 10, LabelsPaint = new SolidColorPaint(SKColor.Parse("#1D1D1F")) { SKTypeface = typeface } } };

            LargestYAxis = new Axis[]
            {
                new Axis
                {
                    Labels = largestData.Select(x =>
                        x.Title.Length > 25 ? x.Title.Substring(0, 25) + "..." : x.Title
                    ).ToArray(),
                    TextSize = 10,
                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#1D1D1F")) { SKTypeface = typeface }
                }
            };
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