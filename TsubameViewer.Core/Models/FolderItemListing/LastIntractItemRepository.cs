using LiteDB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TsubameViewer.Core.Contracts.Services;
using TsubameViewer.Core.Infrastructure;

namespace TsubameViewer.Core.Models.FolderItemListing;

public sealed class LastIntractItemRepository
{
    private readonly FolderLastIntractItemRepository _folderLastIntractItemRepository;
    private readonly AlbamLastIntractItemRepository _albamLastIntractItemRepository;

    public LastIntractItemRepository(
        FolderLastIntractItemRepository folderLastIntractItemRepository,
        AlbamLastIntractItemRepository albamLastIntractItemRepository
        )
    {
        _folderLastIntractItemRepository = folderLastIntractItemRepository;
        _albamLastIntractItemRepository = albamLastIntractItemRepository;
    }

    public string GetLastIntractItemName(string path)
    {
        return _folderLastIntractItemRepository.GetLastIntractItemName(path);
    }

    public void SetLastIntractItemName(string path, string itemName)
    {
        if (Path.IsPathRooted(itemName))
        {
            itemName = Path.GetFileName(itemName);
        }
        _folderLastIntractItemRepository.SetLastIntractItemName(path, itemName);
    }

    public void Remove(string path)
    {
        _folderLastIntractItemRepository.DeleteItem(path);
    }

    public void RemoveAllUnderPath(string path)
    {
        _folderLastIntractItemRepository.DeleteAllUnderPath(path);
    }



    public string GetLastIntractItemName(Guid albamId)
    {
        return _albamLastIntractItemRepository.GetLastIntractItemName(albamId);
    }

    public void SetLastIntractItemName(Guid albamId, string itemPath)
    {
        _albamLastIntractItemRepository.SetLastIntractItemPath(albamId, itemPath);
    }

    public void Remove(Guid albamId)
    {
        _albamLastIntractItemRepository.DeleteItem(albamId);
    }


    public class FolderLastIntractItemRepository : LiteDBServiceBase<FolderLastIntractItem>
    {
        public FolderLastIntractItemRepository(ILiteDatabase liteDatabase) : base(liteDatabase)
        {
        }

        public string GetLastIntractItemName(string path)
        {
            return _collection.FindById(path)?.ItemName;
        }

        public void SetLastIntractItemName(string path, string itemName)
        {
            _collection.Upsert(new FolderLastIntractItem() { Path = path, ItemName = itemName });
        }

        internal void DeleteAllUnderPath(string path)
        {
            _collection.DeleteMany(x => path.StartsWith(x.Path));
        }
    }

    public class AlbamLastIntractItemRepository : LiteDBServiceBase<AlbamLastIntractItem>
    {
        public AlbamLastIntractItemRepository(ILiteDatabase liteDatabase) : base(liteDatabase)
        {
        }

        public string GetLastIntractItemName(Guid albamId)
        {
            return _collection.FindById(albamId)?.AlbamItemPath;
        }

        public void SetLastIntractItemPath(Guid albamId, string itemPath)
        {
            _collection.Upsert(new AlbamLastIntractItem() { AlbamId = albamId, AlbamItemPath = itemPath });
        }
    }
}

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
