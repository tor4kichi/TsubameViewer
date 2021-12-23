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
    public sealed class ImageCollectionManager
    {
        private readonly ThumbnailManager _thumbnailManager;
        private readonly FolderListingSettings _folderListingSettings;

        public ImageCollectionManager(
            ThumbnailManager thumbnailManager, 
            FolderListingSettings folderListingSettings
            )
        {
            _thumbnailManager = thumbnailManager;
            _folderListingSettings = folderListingSettings;
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
                var directoryToken = archiveDirectoryPath is not null ? aic.GetDirectoryTokenFromPath(archiveDirectoryPath) : null;
                if (archiveDirectoryPath is not null && directoryToken is null)
                {
                    throw new ArgumentException("not found directory in Archive file : " + archiveDirectoryPath);
                }
                return new ArchiveImageCollectionContext(aic, directoryToken, _folderListingSettings, _thumbnailManager);
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
            return new FolderImageCollectionContext(folder, _folderListingSettings, _thumbnailManager);
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

            try
            {
                ct.ThrowIfCancellationRequested();
                var zipArchive = ZipArchive.Open(stream)
                    .AddTo(disposables);
                foreach (var _ in zipArchive.Entries)
                {
                    ct.ThrowIfCancellationRequested();
                }
                return new ArchiveImageCollection(file, zipArchive, disposables, _folderListingSettings, _thumbnailManager);
            }
            catch
            {
                disposables.Dispose();
                throw;
            }
        }

        private async Task<IImageCollection> GetImagesFromPdfFileAsync(StorageFile file, CancellationToken ct)
        {
            var pdfDocument = await PdfDocument.LoadFromFileAsync(file).AsTask(ct);
            return new PdfImageCollection(file, pdfDocument, _folderListingSettings, _thumbnailManager);
        }


        private async Task<IImageCollection> GetImagesFromRarFileAsync(StorageFile file, CancellationToken ct)
        {
            CompositeDisposable disposables = new CompositeDisposable();
            var stream = await file.OpenStreamForReadAsync();
            disposables.Add(stream);

            try
            {
                var rarArchive = RarArchive.Open(stream)
                    .AddTo(disposables);
                foreach (var _ in rarArchive.Entries)
                {
                    ct.ThrowIfCancellationRequested();
                }
                return new ArchiveImageCollection(file, rarArchive, disposables, _folderListingSettings, _thumbnailManager);
            }
            catch
            {
                disposables.Dispose();
                throw;
            }
        }


        private async Task<IImageCollection> GetImagesFromSevenZipFileAsync(StorageFile file, CancellationToken ct)
        {
            CompositeDisposable disposables = new CompositeDisposable();
            var stream = await file.OpenStreamForReadAsync();
            disposables.Add(stream);

            try
            {
                var szArchive = SevenZipArchive.Open(stream)
                    .AddTo(disposables);
                foreach (var _ in szArchive.Entries)
                {
                    ct.ThrowIfCancellationRequested();
                }
                return new ArchiveImageCollection(file, szArchive, disposables, _folderListingSettings, _thumbnailManager);
            }
            catch
            {
                disposables.Dispose();
                throw;
            }
        }

        private async Task<IImageCollection> GetImagesFromTarFileAsync(StorageFile file, CancellationToken ct)
        {
            CompositeDisposable disposables = new CompositeDisposable();
            var stream = await file.OpenStreamForReadAsync();
            disposables.Add(stream);

            try
            {
                var tarArchive = TarArchive.Open(stream)
                    .AddTo(disposables);
                foreach (var _ in tarArchive.Entries)
                {
                    ct.ThrowIfCancellationRequested();
                }
                return new ArchiveImageCollection(file, tarArchive, disposables, _folderListingSettings, _thumbnailManager);
            }
            catch
            {
                disposables.Dispose();
                throw;
            }
        }
    }    

    

}
