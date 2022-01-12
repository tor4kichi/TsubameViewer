using LiteDB;
using SharpCompress.Archives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using TsubameViewer.Models.Infrastructure;

namespace TsubameViewer.Models.Domain.ImageViewer
{
    public sealed class ArchiveFileInnerStructureCache
    {
        sealed class ArchiveFileInnerSturcture
        {
            [BsonId]
            public string Path { get; set; }

            public List<string> Folders { get; set; }

            public List<string> Files { get; set; }
        }


        private sealed class ArchiveFileInnerStructureCacheRepository : LiteDBServiceBase<ArchiveFileInnerSturcture>
        {
            public ArchiveFileInnerStructureCacheRepository(ILiteDatabase liteDatabase) : base(liteDatabase)
            {
            }

            public ArchiveFileInnerSturcture FindById(string path)
            {
                return _collection.FindById(path);
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
        }

        private readonly ArchiveFileInnerStructureCacheRepository _archiveFileInnerStructureCacheRepository;
        private readonly ArchiveFileLastSizeCacheRepository _archiveFileLastSizeCacheRepository;

        public ArchiveFileInnerStructureCache(ILiteDatabase liteDatabase)
        {
            _archiveFileInnerStructureCacheRepository = new ArchiveFileInnerStructureCacheRepository(liteDatabase);
            _archiveFileLastSizeCacheRepository = new ArchiveFileLastSizeCacheRepository(liteDatabase);
        }

        public (List<string> Folders, Dictionary<string, int> KeyToIndexMap) AddOrUpdateStructure(string path, IArchive archive, CancellationToken ct)
        {
            List<IArchiveEntry> notDirectoryItem = new List<IArchiveEntry>();
            List<IArchiveEntry> directoryItem = new List<IArchiveEntry>();
            foreach (var entry in archive.Entries)
            {
                if (entry.IsDirectory)
                {
                    directoryItem.Add(entry);
                }
                else if (DirectoryPathHelper.GetDirectoryDepth(entry.Key) >= 1 && SupportedFileTypesHelper.IsSupportedImageFileExtension(entry.Key))
                {
                    notDirectoryItem.Add(entry);
                }

                ct.ThrowIfCancellationRequested();
            }

            var cacheEntry = new ArchiveFileInnerSturcture()
            {
                Path = path,
                Folders = directoryItem.Select(x => x.Key).ToList(),
                Files = notDirectoryItem.Select(x => x.Key).ToList(),
            };

            _archiveFileLastSizeCacheRepository.UpdateItem(new ArchiveFileLastSizeCache() { Path = path, Size = archive.TotalSize });
            _archiveFileInnerStructureCacheRepository.UpdateItem(cacheEntry);

            return (cacheEntry.Folders, cacheEntry.Files.Select((key, i) => (key, i)).ToDictionary(x => x.key, (x => x.i)));
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

        public bool TryGetStructure(string path, out (List<string> Folders, Dictionary<string, int> KeyToIndexMap) outSet)
        {
            if (_archiveFileInnerStructureCacheRepository.FindById(path) is not null and var cacheEntry)
            {
                outSet = (cacheEntry.Folders, cacheEntry.Files.Select((key, i) => (key, i)).ToDictionary(x => x.key, (x => x.i)));
                return true;
            }
            else
            {
                outSet = default;
                return false;
            }
        }
    }

}
