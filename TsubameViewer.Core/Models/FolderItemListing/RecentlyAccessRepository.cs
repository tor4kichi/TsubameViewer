using LiteDB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using TsubameViewer.Core.Contracts.Maintenance;
using TsubameViewer.Core.Infrastructure;
using ZLinq;

namespace TsubameViewer.Core.Models.FolderItemListing;

public sealed class RecentlyAccessRepository 
    : ILaunchTimeMaintenance
{
    private readonly ILiteCollection<RecentlyAccessEntry> _collection;
    public static int MaxRecordCount { get; set; } = 100;

    public RecentlyAccessRepository(ILiteDatabase liteDatabase)
    {
        _collection = liteDatabase.GetCollection< RecentlyAccessEntry>();
        _collection.EnsureIndex(x => x.Path);
        _collection.EnsureIndex(x => x.LastAccess);
    }

    public void AddWatched(string path)
    {
        Upsert(path, DateTimeOffset.Now);
    }

    public void AddWatched(string path, DateTimeOffset lastAccess)
    {
        Upsert(path, lastAccess);
    }

    public List<(string Path, DateTimeOffset LastAccessTime)> GetItemsSortWithRecently(int take)
    {
        var items = _collection.Query().OrderByDescending(x => x.LastAccess).Limit(take).ToEnumerable();
        return items.Take(take).Select(x => (x.Path, x.LastAccess)).ToList();
    }

    public void Delete(RecentlyAccessEntry entry)
    {
        _collection.Delete(entry.Path);
    }

    public void Delete(string path)
    {
        _collection.DeleteMany(x => x.Path.Equals(path, StringComparison.Ordinal));
    }

    public void PathChanged(string oldPath, string newPath)
    {
        if (string.IsNullOrEmpty(Path.GetExtension(oldPath)))
        {
            var entires = _collection.Find(x => x.Path.StartsWith(oldPath, StringComparison.Ordinal)).AsValueEnumerable().ToArrayPool();
            StringBuilder sb = new();
            foreach (var entry in entires.Span)
            {
                _collection.Delete(entry.Path);
                Debug.WriteLine($"RecentlyAccess Path changing: {entry.Path}");
                sb.Clear();
                sb.Append(entry.Path);
                sb.Replace(oldPath, newPath);
                entry.Path = sb.ToString();
                _collection.Upsert(entry);
                Debug.WriteLine($"RecentlyAccess Path changed: {entry.Path}");
            }
        }
        else
        {
            var entry = _collection.FindOne(x => x.Path.Equals(oldPath, StringComparison.Ordinal));
            if (entry == null) { return; }
            _collection.Delete(entry.Path);
            Debug.WriteLine($"RecentlyAccess Path changing: {entry.Path}");
            entry.Path = newPath;
            _collection.Upsert(entry);
            Debug.WriteLine($"RecentlyAccess Path changed: {entry.Path}");
        }
    }

    void ILaunchTimeMaintenance.Maintenance()
    {
        MaintenanceRecordLimit(MaxRecordCount);
    }


    int MaintenanceRecordLimit(int limit)
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


    void Upsert(string path, DateTimeOffset lastAccess)
    {
        var existItem = _collection.FindOne(x => x.Path.Equals(path, StringComparison.Ordinal));
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

    public void DeleteAllUnderPath(string path)
    {
        _collection.DeleteMany(x => path.StartsWith(x.Path, StringComparison.Ordinal));
    }

    public void DeleteAll()
    {
        _collection.DeleteAll();
    }

    public class RecentlyAccessEntry
    {
        [BsonId]
        public string Path { get; set; }

        [BsonField]
        public DateTimeOffset LastAccess { get; set; }
    }
}
