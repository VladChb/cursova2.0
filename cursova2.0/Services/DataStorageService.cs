using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using MediaVault.Models;

namespace MediaVault.Services
{
    public class DataStorageService
    {
        private readonly string _dataFolder;
        private readonly string _mediaFilePath;

        public DataStorageService()
        {
            _dataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MediaVault");

            if (!Directory.Exists(_dataFolder))
                Directory.CreateDirectory(_dataFolder);

            _mediaFilePath = Path.Combine(_dataFolder, "media_collection.json");
        }

        public string MediaFilePath => _mediaFilePath;

        public async Task SaveMediaItemsAsync(List<MediaItem> items)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                string json = JsonSerializer.Serialize(items, options);
                await File.WriteAllTextAsync(_mediaFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Помилка збереження: {ex.Message}");
                throw;
            }
        }

        public async Task<List<MediaItem>> LoadMediaItemsAsync()
        {
            try
            {
                if (!File.Exists(_mediaFilePath))
                    return new List<MediaItem>();

                string json = await File.ReadAllTextAsync(_mediaFilePath);

                if (string.IsNullOrWhiteSpace(json))
                    return new List<MediaItem>();

                var items = JsonSerializer.Deserialize<List<MediaItem>>(json);
                return items ?? new List<MediaItem>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Помилка завантаження: {ex.Message}");
                return new List<MediaItem>();
            }
        }

        public async Task ClearCollectionAsync()
        {
            await Task.Run(() =>
            {
                if (File.Exists(_mediaFilePath))
                    File.Delete(_mediaFilePath);
            });
        }

        public bool CollectionExists()
        {
            return File.Exists(_mediaFilePath);
        }
    }
}