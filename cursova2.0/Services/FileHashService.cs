using System;
using System.IO;
using System.Security.Cryptography;
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
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        public static Task<string> ComputeSha256Async(string filePath)
        {
            return Task.Run(() => ComputeSha256(filePath));
        }
    }
}
