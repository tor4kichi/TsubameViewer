using LiteDB;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Infrastructure;

namespace TsubameViewer.Core.Models.Albam;

public sealed record AlbamEntry
{
    [BsonId(autoId: true)]
    public Guid _id { get; init; }

    public string Name { get; init; }

    public DateTimeOffset CreatedAt { get; init; }        
}

public sealed record AlbamItemEntry
{
    [BsonId(autoId: true)]
    public Guid _id { get; init; }


    public Guid AlbamId { get; init; }

    public string Path { get; init; }

    public string Name { get; init; }

    public DateTimeOffset AddedAt { get; init; }

    public AlbamItemType ItemType { get; init; } = AlbamItemType.Image;
}

public enum AlbamItemType
{
    Image,
    FolderOrArchive,
}

public sealed class AlbamRepository
{
    

    sealed class AlbamDatabase : LiteDBServiceBase<AlbamEntry>
    {
        public AlbamDatabase(ILiteDatabase liteDatabase) : base(liteDatabase)
        {
            
        }

        internal AlbamEntry FindById(Guid albamId)
        {
            return _collection.FindById(albamId);
        }
    }


    sealed class AlbamItemDatabase : LiteDBServiceBase<AlbamItemEntry>
    {
        public AlbamItemDatabase(ILiteDatabase liteDatabase) : base(liteDatabase)
        {
            _collection.EnsureIndex(x => x.AlbamId);
            _collection.EnsureIndex(x => x.Path);
            _collection.EnsureIndex(x => x.AddedAt);
            _collection.EnsureIndex(x => x.ItemType);
        }

        public AlbamItemEntry? FindByPath(string path)
        {
            return _collection.FindOne(x => x.Path.Equals(path, StringComparison.Ordinal));
        }

        public int DeleteAlbam(Guid albamId)
        {
            return _collection.DeleteMany(x => x.AlbamId == albamId);
        }

        public bool Delete(Guid albamId, string path)
        {
            return _collection.DeleteMany(x => x.AlbamId == albamId && x.Path == path) > 0;
        }

        public IEnumerable<AlbamItemEntry> GetAlbamItem(Guid albamId, FileSortType sort, int skip = 0, int limit = int.MaxValue)
        {
            return sort switch
            {
                FileSortType.None => _collection.Query().Where(x => x.AlbamId == albamId).Offset(skip).Limit(limit).ToEnumerable(),
                FileSortType.TitleAscending => _collection.Query().Where(x => x.AlbamId == albamId).OrderBy(x => x.Name).Offset(skip).Limit(limit).ToEnumerable(),
                FileSortType.TitleDecending => _collection.Query().Where(x => x.AlbamId == albamId).OrderByDescending(x => x.Name).Offset(skip).Limit(limit).ToEnumerable(),
                FileSortType.UpdateTimeAscending => _collection.Query().Where(x => x.AlbamId == albamId).OrderBy(x => x.AddedAt).Offset(skip).Limit(limit).ToEnumerable(),
                FileSortType.UpdateTimeDecending => _collection.Query().Where(x => x.AlbamId == albamId).OrderByDescending(x => x.AddedAt).Offset(skip).Limit(limit).ToEnumerable(),
                _ => throw new NotSupportedException(sort.ToString()),
            };
        }

        public int DeleteUnderPath(string path)
        {
            return _collection.DeleteMany(x => x.Path.StartsWith(path));
        }


        public IEnumerable<AlbamItemEntry> GetImageItems(FileSortType sort, int skip = 0, int limit = int.MaxValue)
        {
            return sort switch
            {
                FileSortType.None => _collection.Query().Where(x => x.ItemType == AlbamItemType.Image).Offset(skip).Limit(limit).ToEnumerable(),
                FileSortType.TitleAscending => _collection.Query().Where(x => x.ItemType == AlbamItemType.Image).OrderBy(x => x.Name).Offset(skip).Limit(limit).ToEnumerable(),
                FileSortType.TitleDecending => _collection.Query().Where(x => x.ItemType == AlbamItemType.Image).OrderByDescending(x => x.Name).Offset(skip).Limit(limit).ToEnumerable(),
                FileSortType.UpdateTimeAscending => _collection.Query().Where(x => x.ItemType == AlbamItemType.Image).OrderBy(x => x.AddedAt).Offset(skip).Limit(limit).ToEnumerable(),
                FileSortType.UpdateTimeDecending => _collection.Query().Where(x => x.ItemType == AlbamItemType.Image).OrderByDescending(x => x.AddedAt).Offset(skip).Limit(limit).ToEnumerable(),
                _ => throw new NotSupportedException(sort.ToString()),
            };
        }

        public IEnumerable<AlbamItemEntry> GetFolderOrArchiveItems(FileSortType sort, int skip = 0, int limit = int.MaxValue)
        {
            return sort switch
            {
                FileSortType.None => _collection.Query().Where(x => x.ItemType == AlbamItemType.FolderOrArchive).Offset(skip).Limit(limit).ToEnumerable(),
                FileSortType.TitleAscending => _collection.Query().Where(x => x.ItemType == AlbamItemType.FolderOrArchive).OrderBy(x => x.Name).Offset(skip).Limit(limit).ToEnumerable(),
                FileSortType.TitleDecending => _collection.Query().Where(x => x.ItemType == AlbamItemType.FolderOrArchive).OrderByDescending(x => x.Name).Offset(skip).Limit(limit).ToEnumerable(),
                FileSortType.UpdateTimeAscending => _collection.Query().Where(x => x.ItemType == AlbamItemType.FolderOrArchive).OrderBy(x => x.AddedAt).Offset(skip).Limit(limit).ToEnumerable(),
                FileSortType.UpdateTimeDecending => _collection.Query().Where(x => x.ItemType == AlbamItemType.FolderOrArchive).OrderByDescending(x => x.AddedAt).Offset(skip).Limit(limit).ToEnumerable(),
                _ => throw new NotSupportedException(sort.ToString()),
            };
        }

        public void PathChanged(string oldPath, string newPath)
        {
            var items = _collection.Find(x => x.Path == oldPath).ToList();
            foreach (var item in items)
            {
                _collection.Update(item with { Path = newPath });
            }
        }
    }


    private readonly AlbamDatabase _albamDatabase;
    private readonly AlbamItemDatabase _albamItemDatabase;
    private readonly IMessenger _messenger;

    public AlbamRepository(
        ILiteDatabase liteDatabase,
        IMessenger messenger
        )
    {
        _albamDatabase = new AlbamDatabase(liteDatabase);
        _albamItemDatabase = new AlbamItemDatabase(liteDatabase);
        _messenger = messenger;
    }

    public bool IsExistAlbam(Guid albamId)
    {
        return _albamDatabase.Exists(x => x._id == albamId);
    }

    public AlbamEntry CreateAlbam(Guid id, string name)
    {
        var entry = new AlbamEntry { _id = id, Name = name, CreatedAt = DateTimeOffset.Now };
        var createdAlbam = _albamDatabase.CreateItem(entry);
        _messenger.Send(new AlbamCreatedMessage(createdAlbam));
        return createdAlbam;
    }

    public bool DeleteAlbam(Guid albamId)
    {
        _albamItemDatabase.DeleteAlbam(albamId);
        var result = _albamDatabase.DeleteItem(albamId);
        if (result)
        {
            _messenger.Send(new AlbamDeletedMessage(albamId));
        }

        return result;
    }

    public bool DeleteAlbam(AlbamEntry entry)
    {
        return DeleteAlbam(entry._id);
    }

    public void UpdateAlbam(AlbamEntry entry)
    {
        _albamDatabase.UpdateItem(entry);
        _messenger.Send(new AlbamEditedMessage(entry));
    }

    public AlbamEntry GetAlbam(Guid albamId)
    {
        return _albamDatabase.FindById(albamId);
    }

    public List<AlbamEntry> GetAlbams()
    {
        return _albamDatabase.ReadAllItems();
    }


    public bool IsExistAlbamItem(AlbamItemType itemType)
    {
        return _albamItemDatabase.Exists(x => x.ItemType == itemType);
    }

    public bool IsExistAlbamItem(string path)
    {
        return _albamItemDatabase.Exists(x => x.Path == path);
    }

    public bool IsExistAlbamItem(string path, AlbamItemType itemType)
    {
        return _albamItemDatabase.Exists(x => x.ItemType == itemType && x.Path == path);
    }

    public AlbamItemEntry AddAlbamItem(Guid albamId, string path, string name, AlbamItemType itemType)
    {
#if DEBUG
        Guard.IsTrue(_albamDatabase.Exists(x => x._id == albamId), $"Not Exist Id: {albamId}");
#endif
        var createdItem = _albamItemDatabase.CreateItem(new AlbamItemEntry() { _id = Guid.NewGuid(), AlbamId = albamId, Path = path, Name = name, AddedAt = DateTimeOffset.Now, ItemType = itemType });
        _messenger.Send(new AlbamItemAddedMessage(albamId, path, itemType));
        return createdItem;
    }

    public bool DeleteAlbamItem(Guid albamId, string path)
    {
        var item = _albamItemDatabase.FindByPath(path);
        if (item == null) { return false; }

        var result = _albamItemDatabase.Delete(albamId, path);
        if (result)
        {
            _messenger.Send(new AlbamItemRemovedMessage(albamId, path, item.ItemType));
        }

        return result;
    }

    public int GetAlbamItemsCount()
    {
        return _albamItemDatabase.Count();
    }

    public int GetAlbamItemsCount(AlbamItemType itemType)
    {
        return _albamItemDatabase.Count(x => x.ItemType == itemType);
    }


    public IEnumerable<AlbamItemEntry> GetAlbamItems(Guid albamId, FileSortType fileSortType = FileSortType.None, int skip = 0, int limit = int.MaxValue)
    {
        return _albamItemDatabase.GetAlbamItem(albamId, fileSortType, skip, limit);
    }

    public IEnumerable<AlbamItemEntry> GetAlbamImageItems(FileSortType fileSortType = FileSortType.None, int skip = 0, int limit = int.MaxValue)
    {
        return _albamItemDatabase.GetImageItems(fileSortType, skip, limit);
    }

    public IEnumerable<AlbamItemEntry> GetAlbamFolderOrArchiveItems(FileSortType fileSortType = FileSortType.None, int skip = 0, int limit = int.MaxValue)
    {
        return _albamItemDatabase.GetFolderOrArchiveItems(fileSortType, skip, limit);
    }

    public int DeleteAlbamItemsUnderPath(string path)
    {
        return _albamItemDatabase.DeleteUnderPath(path);
    }


    public void PathChanged(string oldPath, string newPath)
    {
        _albamItemDatabase.PathChanged(oldPath, newPath);
    }

}
