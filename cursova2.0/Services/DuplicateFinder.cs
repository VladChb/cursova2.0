using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FuzzySharp;
using MediaVault.Models;

namespace MediaVault.Services
{
    public class DuplicateFinder
    {
        private const int SimilarNameScore = 82;

        /// <summary>
        /// Дублікат — лише однаковий SHA-256.
        /// Схожі назви (наприклад «Звіт1» / «Звіт2») не вважаються копіями,
        /// поки хеш не підтвердить той самий вміст.
        /// Хешуються лише файли з однаковим розміром або зі схожою назвою.
        /// </summary>
        public async Task<List<DuplicateGroup>> FindDuplicatesAsync(
            List<MediaItem> items,
            IProgress<int> progress = null)
        {
            return await Task.Run(() =>
            {
                var existing = items.Where(x => File.Exists(x.FilePath)).ToList();
                var toHash = new HashSet<MediaItem>();

                foreach (var sizeGroup in existing.GroupBy(x => x.FileSize).Where(g => g.Count() > 1))
                {
                    foreach (var item in sizeGroup)
                        toHash.Add(item);
                }

                var named = existing
                    .Select(x => (Item: x, Name: NormalizeName(x.Title ?? Path.GetFileNameWithoutExtension(x.FileName))))
                    .Where(x => x.Name.Length >= 3)
                    .ToList();

                for (int i = 0; i < named.Count; i++)
                {
                    for (int j = i + 1; j < named.Count; j++)
                    {
                        if (named[i].Item.FileSize != named[j].Item.FileSize)
                            continue;

                        if (named[i].Name.Equals(named[j].Name, StringComparison.OrdinalIgnoreCase) ||
                            Fuzz.Ratio(named[i].Name, named[j].Name) >= SimilarNameScore)
                        {
                            toHash.Add(named[i].Item);
                            toHash.Add(named[j].Item);
                        }
                    }
                }

                var list = toHash.ToList();
                int total = Math.Max(list.Count, 1);
                int current = 0;
                var hashMap = new Dictionary<string, List<MediaItem>>(StringComparer.OrdinalIgnoreCase);

                foreach (var item in list)
                {
                    try
                    {
                        string hash = item.Sha256Hash;
                        if (string.IsNullOrEmpty(hash))
                        {
                            hash = FileHashService.ComputeSha256(item.FilePath);
                            item.Sha256Hash = hash;
                        }

                        if (!hashMap.ContainsKey(hash))
                            hashMap[hash] = new List<MediaItem>();

                        hashMap[hash].Add(item);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Помилка хешування {item.FilePath}: {ex.Message}");
                    }

                    current++;
                    progress?.Report((int)((double)current / total * 100));
                }

                var groups = new List<DuplicateGroup>();
                foreach (var kvp in hashMap.Where(x => x.Value.Count > 1))
                {
                    bool similarNames = HaveSimilarNames(kvp.Value);
                    groups.Add(new DuplicateGroup
                    {
                        Hash = kvp.Key,
                        MatchType = similarNames ? DuplicateMatchType.SimilarNameConfirmedByHash : DuplicateMatchType.Hash,
                        MatchKey = similarNames ? "Схожі назви, вміст ідентичний" : null,
                        Items = kvp.Value,
                        TotalSize = kvp.Value.Sum(x => x.FileSize)
                    });
                }

                progress?.Report(100);
                return groups.OrderByDescending(g => g.TotalSize).ToList();
            });
        }

        private static bool HaveSimilarNames(List<MediaItem> items)
        {
            var names = items
                .Select(x => NormalizeName(x.Title ?? Path.GetFileNameWithoutExtension(x.FileName)))
                .Where(x => x.Length > 0)
                .ToList();

            if (names.Count < 2)
                return false;

            return Fuzz.Ratio(names[0], names[1]) >= SimilarNameScore;
        }

        private static string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            return new string(name
                .ToLowerInvariant()
                .Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
                .ToArray())
                .Trim();
        }

        public void DeleteFiles(List<MediaItem> items)
        {
            foreach (var item in items)
            {
                try
                {
                    if (File.Exists(item.FilePath))
                        File.Delete(item.FilePath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Не вдалося видалити {item.FilePath}: {ex.Message}");
                }
            }
        }
    }

    public enum DuplicateMatchType
    {
        Hash,
        SimilarNameConfirmedByHash,
        Name
    }

    public class DuplicateGroup
    {
        public string Hash { get; set; }
        public string MatchKey { get; set; }
        public DuplicateMatchType MatchType { get; set; } = DuplicateMatchType.Hash;
        public List<MediaItem> Items { get; set; } = new();
        public long TotalSize { get; set; }
        public int Count => Items.Count;

        public string MatchTypeLabel => MatchType switch
        {
            DuplicateMatchType.SimilarNameConfirmedByHash => "Схожі назви, підтверджено хешем",
            DuplicateMatchType.Name => "За назвою",
            _ => "Ідентичний вміст (SHA-256)"
        };

        public string FormattedTotalSize
        {
            get
            {
                if (TotalSize < 1024) return $"{TotalSize} B";
                if (TotalSize < 1024 * 1024) return $"{TotalSize / 1024.0:F1} KB";
                if (TotalSize < 1024L * 1024 * 1024) return $"{TotalSize / (1024.0 * 1024):F1} MB";
                return $"{TotalSize / (1024.0 * 1024 * 1024):F2} GB";
            }
        }
    }
}
