using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using MediaVault.Models;

namespace MediaVault.Services
{
    public class FileScanner
    {
        private static readonly string[] MediaExtensions =
        {
            ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv",
            ".pdf", ".epub", ".fb2", ".mobi", ".djvu",
            ".mp3", ".flac", ".wav", ".aac", ".ogg",
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp"
        };

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
                    var allFoundFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);

                    int total = allFoundFiles.Length;
                    int current = 0;

                    foreach (var file in allFoundFiles)
                    {
                        try
                        {
                            string ext = Path.GetExtension(file).ToLowerInvariant();
                            if (MediaExtensions.Contains(ext))
                            {
                                files.Add(file);
                            }
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