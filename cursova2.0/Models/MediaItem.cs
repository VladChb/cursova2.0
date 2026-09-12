using System;
using System.Collections.Generic;
using System.Text;

namespace MediaVault.Models
{
    public class MediaItem
    {

        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FilePath { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }      
        public int? Year { get; set; }
        public string Genre { get; set; }
        public string Description { get; set; }
        public string ThumbnailPath { get; set; } 
        public string FileExtension { get; set; }
        public long FileSize { get; set; }       
        public DateTime DateAdded { get; set; } = DateTime.Now;

        
        public MediaType Type { get; set; }

        
        public int Rating { get; set; }          
        public bool IsFavorite { get; set; }
        public string Notes { get; set; }

        
        public bool IsIdentified { get; set; } = true;
    

      public string TypeIcon
        {
            get
            {
                return Type switch
                {
                    MediaType.Film => "🎬",
                    MediaType.Book => "📚",
                    MediaType.Music => "🎵",
                    MediaType.Image => "🖼️",
                    MediaType.Document => "📄",
                    _ => "📁"
                };
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
    }

    public enum MediaType
    {
        Film,
        Book,
        Music,
        Image,
        Document,
        Unknown
    }
}
