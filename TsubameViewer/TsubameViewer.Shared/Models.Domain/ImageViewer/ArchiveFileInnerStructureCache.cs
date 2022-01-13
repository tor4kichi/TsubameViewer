using LiteDB;
using SharpCompress.Archives;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using TsubameViewer.Models.Infrastructure;

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

            public long Size { get; set; }
        }


        private sealed class ArchiveFileLastSizeCacheRepository : LiteDBServiceBase<ArchiveFileLastSizeCache>
        {
            public ArchiveFileLastSizeCacheRepository(ILiteDatabase liteDatabase) : base(liteDatabase)
            {
            }

            public long? FindById(string path)
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

        public ArchiveFileInnerSturcture AddOrUpdateStructure(string path, IArchive archive, CancellationToken ct)
        {
            List<string> items = new List<string>();
            List<int> fileIndexies = new();
            List<int> folderIndexies = new();
            foreach (var (entry, index) in archive.Entries.Select((x, i) => (x, i)))
            {
                items.Add(entry.Key);
                if (entry.IsDirectory)
                {
                    folderIndexies.Add(index);
                }
                else
                {
                    // Note: 将来の拡張子対応の変更に備えてキャッシュデータ上では絞り込みを行わない。
                    fileIndexies.Add(index);
                }

                ct.ThrowIfCancellationRequested();
            }

            var fileIndexiesSortWithDateTime = archive.Entries
                .Where(x => x.IsDirectory is false)
                .Select((x, i) => (Entry: x, Index: i, DateTime: x.ArchivedTime ?? x.CreatedTime ?? x.LastModifiedTime ?? DateTime.MinValue))
                .OrderBy(x => x.DateTime)
                .Select(x => x.Index)
                .ToArray();
               
            var cacheEntry = new ArchiveFileInnerSturcture()
            {
                Path = path,
                Items = items.ToArray(),
                FileIndexies = fileIndexies.ToArray(),
                FolderIndexies = folderIndexies.ToArray(),
                FileIndexiesSortWithDateTime = fileIndexiesSortWithDateTime,
            };

            _archiveFileLastSizeCacheRepository.UpdateItem(new ArchiveFileLastSizeCache() { Path = path, Size = archive.TotalSize });
            _archiveFileInnerStructureCacheRepository.UpdateItem(cacheEntry);

            Debug.WriteLine($"create Archive file folder structure. {path}");

            return cacheEntry;
        }

        public bool IsArchiveCachedAndSameSize(string path, IArchive archive)
        {
            if (_archiveFileLastSizeCacheRepository.FindById(path) is not null and long size)
            {
                return archive.TotalSize == size;
            }
            else
            {
                return false;
            }
        }

        public ArchiveFileInnerSturcture GetOrCreateStructure(string path, IArchive archive, CancellationToken ct)
        {
            if (IsArchiveCachedAndSameSize(path, archive))
            {
                var cacheEntry = _archiveFileInnerStructureCacheRepository.FindById(path);
                if (cacheEntry is not null)
                {
                    Debug.WriteLine($"get Archive file folder structure from cache. {path}");
                    return cacheEntry;
                }
            }

            return AddOrUpdateStructure(path, archive, ct);
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
