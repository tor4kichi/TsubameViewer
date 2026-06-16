using LiteDB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Infrastructure;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.UI.Xaml.Controls;
using ZLinq;

namespace TsubameViewer.Core.Models.FolderItemListing;

public enum FolderContainerType
{
    OnlyImages,
    Other,
}

public sealed class FolderContainerTypeManager
{
    public class FolderContainerEntry
    {
        [BsonId]
        public string Path { get; set; }

        [BsonField]
        public FolderContainerType ContainerType { get; set; }
    }

    private readonly ILiteCollection<FolderContainerEntry> _collection;

    public FolderContainerTypeManager(ILiteDatabase liteDatabase)
    {
        _collection = liteDatabase.GetCollection<FolderContainerEntry>();
    }

    public async ValueTask<FolderContainerType> GetFolderContainerTypeWithCacheAsync(StorageFolder folder, CancellationToken ct)
    {
        return GetContainerType(folder.Path)
            ?? await GetLatestFolderContainerTypeAndUpdateCacheAsync(folder, ct);
    }

    public async Task<FolderContainerType> GetLatestFolderContainerTypeAndUpdateCacheAsync(StorageFolder folder, CancellationToken ct)
    {
        FolderContainerType folderContainerType = FolderContainerType.Other;
        if (await folder.CreateItemQueryWithOptions(new QueryOptions(CommonFileQuery.DefaultQuery, 
            Enumerable.Concat(SupportedFileTypesHelper.SupportedArchiveFileExtensions, SupportedFileTypesHelper.SupportedEBookFileExtensions))
            { FolderDepth = FolderDepth.Shallow }).GetItemCountAsync().AsTask(ct) > 0
           )
        {
            folderContainerType = FolderContainerType.Other;
        }
        else if (await folder.CreateItemQueryWithOptions(new QueryOptions(CommonFileQuery.DefaultQuery,
            SupportedFileTypesHelper.SupportedImageFileExtensions)
            { FolderDepth = FolderDepth.Shallow }).GetItemCountAsync().AsTask(ct) > 0)
        {
            folderContainerType = FolderContainerType.OnlyImages;
        }
        else
        {
            // folder no items.
            folderContainerType = FolderContainerType.Other;
        }

        SetContainerType(folder.Path, folderContainerType);
        return folderContainerType;
    }


    public void Delete(string path)
    {
        _collection.Delete(path);
    }

    public void DeleteAllUnderPath(string path)
    {
        _collection.DeleteMany(x => path.StartsWith(x.Path));
    }

    internal void SetContainerType(StorageFolder folder, FolderContainerType folderContainerType)
    {
        SetContainerType(folder.Path, folderContainerType);
    }


    public void SetContainerType(string path, FolderContainerType folderContainerType)
    {
        _collection.Upsert(new FolderContainerEntry() { Path = path, ContainerType = folderContainerType });
    }

    public FolderContainerType? GetContainerType(string path)
    {
        return _collection.Exists(x => x.Path.Equals(path, StringComparison.Ordinal))
            ? _collection.FindById(path).ContainerType
            : default(FolderContainerType?)
            ;
    }

    public void PathChanged(string oldPath, string newPath)
    {
        if (string.IsNullOrEmpty(Path.GetExtension(oldPath)))
        {
            var entires = _collection.Find(x => x.Path.StartsWith(oldPath)).AsValueEnumerable().ToArrayPool();
            StringBuilder sb = new();
            foreach (var entry in entires.Span)
            {
                _collection.Delete(entry.Path);
                Debug.WriteLine($"FolderContainerType Path changing: {entry.Path}");
                sb.Clear();
                sb.Append(entry.Path);
                sb.Replace(oldPath, newPath);
                entry.Path = sb.ToString();
                _collection.Upsert(entry);
                Debug.WriteLine($"FolderContainerType Path changed: {entry.Path}");
            }
        }
        else
        {
            var entry = _collection.FindOne(x => x.Path.Equals(oldPath, StringComparison.Ordinal));
            if (entry == null) { return; }
            _collection.Delete(entry.Path);
            Debug.WriteLine($"FolderContainerType Path changing: {entry.Path}");
            entry.Path = newPath;
            _collection.Upsert(entry);
            Debug.WriteLine($"FolderContainerType Path changed: {entry.Path}");
        }
    }
}
