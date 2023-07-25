using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Tar;
using SharpCompress.Archives.Zip;
using SharpCompress.Readers;
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
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using TsubameViewer.Core.Models.SourceFolders;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Search;
using static System.Net.WebRequestMethods;

namespace TsubameViewer.Core.Models.ImageViewer;

public sealed class ImageCollectionManager
{
    private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
    private readonly ImageViewerSettings _imageViewerSettings;
    private readonly ArchiveFileInnerStructureCache _archiveFileInnerStructureCache;

    public ImageCollectionManager(
        SourceStorageItemsRepository sourceStorageItemsRepository,
        ImageViewerSettings imageViewerSettings,
        ArchiveFileInnerStructureCache archiveFileInnerStructureCache            
        )
    {
        _sourceStorageItemsRepository = sourceStorageItemsRepository;
        _imageViewerSettings = imageViewerSettings;
        _archiveFileInnerStructureCache = archiveFileInnerStructureCache;
    }

    public bool IsSupportGetArchiveImageCollectionContext(IStorageItem storageItem, CancellationToken ct)
    {
        if (storageItem is StorageFile file)
        {
            return file.IsSupportedMangaFile();
        }
        else
        {
            return false;
        }
    }

    public bool IsSupportGetImageCollectionContext(IStorageItem storageItem, CancellationToken ct)
    {
        if (storageItem is StorageFile file)
        {
            return file.IsSupportedMangaFile();
        }
        else if (storageItem is StorageFolder folder)
        {
            return true;
        }
        else
        {
            throw new NotSupportedException();
        }
    }

    public async Task<(IImageSource ImageSource, IImageCollectionContext ImageCollectionContext)> GetImageSourceAndContextAsync(string path, string? pageName, CancellationToken ct)
    {
        IStorageItem storageItem = null;
        foreach (var _ in Enumerable.Repeat(0, 10))
        {
            storageItem = await _sourceStorageItemsRepository.TryGetStorageItemFromPath(path);
            if (storageItem != null)
            {
                break;
            }
            await Task.Delay(100, ct);
        }

        if (storageItem == null) { throw new FileNotFoundException(path); }

        if (storageItem is StorageFolder folder)
        {
            Debug.WriteLine(folder.Path);
            return (new StorageItemImageSource(storageItem), GetFolderImageCollectionContext(folder, ct));
        }
        else if (storageItem is StorageFile file)
        {
            Debug.WriteLine(file.Path);
            if (file.IsSupportedImageFile())
            {
                try
                {
                    var parentFolder = await file.GetParentAsync();
                    if (parentFolder != null)
                    {
                        return (new StorageItemImageSource(storageItem), GetFolderImageCollectionContext(parentFolder, ct));
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    var parentItem = await _sourceStorageItemsRepository.TryGetStorageItemFromPath(Path.GetDirectoryName(path));
                    if (parentItem is StorageFolder parentFolder)
                    {
                        return (new StorageItemImageSource(storageItem), GetFolderImageCollectionContext(parentFolder, ct));
                    }
                    else
                    {
                        throw;
                    }
                }

                return (new StorageItemImageSource(storageItem), new OnlyOneFileImageCollectionContext(file));
            }
            else if (file.IsSupportedMangaFile())
            {
                // string.Emptyを渡すことでルートフォルダのフォルダ取得を行える
                var imageCollectionContext = await GetArchiveImageCollectionContextAsync(file, pageName ?? string.Empty, ct);
                if (string.IsNullOrWhiteSpace(pageName))
                {
                    // Mangaファイルが指定された場合
                    return (new StorageItemImageSource(storageItem), imageCollectionContext);
                }
                else if (imageCollectionContext is ArchiveImageCollectionContext aic)
                {
                    // Managaファイル内のEntryが指定された場合
                    return (new ArchiveDirectoryImageSource(aic.ArchiveImageCollection, aic.ArchiveDirectoryToken), imageCollectionContext);
                }
                else
                {
                    (imageCollectionContext as IDisposable)?.Dispose();
                    throw new NotImplementedException();
                }
            }
            else
            {
                // 非対応なファイル
                throw new NotSupportedException();
            }
        }
        else
        {
            // 非対応なストレージコンテンツ
            throw new NotSupportedException();
        }
    }

    public async Task<IImageCollectionContext> GetArchiveImageCollectionContextAsync(StorageFile file, string? archiveDirectoryPath, CancellationToken ct)
    {
        Guard.IsTrue(file.IsSupportedMangaFile(), "file.IsSupportedMangaFile");

        // Task.Runで包まないとUIが固まる
        var imageCollection = await Task.Run(() => GetImagesFromArchiveFileAsync(file, ct), ct);
        if (imageCollection is ArchiveImageCollection aic)
        {
            var directoryToken = archiveDirectoryPath is not null ? aic.GetDirectoryTokenFromPath(archiveDirectoryPath) : aic.RootDirectoryToken;
            if (archiveDirectoryPath is not null && directoryToken is null)
            {
                throw new ArgumentException("not found directory in Archive file : " + archiveDirectoryPath);
            }
            return new ArchiveImageCollectionContext(aic, directoryToken);
        }
        else if (imageCollection is PdfImageCollection pdfImageCollection)
        {
            return new PdfImageCollectionContext(pdfImageCollection);
        }
        else
        {
            throw new NotSupportedException();
        }
    }

    public FolderImageCollectionContext GetFolderImageCollectionContext(StorageFolder folder, CancellationToken ct)
    {
        return new FolderImageCollectionContext(folder);
    }


    

    private Task<IImageCollection> GetImagesFromArchiveFileAsync(StorageFile file, CancellationToken ct)
    {
        return file.FileType.ToLowerInvariant() switch
        {
            SupportedFileTypesHelper.ZipFileType => GetImagesFromZipFileAsync(file, ct),
            SupportedFileTypesHelper.RarFileType => GetImagesFromRarFileAsync(file, ct),
            SupportedFileTypesHelper.PdfFileType => GetImagesFromPdfFileAsync(file, ct),
            SupportedFileTypesHelper.CbzFileType => GetImagesFromZipFileAsync(file, ct),
            SupportedFileTypesHelper.CbrFileType => GetImagesFromRarFileAsync(file, ct),
            SupportedFileTypesHelper.SevenZipFileType => GetImagesFromSevenZipFileAsync(file, ct),
            SupportedFileTypesHelper.Cb7FileType => GetImagesFromSevenZipFileAsync(file, ct),
            SupportedFileTypesHelper.TarFileType => GetImagesFromTarFileAsync(file, ct),
            _ => throw new NotSupportedException("not supported file type: " + file.FileType),
        };
    }

    
    // Note: 取得前にforeachで全項目を列挙しているのはオープン中の非同期キャンセルをするため

    private async Task<IImageCollection> GetImagesFromZipFileAsync(StorageFile file, CancellationToken ct)
    {
        CompositeDisposable disposables = new CompositeDisposable();
        var stream = await file.OpenStreamForReadAsync();
        disposables.Add(stream);

        var prop = await file.GetBasicPropertiesAsync();
        try
        {
            ct.ThrowIfCancellationRequested();
            var zipArchive = ZipArchive.Open(stream)
                .AddTo(disposables);

            var sturecture = _archiveFileInnerStructureCache.GetOrCreateStructure(file.Path, prop.Size, zipArchive, ct);

            return new ArchiveImageCollection(file, zipArchive, sturecture, disposables);
        }
        catch
        {
            _archiveFileInnerStructureCache.Delete(file.Path);
            disposables.Dispose();
            throw;
        }
    }

    private async Task<IImageCollection> GetImagesFromPdfFileAsync(StorageFile file, CancellationToken ct)
    {
        var pdfDocument = await PdfDocument.LoadFromFileAsync(file).AsTask(ct);
        return new PdfImageCollection(file, pdfDocument, _imageViewerSettings);
    }


    private async Task<IImageCollection> GetImagesFromRarFileAsync(StorageFile file, CancellationToken ct)
    {
        CompositeDisposable disposables = new CompositeDisposable();
        var stream = await file.OpenStreamForReadAsync();
        disposables.Add(stream);

        var prop = await file.GetBasicPropertiesAsync();
        try
        {
            var rarArchive = RarArchive.Open(stream)
                .AddTo(disposables);
            var sturecture = _archiveFileInnerStructureCache.GetOrCreateStructure(file.Path, prop.Size, rarArchive, ct);
            return new ArchiveImageCollection(file, rarArchive, sturecture, disposables);
        }
        catch
        {
            _archiveFileInnerStructureCache.Delete(file.Path);
            disposables.Dispose();
            throw;
        }
    }


    private async Task<IImageCollection> GetImagesFromSevenZipFileAsync(StorageFile file, CancellationToken ct)
    {
        CompositeDisposable disposables = new CompositeDisposable();
        var stream = await file.OpenStreamForReadAsync();
        disposables.Add(stream);

        var prop = await file.GetBasicPropertiesAsync();
        try
        {
            var szArchive = SevenZipArchive.Open(stream)
                .AddTo(disposables);
            var sturecture = _archiveFileInnerStructureCache.GetOrCreateStructure(file.Path, prop.Size, szArchive, ct);
            return new ArchiveImageCollection(file, szArchive, sturecture, disposables);
        }
        catch
        {
            _archiveFileInnerStructureCache.Delete(file.Path);
            disposables.Dispose();
            throw;
        }
    }

    private async Task<IImageCollection> GetImagesFromTarFileAsync(StorageFile file, CancellationToken ct)
    {
        CompositeDisposable disposables = new CompositeDisposable();
        var stream = await file.OpenStreamForReadAsync();
        disposables.Add(stream);

        var prop = await file.GetBasicPropertiesAsync();
        try
        {
            var tarArchive = TarArchive.Open(stream)
                .AddTo(disposables);
            var sturecture = _archiveFileInnerStructureCache.GetOrCreateStructure(file.Path, prop.Size, tarArchive, ct);
            return new ArchiveImageCollection(file, tarArchive, sturecture, disposables);
        }
        catch
        {
            _archiveFileInnerStructureCache.Delete(file.Path);
            disposables.Dispose();
            throw;
        }
    }
}


public sealed class OnlyOneFileImageCollectionContext : IImageCollectionContext
{
    private readonly StorageFile _file;
    private readonly StorageItemImageSource _imageSource;

    public OnlyOneFileImageCollectionContext(StorageFile file)
    {
        _file = file;
        _imageSource = new StorageItemImageSource(file);
    }
    public string Name => _imageSource.Name;

    public bool IsSupportedFolderContentsChanged => false;

    public IObservable<Unit> CreateFolderAndArchiveFileChangedObserver()
    {
        throw new NotImplementedException();
    }

    public IObservable<Unit> CreateImageFileChangedObserver()
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<IImageSource> GetAllImageFilesAsync(CancellationToken ct)
    {
        return new[] { _imageSource }.ToAsyncEnumerable();
    }

    public IAsyncEnumerable<IImageSource> GetFolderOrArchiveFilesAsync(CancellationToken ct)
    {
        return AsyncEnumerable.Empty<IImageSource>();
    }

    public ValueTask<IImageSource> GetImageFileAtAsync(int index, FileSortType sort, CancellationToken ct)
    {
        if (index == 0)
        {
            return new (_imageSource);
        }
        else
        {
            throw new InvalidOperationException(index.ToString());
        }
    }

    public ValueTask<int> GetImageFileCountAsync(CancellationToken ct)
    {
        return new(1);
    }

    public IAsyncEnumerable<IImageSource> GetImageFilesAsync(CancellationToken ct)
    {
        return new[] { _imageSource }.ToAsyncEnumerable();
    }

    public ValueTask<int> GetIndexFromKeyAsync(string key, FileSortType sort, CancellationToken ct)
    {
        if (key == _file.Name) { return new(0); }

        throw new InvalidOperationException(key);
    }

    public IAsyncEnumerable<IImageSource> GetLeafFoldersAsync(CancellationToken ct)
    {
        return AsyncEnumerable.Empty<IImageSource>();
    }

    public ValueTask<bool> IsExistFolderOrArchiveFileAsync(CancellationToken ct)
    {
        return new(false);
    }

    public ValueTask<bool> IsExistImageFileAsync(CancellationToken ct)
    {
        return new(false);
    }
}
