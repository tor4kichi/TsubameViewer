using CommunityToolkit.Diagnostics;
using LiteDB;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using SharpCompress.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Contracts.Services;
using TsubameViewer.Core.Helpers;
using TsubameViewer.Core.Infrastructure;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using ZLinq;
#nullable enable
namespace TsubameViewer.Core.Models.ImageViewer;

public interface IImageCollectionContext
{
    string Name { get; }

    ValueTask<bool> IsExistImageFileAsync(CancellationToken ct);
    ValueTask<bool> IsExistFolderOrArchiveFileAsync(CancellationToken ct);
    IAsyncEnumerable<IImageSource> GetAllImageFilesAsync(CancellationToken ct);

    IAsyncEnumerable<IImageSource> GetImageFilesAsync(CancellationToken ct);

    ValueTask<int> GetImageFileCountAsync(CancellationToken ct);
    ValueTask<IImageSource> GetImageFileAtAsync(int index, FileSortType sort, CancellationToken ct);
    ValueTask<int> GetImageFileIndexFromKeyAsync(string key, FileSortType sort, CancellationToken ct);

    bool IsSupportFolderOrArchiveFilesIndexAccess { get; }
    ValueTask<int> GetFolderOrArchiveFilesCountAsync(CancellationToken ct);
    ValueTask<IImageSource> GetFolderOrArchiveFileAtAsync(int index, FileSortType sort, CancellationToken ct);
    ValueTask<int> GetFolderOrArchiveFilesIndexFromKeyAsync(string key, FileSortType sort, CancellationToken ct);
    IAsyncEnumerable<IImageSource> GetFolderOrArchiveFilesAsync(CancellationToken ct);
    IAsyncEnumerable<IImageSource> GetLeafFoldersAsync(CancellationToken ct);

    bool IsSupportedFolderContentsChanged { get; }

    IObservable<Unit> CreateFolderAndArchiveFileChangedObserver();
    IObservable<Unit> CreateImageFileChangedObserver();
}



public sealed class FolderImageCollectionContext : IImageCollectionContext
{
    public static readonly QueryOptions DefaultImageFileSearchQueryOptions = CreateDefaultImageFileSearchQueryOptions(FileSortType.None);
    public static readonly QueryOptions FoldersAndArchiveFileSearchQueryOptions = CreateDefaultFolderOrArchiveFilesSearchQueryOptions(FileSortType.None);

    private StorageItemQueryResult? _folderAndArchiveFileSearchQuery;
    private StorageItemQueryResult FolderAndArchiveFileSearchQuery => _folderAndArchiveFileSearchQuery ??= Folder.CreateItemQueryWithOptions(FoldersAndArchiveFileSearchQueryOptions);

    private StorageFileQueryResult? _imageFileSearchQuery;
    private StorageFileQueryResult ImageFileSearchQuery => _imageFileSearchQuery ??= Folder.CreateFileQueryWithOptions(DefaultImageFileSearchQueryOptions);

    public string Name => Folder?.Name ?? "";

    static FolderStructureFilesRepository? _cacheRepo;

    public FolderImageCollectionContext(StorageFolder storageFolder)
    {
        Folder = storageFolder;
        _cacheRepo ??= new(new LiteDatabase(new ConnectionString() { Filename = Path.Combine(ApplicationData.Current.TemporaryFolder.Path, "folder_structure.litedb") }));
        Context = new FolderStructureCacheContext(Folder, _cacheRepo);                
    }

    public StorageFolder Folder { get; }

    public FolderStructureCacheContext Context { get; }



    public bool IsSupportFolderOrArchiveFilesIndexAccess => true;

    private FileSortType _lastFolderAndArchiveFilesSortType;
    public async ValueTask<int> GetFolderOrArchiveFilesCountAsync(CancellationToken ct)
    {
        return (int)await FolderAndArchiveFileSearchQuery.GetItemCountAsync().AsTask(ct);
    }

    int _prevAccessIndex = -1;
    int _cachedPage = -1;
    IStorageItem?[] _cachedPageItems = new IStorageItem[100];

    AsyncLock _lock = new AsyncLock();
    public async ValueTask<IImageSource> GetFolderOrArchiveFileAtAsync(int index, FileSortType sort, CancellationToken ct)
    {
        using var _ = await _lock.LockAsync(ct);

        if (_lastFolderAndArchiveFilesSortType != sort)
        {
            _lastFolderAndArchiveFilesSortType = sort;
            FolderAndArchiveFileSearchQuery.ApplyNewQueryOptions(GetFolderOrArchiveFilesSortQueryOptions(sort));
            _prevAccessIndex = -1;
            _cachedPage = -1;
        }

        if (_prevAccessIndex == -1 || _prevAccessIndex + 1 == index)
        {
            _prevAccessIndex = index;
            int checkPage = index / _cachedPageItems.Length;
            if (checkPage != _cachedPage)
            {
                _cachedPage = checkPage;
                var items = await FolderAndArchiveFileSearchQuery.GetItemsAsync((uint)(checkPage * _cachedPageItems.Length), (uint)_cachedPageItems.Length).AsTask(ct);
                for (int i = 0; i < items.Count; i++)
                {
                    _cachedPageItems[i] = items[i];
                }

                for (int i = items.Count; i < _cachedPageItems.Length; i++)
                {
                    _cachedPageItems[i] = null;
                }
                
                Debug.WriteLine($"update page {_cachedPage}");
            }

            var imageSource = _cachedPageItems[index - checkPage * _cachedPageItems.Length];
            Guard.IsNotNull(imageSource);
            Debug.WriteLine($"index:{index}, Name:{imageSource.Name}");
            return new StorageItemImageSource(imageSource);
        }
        else
        {
            if (await FolderAndArchiveFileSearchQuery.GetItemsAsync((uint)index, 1).AsTask(ct) is not null and var files
                && files.ElementAtOrDefault(0) is not null and var imageSource)
            {
                Debug.WriteLine($"index:{index}, Name:{imageSource.Name}");
                return new StorageItemImageSource(imageSource);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index out of range.");
            }
        }
    }

    public async ValueTask<int> GetFolderOrArchiveFilesIndexFromKeyAsync(string key, FileSortType sort, CancellationToken ct)
    {
        if (_lastFolderAndArchiveFilesSortType != sort)
        {
            _lastFolderAndArchiveFilesSortType = sort;
            FolderAndArchiveFileSearchQuery.ApplyNewQueryOptions(GetFolderOrArchiveFilesSortQueryOptions(sort));
        }

        if (sort is FileSortType.None or FileSortType.TitleAscending or FileSortType.TitleDecending)
        {
            string filename = Path.GetFileName(key);
            uint result = await FolderAndArchiveFileSearchQuery.FindStartIndexAsync(filename);
            return result != uint.MaxValue ? (int)result : throw new KeyNotFoundException($"not found file : {filename}");
        }
        else
        {
            // FindStartIndexAsync が意図したIndexを返さないので頭から走査する
            int index = 0;
            await foreach (var file in FolderAndArchiveFileSearchQuery.ToAsyncEnumerable(ct).WithCancellation(ct))
            {
                if (file.Name == key || file.Path == key)
                {
                    return index;
                }

                index++;
            }

            throw new KeyNotFoundException($"not found file : {Path.GetFileName(key)}");
        }
    }




    // see@ https://docs.microsoft.com/en-us/uwp/api/windows.storage.search.queryoptions.sortorder?view=winrt-22000#remarks
    public static QueryOptions CreateDefaultFolderOrArchiveFilesSearchQueryOptions(FileSortType sort)
    {
        var query = new QueryOptions(CommonFileQuery.DefaultQuery,
            [
                .. SupportedFileTypesHelper.SupportedArchiveFileExtensions,
                .. SupportedFileTypesHelper.SupportedEBookFileExtensions,
                .. SupportedFileTypesHelper.SupportedMovieFileExtensions                
            ])
        {
            FolderDepth = FolderDepth.Shallow,
        };
        query.SortOrder.Clear();
        switch (sort)
        {
            case FileSortType.None:
                query.SortOrder.Add(new SortEntry() { PropertyName = "System.ItemNameDisplay", AscendingOrder = true });
                break;
            case FileSortType.TitleAscending:
                query.SortOrder.Add(new SortEntry() { PropertyName = "System.ItemNameDisplay", AscendingOrder = true });
                break;
            case FileSortType.TitleDecending:
                query.SortOrder.Add(new SortEntry() { PropertyName = "System.ItemNameDisplay", AscendingOrder = false });
                break;
            case FileSortType.UpdateTimeAscending:
                query.SortOrder.Add(new SortEntry() { PropertyName = "System.DateModified", AscendingOrder = true });
                break;
            case FileSortType.UpdateTimeDecending:
                query.SortOrder.Add(new SortEntry() { PropertyName = "System.DateModified", AscendingOrder = false });
                break;
        }

        return query;
    }


    private readonly Dictionary<FileSortType, QueryOptions> _folderOrArchiveFilesSortTypeToQueryOptions = new()
    {
        { FileSortType.None, DefaultImageFileSearchQueryOptions }
    };

    private QueryOptions GetFolderOrArchiveFilesSortQueryOptions(FileSortType sort)
    {
        // Note: キャッシュして使い回すとGetItemsCountAsync()等でハングアップするので都度生成している。
        return CreateDefaultFolderOrArchiveFilesSearchQueryOptions(sort);
        //return _folderOrArchiveFilesSortTypeToQueryOptions.TryGetValue(sort, out var queryOptions)
        //    ? queryOptions
        //    : _folderOrArchiveFilesSortTypeToQueryOptions[sort] = CreateDefaultFolderOrArchiveFilesSearchQueryOptions(sort);
    }


    public IAsyncEnumerable<IImageSource> GetFolderOrArchiveFilesAsync(CancellationToken ct)
    {
        return FolderAndArchiveFileSearchQuery.ToAsyncEnumerable(ct)
            .Select(x => new StorageItemImageSource(x) as IImageSource);
    }

    public IAsyncEnumerable<IImageSource> GetLeafFoldersAsync(CancellationToken ct)
    {
        return AsyncEnumerable.Empty<IImageSource>();
    }

    public IAsyncEnumerable<IImageSource> GetAllImageFilesAsync(CancellationToken ct)
    {        
        return GetImageFilesAsync(ct);
    }

    public IAsyncEnumerable<IImageSource> GetImageFilesAsync(CancellationToken ct)
    {        
        return ImageFileSearchQuery.ToAsyncEnumerable(ct)
            .Select(x => new StorageItemImageSource(x) as IImageSource);
    }

    public async ValueTask<bool> IsExistFolderOrArchiveFileAsync(CancellationToken ct)
    {
        var count = await FolderAndArchiveFileSearchQuery.GetItemCountAsync().AsTask(ct);
        return count > 0;
    }

    public async ValueTask<bool> IsExistImageFileAsync(CancellationToken ct)
    {
        var count = await ImageFileSearchQuery.GetItemCountAsync().AsTask(ct);
        return count > 0;
    }

    public async ValueTask<int> GetImageFileCountAsync(CancellationToken ct)
    {
        await Context.UpdateImagesCacheIfCountNotSameAsync(ct);
        return Context.GetCachedImagesCount();
        //return (int)await ImageFileSearchQuery.GetItemCountAsync().AsTask(ct); ;
    }

    public async ValueTask<IImageSource> GetImageFileAtAsync(int index, FileSortType sort, CancellationToken ct)
    {
        if (Context.GetEntryFromIndex(index, sort) is not { } entry
            || await Folder.GetFileAsync(entry.Name) is not { } file)
        {
            Context.ForceUpdateRequestForImages();
            await Context.UpdateImagesCacheIfCountNotSameAsync(ct);
            if (Context.GetEntryFromIndex(index, sort) is not { } altEntry
                || await Folder.GetFileAsync(altEntry.Name) is not { } altFile)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index out of range.");
            }
            return new StorageItemImageSource(altFile);
        }

        return new StorageItemImageSource(file);
    }

    // see@ https://docs.microsoft.com/en-us/uwp/api/windows.storage.search.queryoptions.sortorder?view=winrt-22000#remarks
    public static QueryOptions CreateDefaultImageFileSearchQueryOptions(FileSortType sort)
    {
        var query = new QueryOptions(CommonFileQuery.DefaultQuery, SupportedFileTypesHelper.SupportedImageFileExtensions)
        {
            FolderDepth = FolderDepth.Shallow,            
        };
        query.SortOrder.Clear();
        switch (sort)
        {
            case FileSortType.None:
                query.SortOrder.Add(new SortEntry() { PropertyName = "System.ItemNameDisplay", AscendingOrder = true });
                break;
            case FileSortType.TitleAscending:
                query.SortOrder.Add(new SortEntry() { PropertyName = "System.ItemNameDisplay", AscendingOrder = true });
                break;
            case FileSortType.TitleDecending:
                query.SortOrder.Add(new SortEntry() { PropertyName = "System.ItemNameDisplay", AscendingOrder = false });
                break;
            case FileSortType.UpdateTimeAscending:
                query.SortOrder.Add(new SortEntry() { PropertyName = "System.DateImported", AscendingOrder = true });
                break;
            case FileSortType.UpdateTimeDecending:
                query.SortOrder.Add(new SortEntry() { PropertyName = "System.DateImported", AscendingOrder = false });
                break;
        }

        return query;
    }


    private readonly Dictionary<FileSortType, QueryOptions> _filesSortTypeToQueryOptions = new()
    {
        { FileSortType.None, DefaultImageFileSearchQueryOptions },
    };

    private QueryOptions GetImageFileSortQueryOptions(FileSortType sort)
    {
        return _filesSortTypeToQueryOptions.TryGetValue(sort, out var queryOptions)
            ? queryOptions
            : _filesSortTypeToQueryOptions[sort] = CreateDefaultImageFileSearchQueryOptions(sort);
    }


    public async ValueTask<int> GetImageFileIndexFromKeyAsync(string key, FileSortType sort, CancellationToken ct)
    {
        //await Context.UpdateImagesCacheIfCountNotSameAsync(ct);

        //if (_lastFileSortType != sort)
        //{
        //    _lastFileSortType = sort;
        //    _imageFileSearchQuery.ApplyNewQueryOptions(GetImageFileSortQueryOptions(sort));
        //}

        return Context.GetIndexFromKey(key, sort);

        //string filename = Path.GetFileName(key);
        //uint result = await ImageFileSearchQuery.FindStartIndexAsync(filename);
        //if (result != uint.MaxValue) { return (int)result; }

        //// FindStartIndexAsync が意図したIndexを返さないので頭から走査する
        //int index = 0;
        //await foreach (var file in ImageFileSearchQuery.ToAsyncEnumerable(ct))
        //{
        //    if (file.Name == key || file.Path == key)
        //    {
        //        return index;
        //    }

        //    index++;
        //}

        //throw new KeyNotFoundException($"not found file : {Path.GetFileName(key)}");
    }

    public bool IsSupportedFolderContentsChanged => true;

   
    public IObservable<Unit> CreateFolderAndArchiveFileChangedObserver()
    {
        return Observable.Create<Unit>(observer =>
        {
            void FolderAndArchiveFileSearchQuery_ContentsChanged(IStorageQueryResultBase sender, object args)
            {
                observer.OnNext(Unit.Default);
            }
            FolderAndArchiveFileSearchQuery.ContentsChanged += FolderAndArchiveFileSearchQuery_ContentsChanged;
            return Disposable.Create(() => FolderAndArchiveFileSearchQuery.ContentsChanged -= FolderAndArchiveFileSearchQuery_ContentsChanged);
        })
            .Throttle(TimeSpan.FromSeconds(1));
    }

    

    public IObservable<Unit> CreateImageFileChangedObserver()
    {
        return Observable.Create<Unit>(observer =>
        {
            void FolderAndArchiveFileSearchQuery_ContentsChanged(IStorageQueryResultBase sender, object args)
            {                
                observer.OnNext(Unit.Default);
            }
            ImageFileSearchQuery.ContentsChanged += FolderAndArchiveFileSearchQuery_ContentsChanged;
            return Disposable.Create(() => ImageFileSearchQuery.ContentsChanged -= FolderAndArchiveFileSearchQuery_ContentsChanged);
        })
            .Throttle(TimeSpan.FromSeconds(1));
    }

}


public class FolderCacheUpdateInfo
{
    public bool IsRequireUpdate { get; set; }
    public int? CachedImagesCount { get; set; }
    public int? CachedNotImagesCount { get; set; }
}

public sealed class FolderStructureCacheContext : IDisposable
{
    public FolderStructureCacheContext(StorageFolder folder, FolderStructureFilesRepository cacheFilesRepository)
    {
        Folder = folder;
        _repo = cacheFilesRepository;
        var query = Folder.CreateFileQuery();
        query.ContentsChanged += Query_ContentsChanged;

        if (!_updateMap.ContainsKey(Folder.Path))
        {
            _updateMap[Folder.Path] = new FolderCacheUpdateInfo { IsRequireUpdate = true };
        }
    }

    readonly static Dictionary<string, FolderCacheUpdateInfo> _updateMap = [];
    
    private void Query_ContentsChanged(IStorageQueryResultBase sender, object args)
    {
        Debug.WriteLine($"{Folder.Name} contents changed!");
        _updateMap[Folder.Path]?.IsRequireUpdate = true;
    }

    public StorageFolder Folder { get; }
    private readonly FolderStructureFilesRepository _repo;

    public bool HasImagesCache()
    {
        return _repo.HasFolderImages(Folder);
    }

    public IEnumerable<FolderStructureFileEntry> GetCacheImages()
    {
        return _repo.FindFolderImages(Folder.Path);
    }

    public bool HasNotImagesCache()
    {
        return _repo.HasFolderNotImages(Folder);
    }

    public IEnumerable<FolderStructureFileEntry> GetCacheNotImages()
    {
        return _repo.FindFolderNotImages(Folder.Path);
    }

    public FolderStructureFileEntry? GetEntryFromIndex(int index, FileSortType sort)
    {
        var folderItems = _repo.FindFolderImages(Folder.Path);
        return sort switch
        {
            FileSortType.None => folderItems.AsValueEnumerable().OrderBy(x => x.DateCreated).ElementAtOrDefault(index),
            FileSortType.TitleAscending => folderItems.AsValueEnumerable().OrderBy(x => x.Name).ElementAtOrDefault(index),
            FileSortType.TitleDecending => folderItems.AsValueEnumerable().OrderByDescending(x => x.Name).ElementAtOrDefault(index),
            FileSortType.UpdateTimeAscending => folderItems.AsValueEnumerable().OrderBy(x => x.DateCreated).ElementAtOrDefault(index),
            FileSortType.UpdateTimeDecending => folderItems.AsValueEnumerable().OrderByDescending(x => x.DateCreated).ElementAtOrDefault(index),
            _ => throw new InvalidOperationException(),
        };
    }

    public int GetIndexFromKey(string key, FileSortType sort)
    {
        var folderItems = _repo.FindFolderImages(Folder.Path);
        return sort switch
        {
            FileSortType.None => folderItems.AsValueEnumerable().OrderBy(x => x.Name).Index().FirstOrDefault(x => x.Item.Name == key).Index,
            FileSortType.TitleAscending => folderItems.AsValueEnumerable().OrderBy(x => x.Name).Index().FirstOrDefault(x => x.Item.Name == key).Index,
            FileSortType.TitleDecending => folderItems.AsValueEnumerable().OrderByDescending(x => x.Name).Index().FirstOrDefault(x => x.Item.Name == key).Index,
            FileSortType.UpdateTimeAscending => folderItems.AsValueEnumerable().OrderBy(x => x.DateCreated).Index().FirstOrDefault(x => x.Item.Name == key).Index,
            FileSortType.UpdateTimeDecending => folderItems.AsValueEnumerable().OrderByDescending(x => x.DateCreated).Index().FirstOrDefault(x => x.Item.Name == key).Index,
            _ => throw new InvalidOperationException(),
        };
    }


    readonly static Core.AsyncLock _asyncLock = new();
    public async Task HandleDiffImages<T>(ObservableCollection<T> items,
        Func<IDisposable> deferRefreshFactory,
        Func<FolderStructureFileEntry, StorageFile, T> cacheImageViewModelFactory,
        Func<T, string> itemToPathConv,
        CancellationToken ct)
    {
        using var reelaser = await _asyncLock.LockAsync(ct);
        _updateMap[Folder.Path].IsRequireUpdate = false;
        var query = Folder.CreateFileQueryWithOptions(FolderImageCollectionContext.CreateDefaultImageFileSearchQueryOptions(FileSortType.None));
        int imagesCount = (int)await query.GetItemCountAsync().AsTask(ct);
        // キャッシュされたアイテムとの差分を求めてその結果からitemsからアイテムを差し引きする
        var cached = _repo.FindFolderImages(Folder.Path).ToDictionary(x => x.Path);
        bool isInitial = !_repo.HasFolderImages(Folder);
        // filesにあるアイテムがcachedに無い → 増分
        IDisposable deferRefresh = deferRefreshFactory();
        int count = 200;
        await foreach (var file in query.ToAsyncEnumerable(ct).WithCancellation(ct))
        {            
            if (!cached.Remove(file.Path, out var entry) || isInitial)
            {
                ct.ThrowIfCancellationRequested();
                entry = _repo.AddOrUpdateItem(file);
                var itemVM = cacheImageViewModelFactory(entry, file);
                items.Add(itemVM);
            }
            else { continue; }

            if (count-- <= 0)
            {
                count = 200;
                deferRefresh.Dispose();
                deferRefresh = deferRefreshFactory();
            }
        }

        _updateMap[Folder.Path].CachedImagesCount = imagesCount;
        deferRefresh.Dispose();
        deferRefresh = deferRefreshFactory();

        // cachedにあってfilesに無い → 減分
        foreach (var (i, item) in items.AsValueEnumerable().Index().Reverse())
        {
            if (cached.TryGetValue(itemToPathConv(item), out var entry))
            {
                items.RemoveAt(i);
                _repo.FileRemoved(entry);
            }
        }

        deferRefresh.Dispose();
    }

    public async Task HandleDiffNotImages<T>(ObservableCollection<T> items,
       Func<IDisposable> deferRefreshFactory,
       Func<FolderStructureFileEntry, IStorageItem, T> cacheImageViewModelFactory,
       Func<T, string> itemToPathConv,
       CancellationToken ct)
    {
        using var reelaser = await _asyncLock.LockAsync(ct);
        _updateMap[Folder.Path].IsRequireUpdate = false;
        var query = Folder.CreateItemQueryWithOptions(FolderImageCollectionContext.CreateDefaultFolderOrArchiveFilesSearchQueryOptions(FileSortType.None));
        int imagesCount = (int)await query.GetItemCountAsync().AsTask(ct);
        // キャッシュされたアイテムとの差分を求めてその結果からitemsからアイテムを差し引きする
        var cached = _repo.FindFolderNotImages(Folder.Path).ToDictionary(x => x.Path);
        bool isInitial = !_repo.HasFolderNotImages(Folder);
        // filesにあるアイテムがcachedに無い → 増分
        IDisposable deferRefresh = deferRefreshFactory();
        int count = 200;
        await foreach (var file in query.ToAsyncEnumerable(ct).WithCancellation(ct))
        {
            if (!cached.Remove(file.Path, out var entry) || isInitial)
            {
                ct.ThrowIfCancellationRequested();
                entry = _repo.AddOrUpdateItem(file);
                var itemVM = cacheImageViewModelFactory(entry, file);
                items.Add(itemVM);
            }
            else { continue; }

            if (count-- <= 0)
            {
                count = 200;
                deferRefresh.Dispose();
                deferRefresh = deferRefreshFactory();
            }
        }

        _updateMap[Folder.Path].CachedNotImagesCount = imagesCount;
        deferRefresh.Dispose();
        deferRefresh = deferRefreshFactory();

        // cachedにあってfilesに無い → 減分
        foreach (var (i, item) in items.AsValueEnumerable().Index().Reverse())
        {
            if (cached.TryGetValue(itemToPathConv(item), out var entry))
            {
                items.RemoveAt(i);
                _repo.FolderRemoved(entry.Path);
            }
        }

        deferRefresh.Dispose();
    }


    public int GetCachedImagesCount()
    {
        var cacheInfo = _updateMap[Folder.Path];
        if (cacheInfo.CachedImagesCount.HasValue)
        {
            return cacheInfo.CachedImagesCount.Value;
        }
        else
        {
            var count = _repo.GetFolderImagesCount(Folder.Path);
            cacheInfo.CachedImagesCount = count;
            return count;
        }
    }

    public int GetCachedNotImagesCount()
    {
        var cacheInfo = _updateMap[Folder.Path];
        if (cacheInfo.CachedNotImagesCount.HasValue)
        {
            return cacheInfo.CachedNotImagesCount.Value;
        }
        else
        {
            var count = _repo.GetFolderNotImagesCount(Folder.Path);
            cacheInfo.CachedNotImagesCount = count;
            return count;
        }
    }

    public async Task<bool> CheckIsNotSameImagesCacheCountAndExactCountAsync(CancellationToken ct)
    {
        var query = Folder.CreateFileQueryWithOptions(FolderImageCollectionContext.CreateDefaultImageFileSearchQueryOptions(FileSortType.None));
        var getCountTask = query.GetItemCountAsync().AsTask(ct);
        var cachedCount = GetCachedImagesCount();
        return cachedCount != (_updateMap[Folder.Path].CachedImagesCount = (int)await getCountTask);
    }

    public async Task<bool> CheckIsNotSameNotImagesCacheCountAndExactCountAsync(CancellationToken ct)
    {
        var query = Folder.CreateItemQueryWithOptions(FolderImageCollectionContext.CreateDefaultFolderOrArchiveFilesSearchQueryOptions(FileSortType.None));
        var getCountTask = query.GetItemCountAsync().AsTask(ct);
        var cachedCount = GetCachedNotImagesCount();
        return cachedCount != (_updateMap[Folder.Path].CachedNotImagesCount = (int)await getCountTask);
    }


    public async Task<bool> UpdateImagesCacheIfCountNotSameAsync(CancellationToken ct)
    {
        using var reelaser = await _asyncLock.LockAsync(ct);

        StorageFileQueryResult query;
        var cacheInfo = _updateMap[Folder.Path];
        if (cacheInfo.IsRequireUpdate)
        {
            query = Folder.CreateFileQueryWithOptions(FolderImageCollectionContext.CreateDefaultImageFileSearchQueryOptions(FileSortType.None));
            cacheInfo.IsRequireUpdate = false;
            var cachedCount = GetCachedImagesCount();
            if (cachedCount == (cacheInfo.CachedImagesCount = (int)await query.GetItemCountAsync().AsTask(ct)))
            {
                Debug.WriteLine($"{Folder.Name} SKIP structure cache update. but GetItemCountAsync called");
                return false;
            }
        }
        else 
        {
            Debug.WriteLine($"{Folder.Name} SKIP structure cache update.");
            return false; 
        }

        Debug.WriteLine($"{Folder.Name} START structure cache update.");
        _repo.FolderRemoved(Folder.Path);
        uint currentCount = 0;
        while (await query.GetFilesAsync(currentCount, 500).AsTask(ct) is not null and var items && items.Any())
        {
            _repo.BulkInsert(items);
            ct.ThrowIfCancellationRequested();
            currentCount += (uint)items.Count;
        }

        Debug.WriteLine($"{Folder.Name} COMPLETE structure cache update.");
        return true;
    }

    public void ForceUpdateRequestForNotImages()
    {
        if (_updateMap.TryGetValue(Folder.Path,  out var info))
        {
            info.IsRequireUpdate = true;
            info.CachedNotImagesCount = 0;            
        }
    }
    public void ForceUpdateRequestForImages()
    {
        if (_updateMap.TryGetValue(Folder.Path, out var info))
        {
            info.IsRequireUpdate = true;
            info.CachedImagesCount = 0;
        }
    }


    public async Task<bool> UpdateNotImagesCacheIfCountNotSameAsync(CancellationToken ct)
    {
        using var reelaser = await _asyncLock.LockAsync(ct);

        StorageItemQueryResult query;
        var cacheInfo = _updateMap[Folder.Path];
        if (cacheInfo.IsRequireUpdate)
        {
            query = Folder.CreateItemQueryWithOptions(FolderImageCollectionContext.CreateDefaultFolderOrArchiveFilesSearchQueryOptions(FileSortType.None));
            cacheInfo.IsRequireUpdate = false;
            var cachedCount = GetCachedNotImagesCount();
            if (cachedCount == (cacheInfo.CachedNotImagesCount = (int)await query.GetItemCountAsync().AsTask(ct)))
            {
                Debug.WriteLine($"{Folder.Name} SKIP structure cache update. but GetItemCountAsync called");
                return false;
            }
        }
        else
        {
            Debug.WriteLine($"{Folder.Name} SKIP structure cache update.");
            return false;
        }

        Debug.WriteLine($"{Folder.Name} START structure cache update.");
        _repo.FolderRemoved(Folder.Path);
        uint currentCount = 0;
        while (await query.GetItemsAsync(currentCount, 500).AsTask(ct) is not null and var items && items.Any())
        {
            _repo.BulkInsert(items);
            ct.ThrowIfCancellationRequested();
            currentCount += (uint)items.Count;
        }

        Debug.WriteLine($"{Folder.Name} COMPLETE structure cache update.");
        return true;
    }

    public void Dispose()
    {
        _repo.Dispose();
    }
}

public sealed class FolderStructureFileEntry
{
    [BsonId]
    public string Path { get; set; } = "";

    string? _parentFolderPath;
    public string ParentFolderPath => _parentFolderPath ??= System.IO.Path.GetDirectoryName(Path);

    public ulong ParentFolderPathHash { get; set; } = 0;

    string? _fileName;
    public string Name => _fileName ??= System.IO.Path.GetFileName(Path);

    public DateTimeOffset DateCreated { get; set; }

    public bool IsImage { get; set; } = true;
}

public sealed class FolderStructureFilesRepository : IDisposable
{
    private readonly ILiteCollection<FolderStructureFileEntry> _collection;
    private readonly ILiteDatabase _tempLiteDatabase;

    public sealed class InternalFolderItemsCacheRepository : LiteDBServiceBase<FolderStructureFileEntry>
    {
        public InternalFolderItemsCacheRepository(ILiteDatabase liteDatabase) : base(liteDatabase)
        {
        }

        public FolderStructureFileEntry FindById(string path)
        {
            return _collection.FindById(path);
        }
    }
    
    public FolderStructureFilesRepository(ILiteDatabase tempLiteDatabase)
    {
        _collection = tempLiteDatabase.GetCollection<FolderStructureFileEntry>();        
        _collection.EnsureIndex(x => x.Name);
        _collection.EnsureIndex(x => x.DateCreated);
        _collection.EnsureIndex(x => x.IsImage);        
        if (_collection.EnsureIndex(x => x.ParentFolderPathHash))
        {
            foreach (var item in _collection.Query().ForUpdate().ToEnumerable())
            {
                item.ParentFolderPathHash = HashHelper.CalculateFNV1a64(System.IO.Path.GetDirectoryName(item.Path));
                _collection.Update(item);
            }
        }
        _tempLiteDatabase = tempLiteDatabase;
    }

    public bool HasFolderImages(StorageFolder folder)
    {
        var hash = HashHelper.CalculateFNV1a64(folder.Path);
        return _collection.Exists(x => x.IsImage && x.ParentFolderPathHash == hash);
    }

    public bool HasFolderNotImages(StorageFolder folder)
    {
        var hash = HashHelper.CalculateFNV1a64(folder.Path);
        return _collection.Exists(x => !x.IsImage && x.ParentFolderPathHash == hash);
    }

    public FolderStructureFileEntry AddOrUpdateItem(IStorageItem file)
    {
        var entry = new FolderStructureFileEntry()
        {
            Path = file.Path,
            DateCreated = file.DateCreated,
            IsImage = file is StorageFile f ? f.IsSupportedImageFile() : false,
            ParentFolderPathHash = HashHelper.CalculateFNV1a64(Path.GetDirectoryName(file.Path))
        };
        _collection.Upsert(entry);
        ClearCache();
        return entry;
    }

    internal void BulkInsert(IReadOnlyList<IStorageItem> items)
    {
        _collection.InsertBulk(items.Select(file => new FolderStructureFileEntry()
        {
            Path = file.Path,
            DateCreated = file.DateCreated,
            IsImage = file is StorageFile f ? f.IsSupportedImageFile() : false,
            ParentFolderPathHash = HashHelper.CalculateFNV1a64(Path.GetDirectoryName(file.Path))
        }));
        ClearCache();
    }


    void ClearCache()
    {
        _folderImagesCache?.Dispose();
        _folderImagesCache = null;

        _folderNotImagesCache?.Dispose();
        _folderNotImagesCache = null;
    }
    PooledArray<FolderStructureFileEntry>? _folderImagesCache;
    ulong? _cachedImagesfolderPathHash;
    public IEnumerable<FolderStructureFileEntry> FindFolderImages(string folderPath)
    {
        var hash = HashHelper.CalculateFNV1a64(folderPath);
        if (_folderImagesCache == null || _cachedImagesfolderPathHash == null || _cachedImagesfolderPathHash != hash)
        {
            _folderImagesCache?.Dispose();
            _folderImagesCache = _collection.Find(x => x.IsImage && x.ParentFolderPathHash == hash).AsValueEnumerable().ToArrayPool();
            _cachedImagesfolderPathHash = hash;
        }

        return _folderImagesCache.Value.ArraySegment;
    }


    PooledArray<FolderStructureFileEntry>? _folderNotImagesCache;
    ulong? _cachedNotImagesfolderPathHash;
    public IEnumerable<FolderStructureFileEntry> FindFolderNotImages(string folderPath)
    {
        var hash = HashHelper.CalculateFNV1a64(folderPath);
        if (_folderNotImagesCache == null || _cachedNotImagesfolderPathHash == null || _cachedNotImagesfolderPathHash != hash)
        {
            _folderNotImagesCache?.Dispose();
            _folderNotImagesCache = _collection.Find(x => !x.IsImage && x.ParentFolderPathHash == hash).AsValueEnumerable().ToArrayPool();
            _cachedNotImagesfolderPathHash = hash;
        }

        return _folderNotImagesCache.Value.ArraySegment;
    }

    public int GetFolderImagesCount(string folderPath)
    {
        FindFolderImages(folderPath);
        return _folderImagesCache!.Value.Size;
    }

    public int GetFolderNotImagesCount(string folderPath)
    {
        FindFolderNotImages(folderPath);
        return _folderNotImagesCache!.Value.Size;
    }

    public void FolderRemoved(string folderPath)
    {
        _collection.DeleteMany(x => folderPath.StartsWith(x.ParentFolderPath, StringComparison.Ordinal));
        ClearCache();
    }
    public void FileRemoved(FolderStructureFileEntry entry)
    {
        _collection.Delete(entry.Path);
        ClearCache();
    }

    public void FileRemoved(string path)
    {
        _collection.Delete(path);
        ClearCache();
    }

    public void Dispose()
    {
        ClearCache();
        _tempLiteDatabase.Dispose();
    }


    public void PathChanged(string oldPath, string newPath)
    {
        if (string.IsNullOrEmpty(Path.GetExtension(oldPath)))
        {
            using var entries = _collection.Find(x => x.ParentFolderPath.StartsWith(oldPath, StringComparison.Ordinal)).AsValueEnumerable().ToArrayPool();
            StringBuilder sb = new();
            foreach (var entry in entries.Span)
            {
                _collection.Delete(entry.Path);
                Debug.WriteLine($"ImageList Path changing: {entry.Path}");
                sb.Clear();
                sb.Append(entry.Path);
                sb.Replace(oldPath, newPath);
                entry.Path = sb.ToString();
                entry.ParentFolderPathHash = HashHelper.CalculateFNV1a64(System.IO.Path.GetDirectoryName(entry.Path));
                _collection.Upsert(entry);
                Debug.WriteLine($"ImageList Path changed: {entry.Path}");
            }
        }
        else
        {
            // FindByIdだと ドライブレターに使われる : によって例外が生じる
            var entry = _collection.FindOne(x => x.Path.Equals(oldPath, StringComparison.Ordinal));
            if (entry == null) { return; }
            _collection.Delete(entry.Path);
            Debug.WriteLine($"ImageList Path changing: {entry.Path}");
            entry.Path = newPath;
            entry.ParentFolderPathHash = HashHelper.CalculateFNV1a64(System.IO.Path.GetDirectoryName(entry.Path));
            _collection.Upsert(entry);
            Debug.WriteLine($"ImageList Path changed: {entry.Path}");
        }

        ClearCache();
    }
}



public sealed class ArchiveImageCollectionContext : IImageCollectionContext, IDisposable
{
    public ArchiveImageCollection ArchiveImageCollection { get; }
    public ArchiveDirectoryToken ArchiveDirectoryToken { get; }

    public string Name => ArchiveImageCollection.Name;

    public StorageFile File => ArchiveImageCollection.File;

    public ArchiveImageCollectionContext(ArchiveImageCollection archiveImageCollection, ArchiveDirectoryToken archiveDirectoryToken)
    {
        ArchiveImageCollection = archiveImageCollection;
        ArchiveDirectoryToken = archiveDirectoryToken;
    }

    public IAsyncEnumerable<IImageSource> GetFolderOrArchiveFilesAsync(CancellationToken ct)
    {
        // アーカイブファイルは内部にフォルダ構造を持っている可能性がある
        // アーカイブ内のアーカイブは対応しない
        return ArchiveImageCollection.GetSubDirectories(ArchiveDirectoryToken)
            .Select(x => (IImageSource)new ArchiveDirectoryImageSource(ArchiveImageCollection, x))
            .ToAsyncEnumerable()
            ;
    }

    public IAsyncEnumerable<IImageSource> GetLeafFoldersAsync(CancellationToken ct)
    {
        return ArchiveImageCollection.GetLeafFolders()
            .Select(x => (IImageSource)new ArchiveDirectoryImageSource(ArchiveImageCollection, x))
            .ToAsyncEnumerable();
    }

    public IAsyncEnumerable<IImageSource> GetAllImageFilesAsync(CancellationToken ct)
    {
        return ArchiveImageCollection.GetAllImages().ToAsyncEnumerable();
    }

    public IAsyncEnumerable<IImageSource> GetImageFilesAsync(CancellationToken ct)
    {
        return ArchiveImageCollection.GetImagesFromDirectory(ArchiveDirectoryToken).ToAsyncEnumerable();
    }

    public ValueTask<bool> IsExistFolderOrArchiveFileAsync(CancellationToken ct)
    {
        return new(ArchiveImageCollection.GetSubDirectories(ArchiveDirectoryToken).Any());
    }

    public ValueTask<bool> IsExistImageFileAsync(CancellationToken ct)
    {
        return new (ArchiveImageCollection.IsExistImageFromDirectory(ArchiveDirectoryToken));
    }

    public ValueTask<int> GetImageFileCountAsync(CancellationToken ct)
    {
        return ArchiveImageCollection.GetImageCountAsync(ct);
    }

    public ValueTask<IImageSource> GetImageFileAtAsync(int index, FileSortType sort, CancellationToken ct)
    {
        return ArchiveImageCollection.GetImageAtAsync(index, sort, ct);
    }

    public ValueTask<int> GetImageFileIndexFromKeyAsync(string key, FileSortType sort, CancellationToken ct)
    {
        return ArchiveImageCollection.GetIndexFromKeyAsync(key, sort, ct);
    }

    public bool IsSupportedFolderContentsChanged => false;

    public bool IsSupportFolderOrArchiveFilesIndexAccess => false;

    public IObservable<Unit> CreateFolderAndArchiveFileChangedObserver() => Observable.Empty<Unit>();
    public IObservable<Unit> CreateImageFileChangedObserver() => Observable.Empty<Unit>();

    public void Dispose()
    {
        ((IDisposable)ArchiveImageCollection).Dispose();
    }

    public ValueTask<int> GetFolderOrArchiveFilesCountAsync(CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask<IImageSource> GetFolderOrArchiveFileAtAsync(int index, FileSortType sort, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask<int> GetFolderOrArchiveFilesIndexFromKeyAsync(string key, FileSortType sort, CancellationToken ct)
    {
        throw new NotSupportedException();
    }
}

public sealed class PdfImageCollectionContext : IImageCollectionContext
{
    private readonly PdfImageCollection _pdfImageCollection;

    public PdfImageCollectionContext(PdfImageCollection pdfImageCollection)
    {
        _pdfImageCollection = pdfImageCollection;
    }

    public string Name => _pdfImageCollection.Name;

    public bool IsSupportedFolderContentsChanged => false;

    public bool IsSupportFolderOrArchiveFilesIndexAccess => false;

    public IObservable<Unit> CreateFolderAndArchiveFileChangedObserver() => Observable.Empty<Unit>();
    public IObservable<Unit> CreateImageFileChangedObserver() => Observable.Empty<Unit>();

    public IAsyncEnumerable<IImageSource> GetFolderOrArchiveFilesAsync(CancellationToken ct)
    {
        return AsyncEnumerable.Empty<IImageSource>();
    }

    public IAsyncEnumerable<IImageSource> GetLeafFoldersAsync(CancellationToken ct)
    {
        return AsyncEnumerable.Empty<IImageSource>();
    }

    public IAsyncEnumerable<IImageSource> GetAllImageFilesAsync(CancellationToken ct)
    {
        return _pdfImageCollection.GetAllImages().ToAsyncEnumerable();
    }

    public IAsyncEnumerable<IImageSource> GetImageFilesAsync(CancellationToken ct)
    {
        return _pdfImageCollection.GetAllImages().ToAsyncEnumerable();
    }

    public ValueTask<bool> IsExistFolderOrArchiveFileAsync(CancellationToken ct)
    {
        return new(false);
    }

    public ValueTask<bool> IsExistImageFileAsync(CancellationToken ct)
    {
        return new(_pdfImageCollection.GetAllImages().Any());
    }


    public ValueTask<int> GetImageFileCountAsync(CancellationToken ct)
    {
        return _pdfImageCollection.GetImageCountAsync(ct);
    }

    public ValueTask<IImageSource> GetImageFileAtAsync(int index, FileSortType sort, CancellationToken ct)
    {
        return _pdfImageCollection.GetImageAtAsync(index, sort, ct);
    }

    public ValueTask<int> GetImageFileIndexFromKeyAsync(string key, FileSortType sort, CancellationToken ct)
    {
        return _pdfImageCollection.GetIndexFromKeyAsync(key, sort, ct);
    }

    public ValueTask<int> GetFolderOrArchiveFilesCountAsync(CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask<IImageSource> GetFolderOrArchiveFileAtAsync(int index, FileSortType sort, CancellationToken ct)
    {
        throw new NotSupportedException();
    }

    public ValueTask<int> GetFolderOrArchiveFilesIndexFromKeyAsync(string key, FileSortType sort, CancellationToken ct)
    {
        throw new NotSupportedException();
    }
}
