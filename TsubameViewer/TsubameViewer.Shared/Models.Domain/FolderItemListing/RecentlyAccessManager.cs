using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TsubameViewer.Models.Infrastructure;

namespace TsubameViewer.Models.Domain.FolderItemListing
{
    public sealed class RecentlyAccessManager 
    {
        public const int RecordCount = 100;
        private readonly RecentlyAccessRepository _recentlyAccessRepository;

        public RecentlyAccessManager(RecentlyAccessRepository recentlyAccessRepository)
        {
            _recentlyAccessRepository = recentlyAccessRepository;
        }

        public void AddWatched(string path)
        {
            _recentlyAccessRepository.Upsert(path, DateTimeOffset.Now);
        }

        public void AddWatched(string path, DateTimeOffset lastAccess)
        {
            _recentlyAccessRepository.Upsert(path, lastAccess);
        }

        public List<RecentlyAccessEntry> GetItemsSortWithRecently(int take)
        {
            return _recentlyAccessRepository.GetItemsSortWithRecently(take);
        }

        public void Delete(RecentlyAccessEntry entry)
        {
            _recentlyAccessRepository.DeleteItem(entry.Path);
        }

        public void Delete(string path)
        {
            _recentlyAccessRepository.Delete(path);
        }

        public void DeleteAllUnderPath(string path)
        {
            _recentlyAccessRepository.DeleteAllUnderPath(path);
        }

        public sealed class RecentlyAccessRepository : LiteDBServiceBase<RecentlyAccessEntry>
        {
            public RecentlyAccessRepository(ILiteDatabase liteDatabase) : base(liteDatabase)
            {
                _collection.EnsureIndex(x => x.Path);
                _collection.EnsureIndex(x => x.LastAccess);
            }

            public void Upsert(string path, DateTimeOffset lastAccess)
            {
                var existItem = _collection.FindOne(x => x.Path == path);
                if (existItem != null)
                {
                    existItem.LastAccess = lastAccess;
                    _collection.Update(existItem);
                    return;
                }
                else
                {
                    var count = _collection.Count();
                    if (count > RecordCount)
                    {
                        var first = _collection.Query().OrderBy(x => x.LastAccess).First();
                        _collection.Delete(first.Path);
                    }

                    _collection.Insert(new RecentlyAccessEntry() { Path = path, LastAccess = lastAccess });
                }
            }

            public List<RecentlyAccessEntry> GetItemsSortWithRecently(int take)
            {
                return _collection.Query().OrderByDescending(x => x.LastAccess).Limit(take).ToList();
            }

            public void Delete(string path)
            {
                _collection.DeleteMany(x => x.Path == path);
            }

            public void DeleteAllUnderPath(string path)
            {
                _collection.DeleteMany(x => path.StartsWith(x.Path));
            }
        }

        public class RecentlyAccessEntry
        {
            [BsonId]
            public string Path { get; set; }

            [BsonField]
            public DateTimeOffset LastAccess { get; set; }
        }
    }
}
