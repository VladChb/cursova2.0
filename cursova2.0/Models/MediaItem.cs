using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MediaVault.Models
{
    public class MediaItem : INotifyPropertyChanged
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        private string _filePath;
        public string FilePath
        {
            get => _filePath;
            set { _filePath = value; OnPropertyChanged(); }
        }

        private string _fileName;
        public string FileName
        {
            get => _fileName;
            set { _fileName = value; OnPropertyChanged(); }
        }

        private string _title;
        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }

        private string _author;
        public string Author
        {
            get => _author;
            set { _author = value; OnPropertyChanged(); }
        }

        private int? _year;
        public int? Year
        {
            get => _year;
            set { _year = value; OnPropertyChanged(); }
        }

        private string _genre;
        public string Genre
        {
            get => _genre;
            set { _genre = value; OnPropertyChanged(); }
        }

        private string _description;
        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        private string _posterPath;
        public string PosterPath
        {
            get => _posterPath;
            set { _posterPath = value; OnPropertyChanged(); }
        }

        private string _posterUrl;
        public string PosterUrl
        {
            get => _posterUrl;
            set { _posterUrl = value; OnPropertyChanged(); }
        }

        private string _trackUrl;
        public string TrackUrl
        {
            get => _trackUrl;
            set { _trackUrl = value; OnPropertyChanged(); }
        }

        public string FileExtension { get; set; }
        public long FileSize { get; set; }
        public DateTime DateAdded { get; set; } = DateTime.Now;

        /// <summary>Тривалість у секундах (відео/аудіо).</summary>
        private double? _durationSeconds;
        public double? DurationSeconds
        {
            get => _durationSeconds;
            set { _durationSeconds = value; OnPropertyChanged(); OnPropertyChanged(nameof(FormattedDuration)); }
        }

        /// <summary>SHA-256 хеш файлу (для пошуку та дублікатів).</summary>
        private string _sha256Hash;
        public string Sha256Hash
        {
            get => _sha256Hash;
            set { _sha256Hash = value; OnPropertyChanged(); }
        }

        private MediaType _type;
        public MediaType Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(); OnPropertyChanged(nameof(TypeIcon)); }
        }

        private int _rating;
        public int Rating
        {
            get => _rating;
            set { _rating = value; OnPropertyChanged(); }
        }

        private bool _isFavorite;
        public bool IsFavorite
        {
            get => _isFavorite;
            set { _isFavorite = value; OnPropertyChanged(); }
        }

        private string _notes;
        public string Notes
        {
            get => _notes;
            set { _notes = value; OnPropertyChanged(); }
        }

        public bool IsIdentified { get; set; } = true;
        public int? TmdbId { get; set; }
        public double VoteAverage { get; set; }

        private string _quality;
        public string Quality
        {
            get => _quality;
            set { _quality = value; OnPropertyChanged(); }
        }

        private string _isbn;
        public string Isbn
        {
            get => _isbn;
            set { _isbn = value; OnPropertyChanged(); }
        }

        public string TmdbUrl => TmdbId.HasValue
            ? $"https://www.themoviedb.org/movie/{TmdbId}"
            : null;

        public string TypeIcon => Type switch
        {
            MediaType.Film => "🎬",
            MediaType.ShortVideo => "📱",
            MediaType.Book => "📚",
            MediaType.Music => "🎵",
            MediaType.Image => "🖼️",
            MediaType.Document => "📄",
            MediaType.Program => "💻",
            _ => "📁"
        };

        public string FormattedDuration
        {
            get
            {
                if (!DurationSeconds.HasValue || DurationSeconds.Value <= 0)
                    return null;

                var ts = TimeSpan.FromSeconds(DurationSeconds.Value);
                return ts.TotalHours >= 1
                    ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                    : $"{ts.Minutes}:{ts.Seconds:D2}";
            }
        }

        public string FormattedSize
        {
            get
            {
                if (FileSize < 1024) return $"{FileSize} B";
                if (FileSize < 1024 * 1024) return $"{FileSize / 1024.0:F1} KB";
                if (FileSize < 1024L * 1024 * 1024) return $"{FileSize / (1024.0 * 1024):F1} MB";
                return $"{FileSize / (1024.0 * 1024 * 1024):F2} GB";
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public enum MediaType
    {
        Film,
        ShortVideo,
        Book,
        Music,
        Image,
        Document,
        Program,
        Unknown
    }
}
