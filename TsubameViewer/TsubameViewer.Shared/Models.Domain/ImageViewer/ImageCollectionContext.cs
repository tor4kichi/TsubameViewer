using Microsoft.Toolkit.Diagnostics;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Tar;
using SharpCompress.Archives.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using Windows.Data.Pdf;
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

        IAsyncEnumerable<IImageSource> GetFolderOrArchiveFilesAsync(CancellationToken ct);
        IAsyncEnumerable<IImageSource> GetLeafFoldersAsync(CancellationToken ct);

        bool IsSupportedFolderContentsChanged { get; }

        IObservable<Unit> CreateFolderAndArchiveFileChangedObserver();
        IObservable<Unit> CreateImageFileChangedObserver();
    }

    public sealed class FolderImageCollectionContext : IImageCollectionContext
    {
        public static readonly QueryOptions ImageFileSearchQueryOptions = new QueryOptions(CommonFileQuery.DefaultQuery, SupportedFileTypesHelper.SupportedImageFileExtensions);
        public static readonly QueryOptions FoldersAndArchiveFileSearchQueryOptions = new QueryOptions(CommonFileQuery.DefaultQuery, Enumerable.Concat(SupportedFileTypesHelper.SupportedArchiveFileExtensions, SupportedFileTypesHelper.SupportedEBookFileExtensions));

        private readonly FolderListingSettings _folderListingSettings;
        private readonly ThumbnailManager _thumbnailManager;
        private StorageItemQueryResult _folderAndArchiveFileSearchQuery;
        private StorageItemQueryResult FolderAndArchiveFileSearchQuery => _folderAndArchiveFileSearchQuery ??= Folder.CreateItemQueryWithOptions(FoldersAndArchiveFileSearchQueryOptions);

        private StorageFileQueryResult _imageFileSearchQuery;
        private StorageFileQueryResult ImageFileSearchQuery => _imageFileSearchQuery ??= Folder.CreateFileQueryWithOptions(ImageFileSearchQueryOptions);

        public string Name => Folder.Name;

        public FolderImageCollectionContext(StorageFolder storageFolder, FolderListingSettings folderListingSettings, ThumbnailManager thumbnailManager)
        {
            Folder = storageFolder;
            _folderListingSettings = folderListingSettings;
            _thumbnailManager = thumbnailManager;
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

        public bool IsSupportedFolderContentsChanged => true;


        public IObservable<Unit> CreateFolderAndArchiveFileChangedObserver()
        {
            return WindowsObservable.FromEventPattern<IStorageQueryResultBase, object>(h => FolderAndArchiveFileSearchQuery.ContentsChanged += h, h => FolderAndArchiveFileSearchQuery.ContentsChanged -= h).ToUnit();
        }

        public IObservable<Unit> CreateImageFileChangedObserver()
        {
            return WindowsObservable.FromEventPattern<IStorageQueryResultBase, object>(h => ImageFileSearchQuery.ContentsChanged += h, h => ImageFileSearchQuery.ContentsChanged -= h).ToUnit();
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
            return ArchiveImageCollection.GetImagesFromDirectory(ArchiveDirectoryToken).ToAsyncEnumerable();
        }

        public ValueTask<bool> IsExistFolderOrArchiveFileAsync(CancellationToken ct)
        {
            return new(ArchiveImageCollection.GetSubDirectories(ArchiveDirectoryToken).Any());
        }

        public ValueTask<bool> IsExistImageFileAsync(CancellationToken ct)
        {
            return new(ArchiveImageCollection.GetImagesFromDirectory(ArchiveDirectoryToken).Any());
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
    }


}
