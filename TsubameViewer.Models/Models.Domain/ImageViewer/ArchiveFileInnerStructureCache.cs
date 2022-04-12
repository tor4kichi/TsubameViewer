using LiteDB;
using SharpCompress.Archives;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using TsubameViewer.Models.Infrastructure;
using Windows.Storage;

namespace TsubameViewer.Models.Domain.ImageViewer
{
    public sealed class ArchiveFileInnerStructureCache
    {
        public sealed class ArchiveFileInnerSturcture
        {
            [BsonId]
            public string Path { get; set; }

            public string[] Items { get; set; }

            /// <summary>
            /// ファイルのインデックス。</br>
            /// </summary>
            public int[] FileIndexies { get; set; }

            public int[] FolderIndexies { get; set; }

            public int[] FileIndexiesSortWithDateTime { get; set; }

            public string RootDirectoryPath { get; set; }

            public Dictionary<string, int[]> FilesByFolder { get; set; }
            
            public char FolderPathSeparator { get; set; }


            public string ReplaceSeparateCharIfAltPathSeparateChar(string path)
            {
                if (FolderPathSeparator == System.IO.Path.AltDirectorySeparatorChar)
                {
                    return path.Replace(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                }
                else
                {
                    return path;
                }
            }
        }

        public sealed record ArchiveFileInnerSturctureItem(string Key, bool IsDirectory);

        private sealed class ArchiveFileInnerStructureCacheRepository : LiteDBServiceBase<ArchiveFileInnerSturcture>
        {
            public ArchiveFileInnerStructureCacheRepository(ILiteDatabase liteDatabase) : base(liteDatabase)
            {
                
            }

            public ArchiveFileInnerSturcture FindById(string path)
            {
                return _collection.FindById(path);
            }

            public int DeleteUnderPath(string path)
            {
                return _collection.DeleteMany(x => x.Path.StartsWith(path));
            }
        }

        sealed class ArchiveFileLastSizeCache
        {
            [BsonId]
            public string Path { get; set; }

            public ulong Size { get; set; }
        }


        private sealed class ArchiveFileLastSizeCacheRepository : LiteDBServiceBase<ArchiveFileLastSizeCache>
        {
            public ArchiveFileLastSizeCacheRepository(ILiteDatabase liteDatabase) : base(liteDatabase)
            {
                
            }

            public ulong? FindById(string path)
            {
                return _collection.FindById(path)?.Size;
            }

            public int DeleteUnderPath(string path)
            {
                return _collection.DeleteMany(x => x.Path.StartsWith(path));
            }
        }

        private readonly ArchiveFileInnerStructureCacheRepository _archiveFileInnerStructureCacheRepository;
        private readonly ArchiveFileLastSizeCacheRepository _archiveFileLastSizeCacheRepository;

        public ArchiveFileInnerStructureCache(ILiteDatabase liteDatabase)
        {
            _archiveFileInnerStructureCacheRepository = new ArchiveFileInnerStructureCacheRepository(liteDatabase);
            _archiveFileLastSizeCacheRepository = new ArchiveFileLastSizeCacheRepository(liteDatabase);
        }

        public ArchiveFileInnerSturcture AddOrUpdateStructure(string path, ulong fileSize, IArchive archive, CancellationToken ct)
        {
            List<string> items = new List<string>();
            List<(IArchiveEntry Entry, int Index)> fileIndexies = new();
            List<int> folderIndexies = new();
            Dictionary<string, List<int>> filesByFolder = new();
            char? folderPathSepalator = null;
            foreach (var (entry, index) in archive.Entries.Select((x, i) => (x, i)))
            {
                items.Add(entry.Key);
                if (entry.IsDirectory)
                {
                    folderIndexies.Add(index);

                    var directoryName = entry.Key;
                    if (filesByFolder.TryGetValue(directoryName, out var filesIndexiesInFolder) is false)
                    {
                        filesByFolder.Add(directoryName, filesIndexiesInFolder = new());
                    }
                }
                else
                {
                    // Note: 将来の拡張子対応の変更に備えてキャッシュデータ上では絞り込みを行わない。
                    fileIndexies.Add((entry, index));

                    var directoryName = Path.GetDirectoryName(entry.Key);
                    if (filesByFolder.TryGetValue(directoryName, out var filesIndexiesInFolder) is false)
                    {
                        filesByFolder.Add(directoryName, filesIndexiesInFolder = new());
                    }

                    filesIndexiesInFolder.Add(index);

                    if (folderPathSepalator == null)
                    {
                        if (entry.Key.Any(c => c == Path.DirectorySeparatorChar))
                        {
                            folderPathSepalator = Path.DirectorySeparatorChar;
                        }
                        else if (entry.Key.Any(c => c == Path.AltDirectorySeparatorChar))
                        {
                            folderPathSepalator = Path.AltDirectorySeparatorChar;
                        }
                    }
                }

                ct.ThrowIfCancellationRequested();
            }

            // Path.GetDirectoryName が / を\\に変更するので
            if (folderPathSepalator == Path.AltDirectorySeparatorChar)
            {
                filesByFolder = filesByFolder.Select(x => (Key: x.Key.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), Value: x.Value)).ToDictionary(x => x.Key, x => x.Value);
            }

            var fileIndexiesSortWithDateTime = fileIndexies
                .Select((x, i) => (Entry: x.Entry, Index: x.Index, DateTime: x.Entry.ArchivedTime ?? x.Entry.CreatedTime ?? x.Entry.LastModifiedTime ?? DateTime.MinValue))
                .OrderBy(x => x.DateTime)
                .Select(x => x.Index)
                .ToArray();

            folderPathSepalator ??= Path.DirectorySeparatorChar;

            var cacheEntry = new ArchiveFileInnerSturcture()
            {
                Path = path,
                Items = items.ToArray(),
                FileIndexies = fileIndexies.Select(x => x.Index).ToArray(),
                FolderIndexies = folderIndexies.ToArray(),
                FileIndexiesSortWithDateTime = fileIndexiesSortWithDateTime,
                FilesByFolder = filesByFolder.ToDictionary(x => x.Key, x => x.Value.ToArray()),
                FolderPathSeparator = folderPathSepalator.Value,
                RootDirectoryPath = string.Join(folderPathSepalator.Value, filesByFolder.Keys.Select(x => x.Split(folderPathSepalator.Value)).Aggregate((a, b) => a.Intersect(b).ToArray())),
            };

            Debug.WriteLine(cacheEntry.RootDirectoryPath);

            _archiveFileLastSizeCacheRepository.UpdateItem(new ArchiveFileLastSizeCache() { Path = path, Size = fileSize });
            _archiveFileInnerStructureCacheRepository.UpdateItem(cacheEntry);

            Debug.WriteLine($"create Archive file folder structure. {path}");

            return cacheEntry;
        }

        public bool IsArchiveCachedAndSameSize(string path, ulong fileSize)
        {
            if (_archiveFileLastSizeCacheRepository.FindById(path) is not null and ulong cachedSize)
            {
                return fileSize == cachedSize;
            }
            else
            {
                return false;
            }
        }

        public ArchiveFileInnerSturcture GetOrCreateStructure(string path, ulong fileSize, IArchive archive, CancellationToken ct)
        {
            if (IsArchiveCachedAndSameSize(path, fileSize))
            {
                var cacheEntry = _archiveFileInnerStructureCacheRepository.FindById(path);
                if (cacheEntry is not null)
                {
                    Debug.WriteLine($"get Archive file folder structure from cache. {path}");
                    return cacheEntry;
                }
            }

            return AddOrUpdateStructure(path, fileSize, archive, ct);
        }

        public void Delete(string path)
        {
            _archiveFileInnerStructureCacheRepository.DeleteItem(path);
            _archiveFileLastSizeCacheRepository.DeleteItem(path);
        }

        public int DeleteUnderPath(string path)
        {
            var count1 = _archiveFileInnerStructureCacheRepository.DeleteUnderPath(path);
            var count2 = _archiveFileLastSizeCacheRepository.DeleteUnderPath(path);

            return count1;
        }
    }

}
