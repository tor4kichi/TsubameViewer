using LiteDB;
using CommunityToolkit.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using TsubameViewer.Core.Infrastructure;

namespace TsubameViewer.Core.Models.FolderItemListing;

public class FolderEntry
{
    public string FolderPath { get; init; }
    public List<string> ItemPaths { get; init; }
}



public sealed class FolderItemsCacheRepository 
{
    private readonly InternalFolderItemsCacheRepository _internalFolderItemsCacheRepository;

    public sealed class InternalFolderItemsCacheRepository : LiteDBServiceBase<FolderEntry>
    {
        public InternalFolderItemsCacheRepository(ILiteDatabase liteDatabase) : base(liteDatabase)
        {
        }

        public FolderEntry FindById(string path)
        {
            return _collection.FindById(path);
        }
    }

    public FolderItemsCacheRepository(InternalFolderItemsCacheRepository  internalFolderItemsCacheRepository) 
    {
        _internalFolderItemsCacheRepository = internalFolderItemsCacheRepository;
    }


    public void AddOrUpdateItem(string folderPath, IEnumerable<string> paths)
    {
        AddOrUpdateItem(new FolderEntry() { FolderPath = folderPath, ItemPaths = paths.ToList() });
    }

    public void AddOrUpdateItem(FolderEntry entry)
    {
        Guard.IsNotNullOrEmpty(entry.FolderPath, nameof(entry.FolderPath));
        Guard.IsNotNull(entry.ItemPaths, nameof(entry.ItemPaths));
        _internalFolderItemsCacheRepository.UpdateItem(entry);
    }


    public FolderEntry GetItem(string path)
    {
        return _internalFolderItemsCacheRepository.FindById(path);
    }

    public void DeleteItem(string folderPath)
    {
        Queue<string> queue = new();
        queue.Enqueue(folderPath);

        while (queue.TryDequeue(out string path))
        {
            var item = _internalFolderItemsCacheRepository.FindById(path);
            if (item is null) { continue; }

            foreach (var childPath in item.ItemPaths)
            {
                queue.Enqueue(childPath);
            }

            _internalFolderItemsCacheRepository.DeleteItem(path);
        }
    }
}
