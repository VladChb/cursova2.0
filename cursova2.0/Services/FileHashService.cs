
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace MediaVault.Services
{
    public static class FileHashService
    {
        public static string ComputeSha256(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var sha256 = SHA256.Create();

            byte[] hash = sha256.ComputeHash(stream);

            return BitConverter.ToString(hash)
                .Replace("-", "")
                .ToLowerInvariant();
        }

        public static Task<string> ComputeSha256Async(string filePath)
        {
            return ComputeSha256Async(
                filePath,
                CancellationToken.None,
                null);
        }

        public static async Task<string> ComputeSha256Async(
            string filePath,
            CancellationToken cancellationToken,
            IProgress<long> progress)
        {
            const int bufferSize = 81920;

            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            using var sha256 = IncrementalHash.CreateHash(
                HashAlgorithmName.SHA256);

            byte[] buffer = new byte[bufferSize];

            long totalBytesRead = 0;
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(
                buffer.AsMemory(0, buffer.Length),
                cancellationToken)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                sha256.AppendData(buffer, 0, bytesRead);

                totalBytesRead += bytesRead;

                progress?.Report(totalBytesRead);
            }

            byte[] hash = sha256.GetHashAndReset();

            return BitConverter.ToString(hash)
                .Replace("-", "")
                .ToLowerInvariant();
        }
    }
}
