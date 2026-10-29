using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace MediaVault.Services
{
    public static class IconExtractor
    {
        private static string CoversFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MediaVault", "Covers");

        /// <summary>
        /// Витягує іконку .exe/.msi і зберігає PNG у кеш обкладинок.
        /// Повертає шлях до PNG або null.
        /// </summary>
        public static string ExtractToPoster(string filePath, string itemId = null)
        {
            try
            {
                if (!File.Exists(filePath))
                    return null;

                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                if (ext is not (".exe" or ".msi" or ".com" or ".scr"))
                    return null;

                Directory.CreateDirectory(CoversFolder);

                string fileName = $"{itemId ?? Path.GetFileNameWithoutExtension(filePath)}_icon.png";
                foreach (char c in Path.GetInvalidFileNameChars())
                    fileName = fileName.Replace(c, '_');

                string outPath = Path.Combine(CoversFolder, fileName);

                using var icon = Icon.ExtractAssociatedIcon(filePath);
                if (icon == null)
                    return null;

                using var bitmap = icon.ToBitmap();
                // Масштаб для картки
                using var resized = new Bitmap(bitmap, new Size(256, 256));
                resized.Save(outPath, ImageFormat.Png);

                return outPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Icon extract {filePath}: {ex.Message}");
                return null;
            }
        }
    }
}
