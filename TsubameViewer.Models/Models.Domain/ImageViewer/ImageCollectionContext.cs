using Microsoft.Toolkit.Diagnostics;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using Windows.Storage;
using Windows.Storage.Search;

namespace TsubameViewer.Models.Domain.ImageViewer
{
    public interface IImageCollectionContext
    {
        string Name { get; }

        ValueTask<bool> IsExistImageFileAsync(CancellationToken ct);
        ValueTask<bool> IsExistFolderOrArchiveFileAsync(CancellationToken ct);
        IAsyncEnumerable<IImageSource> GetAllImageFilesAsync(CancellationToken ct);

        IAsyncEnumerable<IImageSource> GetImageFilesAsync(CancellationToken ct);

        ValueTask<int> GetImageFileCountAsync(CancellationToken ct);
        ValueTask<IImageSource> GetImageFileAtAsync(int index, FileSortType sort, CancellationToken ct);
        ValueTask<int> GetIndexFromKeyAsync(string key, FileSortType sort, CancellationToken ct);


        IAsyncEnumerable<IImageSource> GetFolderOrArchiveFilesAsync(CancellationToken ct);
        IAsyncEnumerable<IImageSource> GetLeafFoldersAsync(CancellationToken ct);

        bool IsSupportedFolderContentsChanged { get; }

        IObservable<Unit> CreateFolderAndArchiveFileChangedObserver();
        IObservable<Unit> CreateImageFileChangedObserver();
    }

    public sealed class FolderImageCollectionContext : IImageCollectionContext
    {
        public static readonly QueryOptions DefaultImageFileSearchQueryOptions = CreateDefaultImageFileSearchQueryOptions(FileSortType.None);
        public static readonly QueryOptions FoldersAndArchiveFileSearchQueryOptions = new QueryOptions(CommonFileQuery.DefaultQuery, Enumerable.Concat(SupportedFileTypesHelper.SupportedArchiveFileExtensions, SupportedFileTypesHelper.SupportedEBookFileExtensions)) { FolderDepth = FolderDepth.Shallow };

        private readonly FolderListingSettings _folderListingSettings;
        private readonly ThumbnailManager _thumbnailManager;
        private StorageItemQueryResult _folderAndArchiveFileSearchQuery;
        private StorageItemQueryResult FolderAndArchiveFileSearchQuery => _folderAndArchiveFileSearchQuery ??= Folder.CreateItemQueryWithOptions(FoldersAndArchiveFileSearchQueryOptions);

        private StorageFileQueryResult _imageFileSearchQuery;
        private StorageFileQueryResult ImageFileSearchQuery => _imageFileSearchQuery ??= Folder.CreateFileQueryWithOptions(DefaultImageFileSearchQueryOptions);

        public string Name => Folder.Name;

        private readonly Dictionary<FileSortType, QueryOptions> _sortTypetoQueryOptions = new ();

        public FolderImageCollectionContext(StorageFolder storageFolder, FolderListingSettings folderListingSettings, ThumbnailManager thumbnailManager)
        {
            Folder = storageFolder;
            _folderListingSettings = folderListingSettings;
            _thumbnailManager = thumbnailManager;
            _sortTypetoQueryOptions.Add(FileSortType.None, DefaultImageFileSearchQueryOptions);
        }

        public StorageFolder Folder { get; }

        public IAsyncEnumerable<IImageSource> GetFolderOrArchiveFilesAsync(CancellationToken ct)
        {
            return FolderAndArchiveFileSearchQuery.ToAsyncEnumerable(ct)
                .Select(x => new StorageItemImageSource(x, _folderListingSettings, _thumbnailManager) as IImageSource);
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
                .Select(x => new StorageItemImageSource(x, _folderListingSettings, _thumbnailManager) as IImageSource);
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
                _imageFileSearchQuery.ApplyNewQueryOptions(GetSortQueryOptions(sort));
            }

            if (await ImageFileSearchQuery.GetFilesAsync((uint)index, 1).AsTask(ct) is not null and var files 
                && files.ElementAtOrDefault(0) is not null and var imageSource)
            {
                return new StorageItemImageSource(imageSource, _folderListingSettings, _thumbnailManager);
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



        private QueryOptions GetSortQueryOptions(FileSortType sort)
        {
            return _sortTypetoQueryOptions.TryGetValue(sort, out var queryOptions)
                ? queryOptions
                : _sortTypetoQueryOptions[sort] = CreateDefaultImageFileSearchQueryOptions(sort);
        }


        public async ValueTask<int> GetIndexFromKeyAsync(string key, FileSortType sort, CancellationToken ct)
        {
            if (_lastFileSortType != sort)
            {
                _lastFileSortType = sort;
                _imageFileSearchQuery.ApplyNewQueryOptions(GetSortQueryOptions(sort));
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
            return WindowsObservable.FromEventPattern<IStorageQueryResultBase, object>(h => FolderAndArchiveFileSearchQuery.ContentsChanged += h, h => FolderAndArchiveFileSearchQuery.ContentsChanged -= h).Throttle(TimeSpan.FromSeconds(1)).ToUnit();
        }

        public IObservable<Unit> CreateImageFileChangedObserver()
        {
            return WindowsObservable.FromEventPattern<IStorageQueryResultBase, object>(h => ImageFileSearchQuery.ContentsChanged += h, h => ImageFileSearchQuery.ContentsChanged -= h).Throttle(TimeSpan.FromSeconds(1)).ToUnit();
        }

    }

    public sealed class ArchiveImageCollectionContext : IImageCollectionContext, IDisposable
    {
        public ArchiveImageCollection ArchiveImageCollection { get; }
        public ArchiveDirectoryToken ArchiveDirectoryToken { get; }

        private readonly FolderListingSettings _folderListingSettings;
        private readonly ThumbnailManager _thumbnailManager;

        public string Name => ArchiveImageCollection.Name;


        public ArchiveImageCollectionContext(ArchiveImageCollection archiveImageCollection, ArchiveDirectoryToken archiveDirectoryToken, FolderListingSettings folderListingSettings, ThumbnailManager thumbnailManager)
        {
            ArchiveImageCollection = archiveImageCollection;
            ArchiveDirectoryToken = archiveDirectoryToken;
            _folderListingSettings = folderListingSettings;
            _thumbnailManager = thumbnailManager;
        }

        public IAsyncEnumerable<IImageSource> GetFolderOrArchiveFilesAsync(CancellationToken ct)
        {
            // アーカイブファイルは内部にフォルダ構造を持っている可能性がある
            // アーカイブ内のアーカイブは対応しない
            return ArchiveImageCollection.GetSubDirectories(ArchiveDirectoryToken)
                .Select(x => (IImageSource)new ArchiveDirectoryImageSource(ArchiveImageCollection, x, _folderListingSettings, _thumbnailManager))
                .ToAsyncEnumerable()
                ;
        }

        public IAsyncEnumerable<IImageSource> GetLeafFoldersAsync(CancellationToken ct)
        {
            return ArchiveImageCollection.GetLeafFolders()
                .Select(x => (IImageSource)new ArchiveDirectoryImageSource(ArchiveImageCollection, x, _folderListingSettings, _thumbnailManager))
                .ToAsyncEnumerable();
        }

        public IAsyncEnumerable<IImageSource> GetAllImageFilesAsync(CancellationToken ct)
        {
            return ArchiveImageCollection.GetAllImages().ToAsyncEnumerable();
        }

        public IAsyncEnumerable<IImageSource> GetImageFilesAsync(CancellationToken ct)
        {
            return ArchiveImageCollection.GetAllImages().ToAsyncEnumerable();
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

        public ValueTask<int> GetIndexFromKeyAsync(string key, FileSortType sort, CancellationToken ct)
        {
            return ArchiveImageCollection.GetIndexFromKeyAsync(key, sort, ct);
        }

        public bool IsSupportedFolderContentsChanged => false;

        public IObservable<Unit> CreateFolderAndArchiveFileChangedObserver() => throw new NotSupportedException();

        public IObservable<Unit> CreateImageFileChangedObserver() => throw new NotSupportedException();

        public void Dispose()
        {
            ((IDisposable)ArchiveImageCollection).Dispose();
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

        public ValueTask<int> GetIndexFromKeyAsync(string key, FileSortType sort, CancellationToken ct)
        {
            return _pdfImageCollection.GetIndexFromKeyAsync(key, sort, ct);
        }
    }


}
