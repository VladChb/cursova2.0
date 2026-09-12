using MediaVault.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MediaVault.Services
{
    public class FileScanner
    {
        private static readonly string[] MediaExtensions =
        {
            // Відео
            ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv",
            // Книги
            ".pdf", ".epub", ".fb2", ".mobi", ".djvu",
            // Аудіо
            ".mp3", ".flac", ".wav", ".aac", ".ogg",
            // Зображення
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp"
        };

        public async Task<List<string>> ScanFolderAsync(string folderPath)
        {
            return await Task.Run(() =>
            {
                var files = new List<string>();

                if (!Directory.Exists(folderPath))
                    return files;

                try
                {
                    var allFoundFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);

                    foreach (var file in allFoundFiles)
                    {
                        string ext = Path.GetExtension(file).ToLowerInvariant();
                        if (MediaExtensions.Contains(ext))
                        {
                            files.Add(file);
                        }
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (Exception) { }

                return files;
            });
        }

        public MediaType GetMediaType(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            if (new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv" }.Contains(ext))
                return MediaType.Film;
            if (new[] { ".pdf", ".epub", ".fb2", ".mobi", ".djvu" }.Contains(ext))
                return MediaType.Book;
            if (new[] { ".mp3", ".flac", ".wav", ".aac", ".ogg" }.Contains(ext))
                return MediaType.Music;
            if (new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" }.Contains(ext))
                return MediaType.Image;

            return MediaType.Unknown;
        }
    }
}
