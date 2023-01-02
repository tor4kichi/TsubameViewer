using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TsubameViewer.Core.Contracts.Maintenance;
using TsubameViewer.Core.Infrastructure;

namespace TsubameViewer.Core.Models.FolderItemListing;

public sealed class RecentlyAccessRepository 
    : ILaunchTimeMaintenance
{
    private readonly RecentlyAccessRepository_Internal _recentlyAccessRepository;
    public static int MaxRecordCount { get; set; } = 100;

    public RecentlyAccessRepository(RecentlyAccessRepository_Internal recentlyAccessRepository)
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

    public List<(string Path, DateTimeOffset LastAccessTime)> GetItemsSortWithRecently(int take)
    {
        return _recentlyAccessRepository.GetItemsSortWithRecently(take).Select(x => (x.Path, x.LastAccess)).ToList();
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

    void ILaunchTimeMaintenance.Maintenance()
    {
        _recentlyAccessRepository.MaintenanceRecordLimit(MaxRecordCount);
    }

    public sealed class RecentlyAccessRepository_Internal : LiteDBServiceBase<RecentlyAccessEntry>
    {
        public RecentlyAccessRepository_Internal(ILiteDatabase liteDatabase) : base(liteDatabase)
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
                _collection.Insert(new RecentlyAccessEntry() { Path = path, LastAccess = lastAccess });
            }
        }

        public int MaintenanceRecordLimit(int limit)
        {
            var count = _collection.Count();
            if (count > limit)
            {
                int deleteCount = limit - count;
                foreach (var deleteItem in _collection.Query().OrderBy(x => x.LastAccess).Limit(deleteCount).ToArray())
                {
                    _collection.Delete(deleteItem.Path);
                }

                return deleteCount;
            }
            else
            {
                return 0;
            }
        }

        public IEnumerable<RecentlyAccessEntry> GetItemsSortWithRecently(int take)
        {
            return _collection.Query().OrderByDescending(x => x.LastAccess).Limit(take).ToEnumerable();
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
