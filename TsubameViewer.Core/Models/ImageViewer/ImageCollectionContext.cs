using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Contracts.Services;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.UI.Xaml.Media;

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

    private StorageItemQueryResult _folderAndArchiveFileSearchQuery;
    private StorageItemQueryResult FolderAndArchiveFileSearchQuery => _folderAndArchiveFileSearchQuery ??= Folder.CreateItemQueryWithOptions(FoldersAndArchiveFileSearchQueryOptions);

    private StorageFileQueryResult _imageFileSearchQuery;
    private StorageFileQueryResult ImageFileSearchQuery => _imageFileSearchQuery ??= Folder.CreateFileQueryWithOptions(DefaultImageFileSearchQueryOptions);

    public string Name => Folder?.Name ?? "";

    public FolderImageCollectionContext(StorageFolder storageFolder)
    {
        Folder = storageFolder;
    }

    public StorageFolder Folder { get; }




    public bool IsSupportFolderOrArchiveFilesIndexAccess => true;

    private FileSortType _lastFolderAndArchiveFilesSortType;
    public async ValueTask<int> GetFolderOrArchiveFilesCountAsync(CancellationToken ct)
    {
        return (int)await FolderAndArchiveFileSearchQuery.GetItemCountAsync().AsTask(ct);
    }

    int _prevAccessIndex = -1;
    int _cachedPage = -1;
    IStorageItem[] _cachedPageItems = new IStorageItem[100];

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
            await foreach (var file in FolderAndArchiveFileSearchQuery.ToAsyncEnumerable(ct))
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
            Enumerable.Concat(SupportedFileTypesHelper.SupportedArchiveFileExtensions, SupportedFileTypesHelper.SupportedEBookFileExtensions)
            )
        {
            FolderDepth = FolderDepth.Shallow
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
        return (int)await ImageFileSearchQuery.GetItemCountAsync().AsTask(ct);
    }

    private FileSortType _lastFileSortType;
    public async ValueTask<IImageSource> GetImageFileAtAsync(int index, FileSortType sort, CancellationToken ct)
    {
        if (_lastFileSortType != sort)
        {
            _lastFileSortType = sort;
            _imageFileSearchQuery.ApplyNewQueryOptions(GetImageFileSortQueryOptions(sort));
        }

        if (await ImageFileSearchQuery.GetFilesAsync((uint)index, 1).AsTask(ct) is not null and var files 
            && files.ElementAtOrDefault(0) is not null and var imageSource)
        {
            return new StorageItemImageSource(imageSource);
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "index out of range.");
        }
    }


    // see@ https://docs.microsoft.com/en-us/uwp/api/windows.storage.search.queryoptions.sortorder?view=winrt-22000#remarks
    public static QueryOptions CreateDefaultImageFileSearchQueryOptions(FileSortType sort)
    {
        var query = new QueryOptions(CommonFileQuery.DefaultQuery, SupportedFileTypesHelper.SupportedImageFileExtensions)
        {
            FolderDepth = FolderDepth.Shallow,
            IndexerOption = IndexerOption.UseIndexerWhenAvailable,            
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
        if (_lastFileSortType != sort)
        {
            _lastFileSortType = sort;
            _imageFileSearchQuery.ApplyNewQueryOptions(GetImageFileSortQueryOptions(sort));
        }

        if (sort is FileSortType.None or FileSortType.TitleAscending or FileSortType.TitleDecending)
        {
            string filename = Path.GetFileName(key);
            uint result = await ImageFileSearchQuery.FindStartIndexAsync(filename);
            return result != uint.MaxValue ? (int)result : throw new KeyNotFoundException($"not found file : {filename}");
        }
        else 
        {
            // FindStartIndexAsync が意図したIndexを返さないので頭から走査する
            int index = 0;
            await foreach (var file in ImageFileSearchQuery.ToAsyncEnumerable(ct))
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

    public IObservable<Unit> CreateFolderAndArchiveFileChangedObserver() => throw new NotSupportedException();

    public IObservable<Unit> CreateImageFileChangedObserver() => throw new NotSupportedException();

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

    public IObservable<Unit> CreateFolderAndArchiveFileChangedObserver() => throw new NotSupportedException();
    public IObservable<Unit> CreateImageFileChangedObserver() => throw new NotSupportedException();

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
