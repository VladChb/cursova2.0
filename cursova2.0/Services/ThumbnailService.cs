using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MediaVault.Services
{
    public static class ThumbnailService
    {
        private static readonly string ThumbFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MediaVault", "Thumbnails");

        private const int ThumbSize = 300;

        public static string GetThumbnailPath(string imagePath)
        {
            try
            {
                if (!File.Exists(imagePath))
                    return imagePath;

                if (!Directory.Exists(ThumbFolder))
                    Directory.CreateDirectory(ThumbFolder);

                string fileName = Path.GetFileNameWithoutExtension(imagePath) + "_thumb.jpg";
                foreach (char c in Path.GetInvalidFileNameChars())
                    fileName = fileName.Replace(c, '_');

                string thumbPath = Path.Combine(ThumbFolder, fileName);

                if (File.Exists(thumbPath))
                    return thumbPath;

                // Створюємо мініатюру
                using var stream = File.OpenRead(imagePath);
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnLoad);
                var frame = decoder.Frames[0];

                double scale = (double)ThumbSize / Math.Max(frame.PixelWidth, frame.PixelHeight);
                if (scale > 1) scale = 1;

                var transform = new TransformedBitmap(frame, new ScaleTransform(scale, scale));

                var encoder = new JpegBitmapEncoder { QualityLevel = 85 };
                encoder.Frames.Add(BitmapFrame.Create(transform));

                using var fs = File.Create(thumbPath);
                encoder.Save(fs);

                return thumbPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Thumbnail помилка {imagePath}: {ex.Message}");
                return imagePath; 
            }
        }

        public static void ClearCache()
        {
            try
            {
                if (Directory.Exists(ThumbFolder))
                    Directory.Delete(ThumbFolder, true);
            }
            catch { }
        }
    }
}