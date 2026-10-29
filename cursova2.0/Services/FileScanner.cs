using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MediaVault.Models;

namespace MediaVault.Services
{
    public class FileScanner
    {
        public static readonly TimeSpan MovieMinDuration = TimeSpan.FromMinutes(30);

        private static readonly string[] VideoExtensions =
            { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".ts" };

        private static readonly string[] BookExtensions =
            { ".pdf", ".epub", ".fb2", ".mobi", ".djvu", ".azw", ".azw3" };

        private static readonly string[] MusicExtensions =
            { ".mp3", ".flac", ".wav", ".aac", ".ogg", ".m4a", ".wma" };

        private static readonly string[] ImageExtensions =
            { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };

        private static readonly string[] DocumentExtensions =
            { ".doc", ".docx", ".txt", ".rtf", ".odt", ".xls", ".xlsx", ".ppt", ".pptx", ".csv" };

        private static readonly string[] ProgramExtensions =
            { ".exe", ".msi", ".msix", ".appx", ".bat", ".cmd", ".ps1", ".apk" };

        private static readonly HashSet<string> MediaExtensions =
            VideoExtensions
                .Concat(BookExtensions)
                .Concat(MusicExtensions)
                .Concat(ImageExtensions)
                .Concat(DocumentExtensions)
                .Concat(ProgramExtensions)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        public async Task<List<string>> ScanFolderAsync(string folderPath,
     Action<int> progressCallback = null)
        {
            return await Task.Run(() =>
            {
                var files = new List<string>();

                if (!Directory.Exists(folderPath))
                    return files;

                try
                {
                    var allFoundFiles = new List<string>();

                    var options = new EnumerationOptions
                    {
                        IgnoreInaccessible = true, 
                        RecurseSubdirectories = true
                    };

                    try
                    {
                        allFoundFiles.AddRange(Directory.EnumerateFiles(folderPath, "*.*", options));
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Помилка сканування: {ex.Message}");
                    }

                    int total = allFoundFiles.Count;
                    int current = 0;

                    foreach (var file in allFoundFiles)
                    {
                        try
                        {
                            string ext = Path.GetExtension(file).ToLowerInvariant();
                            if (MediaExtensions.Contains(ext))
                                files.Add(file);
                        }
                        catch { }

                        current++;

                        if (current % 10 == 0 || current == total)
                        {
                            int percent = total > 0 ? (int)((double)current / total * 100) : 0;
                            progressCallback?.Invoke(percent);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Помилка: {ex.Message}");
                }

                return files;
            });
        }


        public bool IsSupportedFile(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            return MediaExtensions.Contains(ext);
        }

        public MediaType GetMediaType(string filePath, double? durationSeconds = null)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            if (VideoExtensions.Contains(ext))
            {
                if (durationSeconds.HasValue && durationSeconds.Value > 0 &&
                    TimeSpan.FromSeconds(durationSeconds.Value) < MovieMinDuration)
                    return MediaType.ShortVideo;

                return MediaType.Film;
            }

            if (BookExtensions.Contains(ext))
                return MediaType.Book;
            if (MusicExtensions.Contains(ext))
                return MediaType.Music;
            if (ImageExtensions.Contains(ext))
                return MediaType.Image;
            if (DocumentExtensions.Contains(ext))
                return MediaType.Document;
            if (ProgramExtensions.Contains(ext))
                return MediaType.Program;

            return MediaType.Unknown;
        }

        public bool IsVideoFile(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            return VideoExtensions.Contains(ext);
        }

        /// <summary>Читає тривалість через TagLib (сек).</summary>
        public double? ReadDurationSeconds(string filePath)
        {
            try
            {
                using var file = TagLib.File.Create(filePath);
                if (file.Properties?.Duration != null && file.Properties.Duration.TotalSeconds > 0)
                    return file.Properties.Duration.TotalSeconds;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Тривалість {filePath}: {ex.Message}");
            }

            return null;
        }
    }
}
