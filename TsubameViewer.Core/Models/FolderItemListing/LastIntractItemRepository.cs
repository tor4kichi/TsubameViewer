using LiteDB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using TsubameViewer.Core.Contracts.Services;
using TsubameViewer.Core.Infrastructure;
using ZLinq;

namespace TsubameViewer.Core.Models.FolderItemListing;


public sealed class FolderLastIntractItem
{
    [BsonId]
    public string Path { get; set; }

    [BsonField]
    public string ItemName { get; set; }
}


public sealed class AlbamLastIntractItem
{
    [BsonId]
    public Guid AlbamId { get; set; }

    [BsonField]
    public string AlbamItemPath { get; set; }
}


public sealed class LastIntractItemRepository
{
    private readonly ILiteCollection<FolderLastIntractItem> _folderCollection;
    private readonly ILiteCollection<AlbamLastIntractItem> _albamCollection;

    public LastIntractItemRepository(ILiteDatabase liteDatabase)
    {
        _folderCollection = liteDatabase.GetCollection<FolderLastIntractItem>();
        _albamCollection = liteDatabase.GetCollection<AlbamLastIntractItem>();
        _albamCollection.EnsureIndex(x => x.AlbamItemPath);
    }

    public string GetLastIntractItemName(string path)
    {
        return _folderCollection.FindById(path)?.ItemName;
    }

    public void SetLastIntractItemName(string path, string itemName)
    {
        if (Path.IsPathRooted(itemName))
        {
            itemName = Path.GetFileName(itemName);
        }
        _folderCollection.Upsert(new FolderLastIntractItem() { Path = path, ItemName = itemName });
    }

    public void Remove(string path)
    {
        // Deleteだと ドライブレターに使われる : によって例外が生じる
        _folderCollection.DeleteMany(x => x.Path.Equals(path, StringComparison.Ordinal));
    }

    public void RemoveAllUnderPath(string path)
    {
        _folderCollection.DeleteMany(x => path.StartsWith(x.Path, StringComparison.Ordinal));
    }



    public string GetLastIntractItemName(Guid albamId)
    {
        return _albamCollection.FindById(albamId)?.AlbamItemPath;
    }

    public void SetLastIntractItemName(Guid albamId, string itemPath)
    {
        _albamCollection.Upsert(new AlbamLastIntractItem() { AlbamId = albamId, AlbamItemPath = itemPath });
    }

    public void Remove(Guid albamId)
    {
        _albamCollection.Delete(albamId);
    }
}
