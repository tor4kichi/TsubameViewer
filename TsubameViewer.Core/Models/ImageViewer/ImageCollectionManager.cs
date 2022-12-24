using CommunityToolkit.Diagnostics;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Tar;
using SharpCompress.Archives.Zip;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Search;

namespace TsubameViewer.Core.Models.ImageViewer;

public sealed class ImageCollectionManager
{
    private readonly ImageViewerSettings _imageViewerSettings;
    private readonly ArchiveFileInnerStructureCache _archiveFileInnerStructureCache;

    public ImageCollectionManager(
        ImageViewerSettings imageViewerSettings,
        ArchiveFileInnerStructureCache archiveFileInnerStructureCache            
        )
    {
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
        return file.FileType.ToLower() switch
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
            IReader reader = ReaderFactory.Open(stream);
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


