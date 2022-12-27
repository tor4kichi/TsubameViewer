using LiteDB;
using Microsoft.IO;
using Reactive.Bindings;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Tar;
using SharpCompress.Compressors.Xz;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Contracts.Services;
using TsubameViewer.Core.Infrastructure;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using TsubameViewer.Core.Models.SourceFolders;
using VersOne.Epub;
using Windows.Data.Pdf;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Xaml.Media.Imaging;


namespace TsubameViewer.Core.Service;

public sealed class ThumbnailImageService 
    : IThumbnailImageService
    , ISecondaryTileThumbnailImageService
    , IThumbnailImageMaintenanceService
{
    private readonly ILiteDatabase _temporaryDb;
    private readonly ILiteCollection<ThumbnailItemIdEntry> _thumbnailIdDb;
    private readonly ILiteStorage<string> _thumbnailDb;
    private readonly FolderListingSettings _folderListingSettings;
    private readonly ThumbnailImageInfoRepository _thumbnailImageInfoRepository;
    private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
    private readonly static AsyncLock _fileReadWriteLock = new();
    private readonly RecyclableMemoryStreamManager _recyclableMemoryStreamManager;    

    private string GetArchiveEntryPath(StorageFile file, IArchiveEntry entry)
    {
        return Path.Combine(file.Path, entry?.Key ?? "_");
    }
    private string GetArchiveEntryPath(StorageFile file, PdfPage pdfPage)
    {
        return Path.Combine(file.Path, pdfPage.Index.ToString());
    }

    Regex _titlePriorityRegex;
    string _lasttitlePriorityRegexText;
    Regex GetTitlePriorityRegex()
    {
        if (_titlePriorityRegex != null)
        {
            if (_lasttitlePriorityRegexText != _folderListingSettings.ThumbnailPriorityTitleRegex)
            {
                _titlePriorityRegex = null;
            }

            try
            {
                _titlePriorityRegex ??= new Regex(_folderListingSettings.ThumbnailPriorityTitleRegex);
            }
            catch { }
        }

        return _titlePriorityRegex;
    }

    private string ToId(string path)
    {
        return $"$/{path}".Replace('\\', '/');
    }

    private string ToId(IStorageItem storageItem)
    {
        return storageItem switch
        {
            StorageFile file => ToId(file),
            StorageFolder folder => ToId(folder),
            _ => throw new NotSupportedException(),
        };
    }

    private string ToId(StorageFile file)
    {
        return ToId(file.Path);
    }


    private string ToId(StorageFolder folder)
    {
        return ToId(Path.Combine(folder.Path, "_"));
    }



    // Note: LiteDBはDeleteしたEntryのIDを削除対象としてマークして、後でまとめて削除するような動作になっている
    //       そのため上書きした画像データのIDが後で消えないようにIDがユニークになるようにしている

    Random _thumbnailIdPostfixRandom = new Random();
    private string CreateThumbnailInsideId(string itemId)
    {
        var itemIdWithRndPostfix = itemId + _thumbnailIdPostfixRandom.Next().ToString();
        _thumbnailIdDb.Upsert(new ThumbnailItemIdEntry() { Id = itemId, InsideId = itemIdWithRndPostfix });
        return itemIdWithRndPostfix;
    }

    private bool TryGetThumbnailInsideId(string itemId, out string outInsideId)
    {
        if (_thumbnailIdDb.FindById(itemId) is not null and var idEntry)
        {
            outInsideId = idEntry.InsideId;
            return true;
        }
        else
        {
            outInsideId = string.Empty;
            return false;
        }
    }

    public long ComputeUsingSize()
    {
        return _thumbnailDb.FindAll().Sum(x => x.Length);
    }

    private void EncodingForFolderOrArchiveFileThumbnailBitmap(BitmapDecoder decoder, BitmapEncoder encoder)
    {
        if (decoder.PixelHeight > decoder.PixelWidth)
        {
            // 縦横比を維持したまま 高さ = LargeFileThumbnailImageHeight になるようにスケーリング
            var ratio = _folderListingSettings.FolderItemThumbnailImageSize.Width / decoder.PixelWidth;
            encoder.BitmapTransform.ScaledHeight = (uint)Math.Floor(decoder.PixelHeight * ratio);
            encoder.BitmapTransform.ScaledWidth = (uint)_folderListingSettings.FolderItemThumbnailImageSize.Width;
        }
        else
        {
            var ratio = _folderListingSettings.FolderItemThumbnailImageSize.Height / decoder.PixelHeight;
            encoder.BitmapTransform.ScaledWidth = (uint)Math.Floor(decoder.PixelWidth * ratio);
            encoder.BitmapTransform.ScaledHeight = (uint)_folderListingSettings.FolderItemThumbnailImageSize.Height;
        }
        //encoder.BitmapTransform.Bounds = new BitmapBounds() { X = 0, Y = 0, Height = encoder.BitmapTransform.ScaledHeight, Width = encoder.BitmapTransform.ScaledWidth };
        encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
    }

    private void EncodingForImageFileThumbnailBitmap(BitmapDecoder decoder, BitmapEncoder encoder)
    {
        // 縦横比を維持したまま 高さ = LargeFileThumbnailImageHeight になるようにスケーリング
        var ratio = (double)ListingImageConstants.LargeFileThumbnailImageHeight / decoder.PixelHeight;
        encoder.BitmapTransform.ScaledWidth = (uint)Math.Floor(decoder.PixelWidth * ratio);
        encoder.BitmapTransform.ScaledHeight = ListingImageConstants.LargeFileThumbnailImageHeight;
        encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
    }

    class ThumbnailItemIdEntry
    {
        [BsonId]
        public string Id { get; set; }

        public string InsideId { get; set; }
    }

    public ThumbnailImageService(
        ILiteDatabase temporaryDb,
        FolderListingSettings folderListingSettings,
        ThumbnailImageInfoRepository thumbnailImageInfoRepository,
        SourceStorageItemsRepository sourceStorageItemsRepository
        )
    {
        _temporaryDb = temporaryDb;
        _thumbnailIdDb = _temporaryDb.GetCollection<ThumbnailItemIdEntry>();
        _thumbnailDb = _temporaryDb.FileStorage;
        _folderListingSettings = folderListingSettings;
        _thumbnailImageInfoRepository = thumbnailImageInfoRepository;
        _sourceStorageItemsRepository = sourceStorageItemsRepository;
        _recyclableMemoryStreamManager = new RecyclableMemoryStreamManager();
    }

    private void UploadWithRetry(string itemId, string filename, Stream stream)
    {
        if (TryGetThumbnailInsideId(itemId, out var insideId))
        {
            _thumbnailDb.Delete(insideId);
            _thumbnailIdDb.Delete(itemId);
        }

        stream.Seek(0, SeekOrigin.Begin);
        _thumbnailDb.Upload(CreateThumbnailInsideId(itemId), filename, stream);
        stream.Seek(0, SeekOrigin.Begin);
    }

    #region ImageSource Base
    
    private string GetId(IImageSource imageSource)
    {
        return imageSource.StorageItem is StorageFolder folder ? ToId(folder) : ToId(imageSource.Path);
    }

    // Note: Task.Run(async () => await SomeValueTaskMethod()) の形になるとリリースビルドでクラッシュする
    public async Task<IRandomAccessStream> GetThumbnailImageStreamAsync(IImageSource imageSource, IRandomAccessStream outputStream = null, CancellationToken ct = default)
    {
        var itemId = GetId(imageSource);
        if (await GetThumbnailFromIdAsync(itemId, ct) is not null and var cachedImageStream)
        {
            return cachedImageStream;
        }

        if (imageSource.StorageItem is StorageFolder folder)
        {
            outputStream ??= _recyclableMemoryStreamManager.GetStream().AsRandomAccessStream();
            try
            {
                var file = await GetCoverThumbnailImageAsync(folder, ct);
                if (await GenerateThumbnailImageToStreamAsync(file, outputStream, EncodingForFolderOrArchiveFileThumbnailBitmap, ct))
                {
                    UploadWithRetry(itemId, imageSource.Name, outputStream.AsStreamForRead());
                    return outputStream;
                }
                else
                {
                    throw new NotSupportedException();
                }
            }
            catch 
            {
                outputStream.Dispose();
                throw;
            }
        }
        else if (imageSource is StorageItemImageSource && imageSource.StorageItem is StorageFile file && file.IsSupportedMangaOrEBookFile())
        {
            outputStream ??= _recyclableMemoryStreamManager.GetStream().AsRandomAccessStream();
            try
            {
                if (await GenerateThumbnailImageToStreamAsync(file, outputStream, EncodingForFolderOrArchiveFileThumbnailBitmap, ct))
                {
                    UploadWithRetry(itemId, imageSource.Name, outputStream.AsStreamForRead());
                    return outputStream;
                }
                else
                {
                    throw new NotSupportedException();
                }
            }
            catch 
            {
                outputStream.Dispose();
                throw;
            }
        }
        else if (imageSource is AlbamItemImageSource albamItemImageSource)
        {
            return await GetThumbnailImageStreamAsync(albamItemImageSource.InnerImageSource, outputStream, ct);
        }
        else if (imageSource is AlbamImageSource albamImageSource)
        {
            var sampleImageSource = await albamImageSource.GetSampleImageSourceAsync(ct);
            if (sampleImageSource == null) { return null; }
            return await GetThumbnailImageStreamAsync(sampleImageSource, outputStream, ct);
        }
        else
        {
            using var imageStream = await imageSource.GetImageStreamAsync(ct);
            using (await _fileReadWriteLock.LockAsync(ct))
            {
                // Note: ImageViewer画面から _recyclableMemoryStreamManager.GetStream(); を使うと
                // BitmapEncoder.CreateAsync() でハングアップしてしまう
                outputStream ??= _recyclableMemoryStreamManager.GetStream().AsRandomAccessStream();
                try
                {
                    await TranscodeThumbnailImageToStreamAsync(imageSource.Path, imageStream, outputStream, EncodingForFolderOrArchiveFileThumbnailBitmap, ct);
                    UploadWithRetry(itemId, imageSource.Name, outputStream.AsStreamForRead());
                    return outputStream;
                }
                catch
                {
                    outputStream.Dispose();
                    throw;
                }
            }
        }
    }

    public async Task SetParentThumbnailImageAsync(IImageSource childImageSource, bool isArchiveThumbnailSetToFile = false, CancellationToken ct = default)
    {
        // ネイティブコンパイル時かつ画像ビューア上からのサムネイル設定でアプリがハングアップを起こすため
        // InMemoryRandomAccessStreamを使用している
        var inMemoryRas = new InMemoryRandomAccessStream();
        using var imageMemoryStream = await GetThumbnailImageStreamAsync(childImageSource, inMemoryRas, ct);

        bool requireTranscode = false;

        if (childImageSource is ArchiveEntryImageSource archiveEntry)
        {
            var parentDirectoryArchiveEntry = archiveEntry.GetParentDirectoryEntry();
            if (isArchiveThumbnailSetToFile || parentDirectoryArchiveEntry == null)
            {
                await SetThumbnailAsync(archiveEntry.StorageItem, imageMemoryStream, requireTrancode: requireTranscode, default);
            }
            else
            {
                await SetArchiveEntryThumbnailAsync(archiveEntry.StorageItem, parentDirectoryArchiveEntry, imageMemoryStream, requireTrancode: requireTranscode, default);
            }
        }
        else if (childImageSource is PdfPageImageSource pdf)
        {
            await SetThumbnailAsync(pdf.StorageItem, imageMemoryStream, requireTrancode: requireTranscode, default);
        }
        else if (childImageSource is StorageItemImageSource folderItem)
        {
            var folder = await _sourceStorageItemsRepository.TryGetStorageItemFromPath(Path.GetDirectoryName(folderItem.Path));
            if (folder == null) { throw new InvalidOperationException(); }
            await SetThumbnailAsync(folder, imageMemoryStream, requireTrancode: requireTranscode, default);
        }
        else if (childImageSource is ArchiveDirectoryImageSource archiveDirectoryItem)
        {
            var parentDirectoryArchiveEntry = archiveDirectoryItem.GetParentDirectoryEntry();
            if (isArchiveThumbnailSetToFile || parentDirectoryArchiveEntry == null)
            {
                await SetThumbnailAsync(archiveDirectoryItem.StorageItem, imageMemoryStream, requireTrancode: requireTranscode, default);
            }
            else
            {
                await SetArchiveEntryThumbnailAsync(archiveDirectoryItem.StorageItem, parentDirectoryArchiveEntry, imageMemoryStream, requireTrancode: requireTranscode, default);
            }
        }
        else
        {
            throw new NotSupportedException();
        }       
    }

    public ThumbnailSize? GetCachedThumbnailSize(IImageSource imageSource)
    {
        return _thumbnailImageInfoRepository.TryGetSize(imageSource.Path);        
    }

    public ThumbnailSize SetThumbnailSize(IImageSource imageSource, uint pixelWidth, uint pixelHeight)
    {
        var item = _thumbnailImageInfoRepository.UpdateItem(new ThumbnailImageInfo()
        {
            Path = imageSource.Path,
            ImageWidth = pixelWidth,
            ImageHeight = pixelHeight,            
            RatioWH = pixelWidth / (float)pixelHeight
        });

        return new ThumbnailSize()
        {
            Height = item.ImageHeight,
            Width = item.ImageWidth,
            RatioWH = item.RatioWH,
        };
    }

    #endregion

    public async Task SetThumbnailAsync(IStorageItem targetItem, IRandomAccessStream bitmapImage, bool requireTrancode, CancellationToken ct)
    {
        await Task.Run(async () =>
        {
            var itemId = ToId(targetItem);
            using (await _fileReadWriteLock.LockAsync(ct))
            {
                if (requireTrancode)
                {
                    using (var memoryStream = _recyclableMemoryStreamManager.GetStream())
                    {
                        await TranscodeThumbnailImageToStreamAsync(targetItem.Path, bitmapImage, memoryStream.AsRandomAccessStream(), EncodingForFolderOrArchiveFileThumbnailBitmap, ct);
                        UploadWithRetry(itemId, targetItem.Name, memoryStream);
                    }
                }
                else
                {
                    UploadWithRetry(itemId, targetItem.Name, bitmapImage.AsStreamForRead());
                }
            }
        });
    }


    public async Task SetArchiveEntryThumbnailAsync(StorageFile targetItem, IArchiveEntry entry, IRandomAccessStream bitmapImage, bool requireTrancode, CancellationToken ct)
    {
        await Task.Run(async () =>
        {
            var path = GetArchiveEntryPath(targetItem, entry);
            var itemId = ToId(path);
            using (await _fileReadWriteLock.LockAsync(ct))
            {
                if (requireTrancode)
                {
                    using (var memoryStream = _recyclableMemoryStreamManager.GetStream())
                    {
                        await TranscodeThumbnailImageToStreamAsync(path, bitmapImage, memoryStream.AsRandomAccessStream(), EncodingForFolderOrArchiveFileThumbnailBitmap, ct);

                        memoryStream.Seek(0, SeekOrigin.Begin);
                        UploadWithRetry(itemId, targetItem.Name, memoryStream);

                        memoryStream.Seek(0, SeekOrigin.Begin);
                    }
                }
                else
                {
                    UploadWithRetry(itemId, targetItem.Name, bitmapImage.AsStreamForRead());
                }
            }
        });
    }

    public async Task DeleteAllThumbnailsAsync()
    {
        _thumbnailImageInfoRepository.DeleteAll();

        using (await _fileReadWriteLock.LockAsync(CancellationToken.None))
            foreach (var id in _thumbnailDb.FindAll().ToArray())
            {
                _thumbnailDb.Delete(id.Id);
            }
    }

    public async Task DeleteThumbnailFromPathAsync(string path)
    {
        _thumbnailImageInfoRepository.DeleteItem(path);
        var id = ToId(path);
        if (TryGetThumbnailInsideId(id, out var insideId) is false) { return; }
        using (await _fileReadWriteLock.LockAsync(CancellationToken.None))
            if (_thumbnailDb.Exists(insideId))
            {
                _thumbnailDb.Delete(insideId);
            }
    }

    public async Task DeleteAllThumbnailUnderPathAsync(string path)
    {
        _thumbnailImageInfoRepository.DeleteAllUnderPath(path);
        var id = ToId(path);
        using (await _fileReadWriteLock.LockAsync(CancellationToken.None))
        {
            foreach (var item in _thumbnailDb.Find(x => x.Id.StartsWith(id)).ToArray())
            {
                _thumbnailDb.Delete(item.Id);
            }
        }
    }


    public async Task FolderChangedAsync(string oldPath, string newPath)
    {
        using (await _fileReadWriteLock.LockAsync(CancellationToken.None))
        {
            var oldPathId = ToId(oldPath);
            if (TryGetThumbnailInsideId(oldPathId, out var insideId) is false) { return; }
            foreach (var oldPathItem in _thumbnailDb.Find(insideId).ToArray())
            {
                using (var memoryStream = _recyclableMemoryStreamManager.GetStream())
                {
                    oldPathItem.CopyTo(memoryStream);
                    _thumbnailDb.Delete(oldPathItem.Id);

                    var newPathId = oldPathItem.Id.Replace(oldPath, newPath);
                    UploadWithRetry(newPathId, oldPathItem.Filename, memoryStream);
                }
            }

        }
    }


    private ValueTask<IRandomAccessStream> GetThumbnailFromIdAsync(string itemId, CancellationToken ct)
    {
        if (TryGetThumbnailInsideId(itemId, out var insideId)
            && _thumbnailDb.Exists(insideId)
            )
        {
            var memoryStream = _recyclableMemoryStreamManager.GetStream();
            try
            {
                _thumbnailDb.Download(insideId, memoryStream);

                memoryStream.Seek(0, SeekOrigin.Begin);
                return new ValueTask<IRandomAccessStream>(memoryStream.AsRandomAccessStream());
            }
            catch
            {
                memoryStream.Dispose();
                throw;
            }
        }
        else
        {
            return new ValueTask<IRandomAccessStream>();
        }
    }


    public ValueTask<IRandomAccessStream> GetThumbnailAsync(IStorageItem storageItem, CancellationToken ct)
    {
        if (storageItem is StorageFolder folder)
        {
            return GetFolderThumbnailImageFileAsync(folder, ct);
        }
        else if (storageItem is StorageFile file)
        {
            return GetFileThumbnailImageFileAsync(file, ct);
        }
        else
        {
            throw new NotSupportedException();
        }
    }

    public async ValueTask<IRandomAccessStream> GetFolderThumbnailImageFileAsync(StorageFolder folder, CancellationToken ct)
    {
        var itemId = ToId(folder);
        if (await GetThumbnailFromIdAsync(itemId, ct) is not null and var cachedFile)
        {
            if (cachedFile is not null && cachedFile.Size > 0)
            {
                return cachedFile;
            }
        }

#if WINDOWS_UWP

        var file = await GetCoverThumbnailImageAsync(folder, ct);
        if (file == null) { return null; }
        return await GenerateThumbnailImageAsync(file, itemId, EncodingForFolderOrArchiveFileThumbnailBitmap, ct);
#else
        return null;
#endif
    }

    public async Task<IRandomAccessStream> GetFolderThumbnailImageStreamAsync(StorageFolder folder, CancellationToken ct)
    {
#if WINDOWS_UWP

        var file = await GetCoverThumbnailImageAsync(folder, ct);
        var outputStream = _recyclableMemoryStreamManager.GetStream().AsRandomAccessStream();
        try
        {
            return await GenerateThumbnailImageToStreamAsync(file, outputStream, EncodingForFolderOrArchiveFileThumbnailBitmap, ct) ? outputStream : null;
        }
        catch
        {
            outputStream.Dispose();
            throw;
        }
#else
        return null;
#endif
    }

    readonly static QueryOptions _CoverFileQueryOptions = new QueryOptions(CommonFileQuery.OrderByName, SupportedFileTypesHelper.SupportedImageFileExtensions)
    {
        FolderDepth = FolderDepth.Deep,
        ApplicationSearchFilter = "System.FileName:*cover*"
    };

    readonly static QueryOptions _AllSupportedFileQueryOptions = new QueryOptions(CommonFileQuery.OrderByName, SupportedFileTypesHelper.GetAllSupportedFileExtensions()) { FolderDepth = FolderDepth.Deep };

    private async Task<StorageFile> GetCoverThumbnailImageAsync(StorageFolder folder, CancellationToken ct)
    {
        StorageFile file = null;
        // タイトルに "cover" を含む画像を優先してサムネイルとして採用する
        var coverFileQuery = folder.CreateFileQueryWithOptions(_CoverFileQueryOptions);
        if (await coverFileQuery.GetItemCountAsync().AsTask(ct) >= 1)
        {
            var files = await coverFileQuery.GetFilesAsync(0, 1).AsTask(ct);
            file = files[0];
        }

        if (file == null)
        {
            var query = folder.CreateFileQueryWithOptions(_AllSupportedFileQueryOptions);
            var files = await query.GetFilesAsync(0, 1);
            file = files.ElementAtOrDefault(0);
        }
        return file;
    }

    public async Task<IRandomAccessStream> GetThumbnailImageFromPathAsync(string path, CancellationToken ct)
    {
        var itemId = ToId(path);
        if (await GetThumbnailFromIdAsync(itemId, ct) is not null and var cachedFile)
        {
            return cachedFile;
        }
        else
        {
            return null;
        }
    }

    public async ValueTask<IRandomAccessStream> GetFileThumbnailImageFileAsync(StorageFile file, CancellationToken ct)
    {
        var itemId = ToId(file.Path);
        if (await GetThumbnailFromIdAsync(itemId, ct) is not null and var cachedFile)
        {
            return cachedFile;
        }

        Action<BitmapDecoder, BitmapEncoder> encoderSettingMapper = SupportedFileTypesHelper.IsSupportedArchiveFileExtension(file.FileType)
            ? EncodingForFolderOrArchiveFileThumbnailBitmap
            : EncodingForImageFileThumbnailBitmap
            ;

        return await GenerateThumbnailImageAsync(file, itemId, encoderSettingMapper, ct);
    }

    public async Task<IRandomAccessStream> GetFileThumbnailImageStreamAsync(StorageFile file, CancellationToken ct)
    {
        var outputStream = _recyclableMemoryStreamManager.GetStream().AsRandomAccessStream();
        try
        {
            if (SupportedFileTypesHelper.IsSupportedArchiveFileExtension(file.FileType)
                )
            {
                return await GenerateThumbnailImageToStreamAsync(file, outputStream, EncodingForFolderOrArchiveFileThumbnailBitmap, ct) ? outputStream : null;
            }
            else
            {
                return await GenerateThumbnailImageToStreamAsync(file, outputStream, EncodingForImageFileThumbnailBitmap, ct) ? outputStream : null;
            }
        }
        catch
        {
            outputStream.Dispose();
            throw;
        }
    }

    static readonly object _lockForReadArchiveEntry = new object();

    public async ValueTask<IRandomAccessStream> GetArchiveEntryThumbnailImageFileAsync(StorageFile sourceFile, IArchiveEntry archiveEntry, CancellationToken ct)
    {
        var path = GetArchiveEntryPath(sourceFile, archiveEntry);
        var itemId = ToId(path);
        if (await GetThumbnailFromIdAsync(itemId, ct) is not null and var cachedFile)
        {
            return cachedFile;
        }

        if (archiveEntry.IsDirectory) { return null; }

        var outputStream = _recyclableMemoryStreamManager.GetStream();
        var outputRas = outputStream.AsRandomAccessStream();
        try
        {
            using (var memoryStream = _recyclableMemoryStreamManager.GetStream())
            {
                // アーカイブファイル内のシーク制御を確実に同期的に行わせるために別途ロックを仕掛ける                    
                lock (_lockForReadArchiveEntry)
                    using (var entryStream = archiveEntry.OpenEntryStream())
                    {
                        entryStream.CopyTo(memoryStream);
                        memoryStream.Seek(0, SeekOrigin.Begin);

                        ct.ThrowIfCancellationRequested();
                    }

                await TranscodeThumbnailImageToStreamAsync(path, memoryStream.AsRandomAccessStream(), outputRas, archiveEntry.IsDirectory ? EncodingForFolderOrArchiveFileThumbnailBitmap : EncodingForImageFileThumbnailBitmap, ct);
                outputRas.Seek(0);

                UploadWithRetry(itemId, Path.GetFileName(path), outputStream);

                outputRas.Seek(0);
                return outputRas;
            }
        }
        catch
        {
            outputStream.Dispose();
            throw;
        }
    }

    public async Task<IRandomAccessStream> GetArchiveEntryThumbnailImageStreamAsync(StorageFile sourceFile, IArchiveEntry archiveEntry, CancellationToken ct)
    {
        var path = GetArchiveEntryPath(sourceFile, archiveEntry);
        var itemId = ToId(path);
        if (await GetThumbnailFromIdAsync(itemId, ct) is not null and var cachedFile)
        {
            return cachedFile;
        }

        if (archiveEntry.IsDirectory) { return null; }

        var outputStream = _recyclableMemoryStreamManager.GetStream().AsRandomAccessStream();
        try
        {
            using (var memoryStream = _recyclableMemoryStreamManager.GetStream())
            {
                // アーカイブファイル内のシーク制御を確実に同期的に行わせるために別途ロックを仕掛ける
                lock (_lockForReadArchiveEntry)
                    using (var entryStream = archiveEntry.OpenEntryStream())
                    {
                        entryStream.CopyTo(memoryStream);
                        memoryStream.Seek(0, SeekOrigin.Begin);

                        ct.ThrowIfCancellationRequested();
                    }

                await TranscodeThumbnailImageToStreamAsync(path, memoryStream.AsRandomAccessStream(), outputStream, archiveEntry.IsDirectory ? EncodingForFolderOrArchiveFileThumbnailBitmap : EncodingForImageFileThumbnailBitmap, ct);
            }
            return outputStream;
        }
        catch
        {
            outputStream.Dispose();
            throw;
        }
    }

    public async ValueTask<IRandomAccessStream> GetPdfPageThumbnailImageFileAsync(StorageFile sourceFile, PdfPage pdfPage, CancellationToken ct)
    {
        var path = GetArchiveEntryPath(sourceFile, pdfPage);
        var itemId = ToId(path);
        if (await GetThumbnailFromIdAsync(itemId, ct) is not null and var cachedFile)
        {
            return cachedFile;
        }

        var outputStream = _recyclableMemoryStreamManager.GetStream();
        try
        {
            using (var memoryStream = _recyclableMemoryStreamManager.GetStream())
            {
                var ras = memoryStream.AsRandomAccessStream();
                using (await _fileReadWriteLock.LockAsync(ct))
                {
                    await pdfPage.RenderToStreamAsync(ras).AsTask(ct);
                    ras.Seek(0);

                    ct.ThrowIfCancellationRequested();
                }

                await TranscodeThumbnailImageToStreamAsync(path, ras, outputStream.AsRandomAccessStream(), EncodingForImageFileThumbnailBitmap, ct);

                UploadWithRetry(itemId, Path.GetFileName(path), outputStream);
            }

            outputStream.Seek(0, SeekOrigin.Begin);
            return outputStream.AsRandomAccessStream();
        }
        catch
        {
            outputStream.Dispose();
            throw;
        }
    }

    public async Task<IRandomAccessStream> GetPdfPageThumbnailImageStreamAsync(StorageFile sourceFile, PdfPage pdfPage, CancellationToken ct)
    {
        var path = GetArchiveEntryPath(sourceFile, pdfPage);

        var outputStream = _recyclableMemoryStreamManager.GetStream().AsRandomAccessStream();
        try
        {
            using (var memoryStream = _recyclableMemoryStreamManager.GetStream())
            using (await _fileReadWriteLock.LockAsync(ct))
            {
                var ras = memoryStream.AsRandomAccessStream();
                await pdfPage.RenderToStreamAsync(ras).AsTask(ct);
                ras.Seek(0);

                ct.ThrowIfCancellationRequested();

                await TranscodeThumbnailImageToStreamAsync(path, ras, outputStream, EncodingForImageFileThumbnailBitmap, ct);
            }

            return outputStream;
        }
        catch
        {
            outputStream.Dispose();
            throw;
        }
    }

    private async Task<IRandomAccessStream> GenerateThumbnailImageAsync(StorageFile file, string itemId, Action<BitmapDecoder, BitmapEncoder> setupEncoder, CancellationToken ct)
    {
        bool result = false;
        var memoryStream = _recyclableMemoryStreamManager.GetStream();
        var ras = memoryStream.AsRandomAccessStream();
        try
        {
            result = await GenerateThumbnailImageToStreamAsync(file, ras, setupEncoder, ct);

            if (result is false) { return null; }

            ras.Seek(0);

            UploadWithRetry(itemId, file.Name, memoryStream);

            ras.Seek(0);
            return ras;
        }
        catch
        {
            memoryStream.Dispose();
            throw;
        }
    }

    private async Task<bool> GenerateThumbnailImageToStreamAsync(StorageFile file, IRandomAccessStream outputStream, Action<BitmapDecoder, BitmapEncoder> setupEncoder, CancellationToken ct)
    {
        var (result, stream) = await (file.FileType switch
        {
            SupportedFileTypesHelper.ZipFileType => ZipFileThumbnailImageWriteToStreamAsync(file, ct),
            SupportedFileTypesHelper.RarFileType => RarFileThumbnailImageWriteToStreamAsync(file, ct),
            SupportedFileTypesHelper.PdfFileType => PdfFileThumbnailImageWriteToStreamAsync(file, ct),
            SupportedFileTypesHelper.CbzFileType => ZipFileThumbnailImageWriteToStreamAsync(file, ct),
            SupportedFileTypesHelper.CbrFileType => RarFileThumbnailImageWriteToStreamAsync(file, ct),
            SupportedFileTypesHelper.SevenZipFileType => SevenZipFileThumbnailImageWriteToStreamAsync(file, ct),
            SupportedFileTypesHelper.Cb7FileType => SevenZipFileThumbnailImageWriteToStreamAsync(file, ct),
            SupportedFileTypesHelper.TarFileType => TarFileThumbnailImageWriteToStreamAsync(file, ct),


            SupportedFileTypesHelper.JpgFileType => ImageFileThumbnailImageWriteToStreamAsync(file, ct),
            SupportedFileTypesHelper.JpegFileType => ImageFileThumbnailImageWriteToStreamAsync(file, ct),
            SupportedFileTypesHelper.JfifFileType => ImageFileThumbnailImageWriteToStreamAsync(file, ct),
            SupportedFileTypesHelper.PngFileType => ImageFileThumbnailImageWriteToStreamAsync(file, ct),
            SupportedFileTypesHelper.BmpFileType => ImageFileThumbnailImageWriteToStreamAsync(file, ct),
            SupportedFileTypesHelper.GifFileType => ImageFileThumbnailImageWriteToStreamAsync(file, ct),
            SupportedFileTypesHelper.TifFileType => ImageFileThumbnailImageWriteToStreamAsync(file, ct),
            SupportedFileTypesHelper.TiffFileType => ImageFileThumbnailImageWriteToStreamAsync(file, ct),
            SupportedFileTypesHelper.SvgFileType => ImageFileThumbnailImageWriteToStreamAsync(file, ct),
            SupportedFileTypesHelper.WebpFileType => ImageFileThumbnailImageWriteToStreamAsync(file, ct),
            SupportedFileTypesHelper.AvifFileType => ImageFileThumbnailImageWriteToStreamAsync(file, ct),
            SupportedFileTypesHelper.JpegXRFileType => ImageFileThumbnailImageWriteToStreamAsync(file, ct),
            SupportedFileTypesHelper.EPubFileType => EPubFileThubnailImageWriteToStreamAsync(file, ct),
            _ => throw new NotSupportedException(file.FileType)
        });
        using (stream)
        {
            if (!result || stream.Size == 0) { return false; }

            ct.ThrowIfCancellationRequested();
            await TranscodeThumbnailImageToStreamAsync(file.Path, stream, outputStream, setupEncoder, ct);
            return true;
        }
    }

    // see@ https://docs.microsoft.com/ja-jp/windows/win32/wic/jpeg-xr-codec        
    static readonly BitmapPropertySet _jpegPropertySet = new BitmapPropertySet()
    {
        { "ImageQuality", new BitmapTypedValue(0.8d, Windows.Foundation.PropertyType.Single) },
    };

    private Task TranscodeThumbnailImageToStreamAsync(string path, IRandomAccessStream stream, IRandomAccessStream outputStream, Action<BitmapDecoder, BitmapEncoder> setupEncoder, CancellationToken ct)
    {
        return TranscodeAsync(path, stream, BitmapEncoder.JpegXREncoderId, _jpegPropertySet, outputStream, setupEncoder, ct);
    }

    private async Task TranscodeAsync(string path, IRandomAccessStream stream, Guid encoderId, BitmapPropertySet propertySet, IRandomAccessStream outputStream, Action<BitmapDecoder, BitmapEncoder> setupEncoder, CancellationToken ct)
    {
        // implement ref@ https://gist.github.com/alexsorokoletov/71431e403c0fa55f1b4c942845a3c850
                
        try
        {
            var decoder = await BitmapDecoder.CreateAsync(stream);

            // サムネイルサイズ情報を記録
            _thumbnailImageInfoRepository.UpdateItem(new ThumbnailImageInfo()
            {
                Path = path,
                ImageWidth = decoder.PixelWidth,
                ImageHeight = decoder.PixelHeight,
                RatioWH = decoder.PixelWidth / (float)decoder.PixelHeight
            });

            var pixelData = await decoder.GetPixelDataAsync();
            var detachedPixelData = pixelData.DetachPixelData();
            pixelData = null;

            outputStream.Size = 0;

            // Note: outputStreamが AsRandomAccessStream() を通しているとハングアップする？
            var encoder = await BitmapEncoder.CreateAsync(encoderId, outputStream, propertySet);

            setupEncoder(decoder, encoder);

            Debug.WriteLine($"thumb out <{path}> size: w= {encoder.BitmapTransform.ScaledWidth} h= {encoder.BitmapTransform.ScaledHeight}");
            encoder.SetPixelData(decoder.BitmapPixelFormat, decoder.BitmapAlphaMode, decoder.OrientedPixelWidth, decoder.OrientedPixelHeight, decoder.DpiX, decoder.DpiY, detachedPixelData);

            await encoder.FlushAsync().AsTask(ct);
            await outputStream.FlushAsync().AsTask(ct);
        }
        catch (Exception ex) when (ex.HResult == -1072868846)
        {
            throw new NotSupportedImageFormatException(Path.GetExtension(path));
        }
    }

    private async Task<(bool, IRandomAccessStream)> ImageFileThumbnailImageWriteToStreamAsync(StorageFile file, CancellationToken ct)
    {
        try
        {
            return (true, await FileRandomAccessStream.OpenAsync(file.Path, FileAccessMode.Read));
        }
        catch
        {
            return (false, null);
        }
        /*
        using (var fileStream = await file.OpenReadAsync())
        {
            await RandomAccessStream.CopyAsync(fileStream, outputStream.AsOutputStream()).AsTask(ct);
            outputStream.Seek(0, SeekOrigin.Begin);
            return true;
        }
        */
    }
    private async Task<(bool, IRandomAccessStream)> ZipFileThumbnailImageWriteToStreamAsync(StorageFile file, CancellationToken ct)
    {
        using (var archiveStream = (await file.OpenReadAsync().AsTask(ct)).AsStreamForRead())
        using (var zipArchive = new ZipArchive(archiveStream))
        {
            ct.ThrowIfCancellationRequested();

            ZipArchiveEntry entry = null;
            if (GetTitlePriorityRegex() is not null and Regex regex)
            {
                entry = zipArchive.Entries.FirstOrDefault(x => regex.IsMatch(x.Name));
            }

            entry ??= zipArchive.Entries.FirstOrDefault(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Name));

            if (entry == null) { return (false, default); }

            var memoryStream = _recyclableMemoryStreamManager.GetStream().AsRandomAccessStream();
            using (var inputStream = entry.Open())
            {
                await RandomAccessStream.CopyAsync(inputStream.AsInputStream(), memoryStream);
                memoryStream.Seek(0);
            }

            return (true, memoryStream);
        }
    }

    private async Task<(bool, IRandomAccessStream)> RarFileThumbnailImageWriteToStreamAsync(StorageFile file, CancellationToken ct)
    {
        using (var archiveStream = (await file.OpenReadAsync().AsTask(ct)).AsStreamForRead())
        using (var rarArchive = RarArchive.Open(archiveStream))
        {
            RarArchiveEntry entry = null;
            if (GetTitlePriorityRegex() is not null and Regex regex)
            {
                entry = rarArchive.Entries.FirstOrDefault(x => regex.IsMatch(x.Key));
            }

            entry ??= rarArchive.Entries.FirstOrDefault(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Key));

            if (entry == null) { return default; }

            var memoryStream = _recyclableMemoryStreamManager.GetStream().AsRandomAccessStream();
            using (var inputStream = entry.OpenEntryStream())
            {
                await RandomAccessStream.CopyAsync(inputStream.AsInputStream(), memoryStream);
                memoryStream.Seek(0);
            }

            return (true, memoryStream);
        }
    }

    private async Task<(bool, IRandomAccessStream)> SevenZipFileThumbnailImageWriteToStreamAsync(StorageFile file, CancellationToken ct)
    {
        using (var archiveStream = (await file.OpenReadAsync().AsTask(ct)).AsStreamForRead())
        using (var archive = SevenZipArchive.Open(archiveStream))
        {
            SevenZipArchiveEntry entry = null;
            if (GetTitlePriorityRegex() is not null and Regex regex)
            {
                entry = archive.Entries.FirstOrDefault(x => regex.IsMatch(x.Key));
            }

            entry ??= archive.Entries.FirstOrDefault(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Key));

            if (entry == null) { return default; }

            var memoryStream = _recyclableMemoryStreamManager.GetStream().AsRandomAccessStream();
            using (var inputStream = entry.OpenEntryStream())
            {
                await RandomAccessStream.CopyAsync(inputStream.AsInputStream(), memoryStream);
                memoryStream.Seek(0);
            }

            return (true, memoryStream);
        }
    }

    private async Task<(bool, IRandomAccessStream)> TarFileThumbnailImageWriteToStreamAsync(StorageFile file, CancellationToken ct)
    {
        using (var archiveStream = (await file.OpenReadAsync().AsTask(ct)).AsStreamForRead())
        using (var archive = TarArchive.Open(archiveStream))
        {
            TarArchiveEntry entry = null;
            if (GetTitlePriorityRegex() is not null and Regex regex)
            {
                entry = archive.Entries.FirstOrDefault(x => regex.IsMatch(x.Key));
            }

            entry ??= archive.Entries.FirstOrDefault(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Key));

            if (entry == null) { return default; }

            var memoryStream = _recyclableMemoryStreamManager.GetStream().AsRandomAccessStream();
            using (var inputStream = entry.OpenEntryStream())
            {
                await RandomAccessStream.CopyAsync(inputStream.AsInputStream(), memoryStream);
                memoryStream.Seek(0);
            }

            return (true, memoryStream);
        }
    }

    private async Task<(bool, IRandomAccessStream)> PdfFileThumbnailImageWriteToStreamAsync(StorageFile file, CancellationToken ct)
    {
        var pdfDocument = await PdfDocument.LoadFromFileAsync(file).AsTask(ct);
        if (pdfDocument.PageCount == 0) { return default; }

        var memoryStream = _recyclableMemoryStreamManager.GetStream().AsRandomAccessStream();
        using var page = pdfDocument.GetPage(0);
        await page.RenderToStreamAsync(memoryStream).AsTask(ct);
        return (true, memoryStream);
    }

    private async Task<(bool, IRandomAccessStream)> EPubFileThubnailImageWriteToStreamAsync(StorageFile file, CancellationToken ct)
    {
        using var fileStream = (await file.OpenReadAsync().AsTask(ct)).AsStreamForRead();

        var epubBook = await EpubReader.OpenBookAsync(fileStream);

        var memoryStream = _recyclableMemoryStreamManager.GetStream();
        if (await epubBook.ReadCoverAsync() is not null and var cover)
        {
            await memoryStream.WriteAsync(cover, 0, cover.Length);
            return (true, memoryStream.AsRandomAccessStream());
        }
        else if (epubBook.Content.Images.Any())
        {
            var firstImage = epubBook.Content.Images.First().Value;
            var bytes = await firstImage.ReadContentAsync();
            await memoryStream.WriteAsync(bytes, 0, bytes.Length);
            return (true, memoryStream.AsRandomAccessStream());
        }
        else
        {
            return default;
        }
    }

    #region Thumbnail Size

    public ThumbnailSize? GetThumbnailOriginalSize(IStorageItem file)
    {
        return _thumbnailImageInfoRepository.TryGetSize(file.Path);
    }

    public ThumbnailSize? GetThumbnailOriginalSize(string path)
    {
        return _thumbnailImageInfoRepository.TryGetSize(path);
    }

    public ThumbnailSize? GetThumbnailOriginalSize(StorageFile file, IArchiveEntry archiveEntry)
    {
        return _thumbnailImageInfoRepository.TryGetSize(GetArchiveEntryPath(file, archiveEntry));
    }

    public ThumbnailSize? GetThumbnailOriginalSize(StorageFile file, PdfPage pdfPage)
    {
        return _thumbnailImageInfoRepository.TryGetSize(GetArchiveEntryPath(file, pdfPage));
    }


    public ThumbnailSize SetThumbnailSize(string path, BitmapImage image)
    {
        var item = _thumbnailImageInfoRepository.UpdateItem(new ThumbnailImageInfo()
        {
            Path = path,
            ImageHeight = (uint)image.PixelHeight,
            ImageWidth = (uint)image.PixelWidth,
            RatioWH = image.PixelWidth / (float)image.PixelHeight
        });

        return new ThumbnailSize()
        {
            Height = item.ImageHeight,
            Width = item.ImageWidth,
            RatioWH = item.RatioWH,
        };
    }

    public class ThumbnailImageInfo
    {
        [BsonId]
        public string Path { get; set; }

        [BsonField]
        public uint ImageWidth { get; set; }

        [BsonField]
        public uint ImageHeight { get; set; }

        [BsonField]
        public float RatioWH { get; set; }
    }

    public class ThumbnailImageInfoRepository : LiteDBServiceBase<ThumbnailImageInfo>
    {
        public ThumbnailImageInfoRepository(ILiteDatabase liteDatabase) : base(liteDatabase)
        {

        }


        public ThumbnailSize? TryGetSize(string path)
        {
            var thumbInfo = _collection.FindById(path);

            if (thumbInfo is not null)
            {
                if (thumbInfo.RatioWH == 0)
                {
                    thumbInfo.RatioWH = thumbInfo.ImageWidth / (float)thumbInfo.ImageHeight;
                    _collection.Update(thumbInfo);
                }

                return new ThumbnailSize()
                {
                    Width = thumbInfo.ImageWidth,
                    Height = thumbInfo.ImageHeight,
                    RatioWH = thumbInfo.RatioWH,
                };
            }
            else
            {
                return default;
            }
        }

        public ThumbnailSize GetSize(string path)
        {
            return TryGetSize(path) ?? throw new InvalidOperationException();
        }


        public int DeleteAll()
        {
            return _collection.DeleteAll();
        }

        public int DeleteAllUnderPath(string path)
        {
            return _collection.DeleteMany(x => path.StartsWith(x.Path));
        }
    }


    #endregion


    #region Secondary Tile

    public const string SecondaryTileThumbnailSaveFolderName = "SecondaryTile";
    static StorageFolder _SecondaryTileThumbnailFolder;
    public static async ValueTask<StorageFolder> GetSecondaryTileThumbnailFolderAsync()
    {
        return _SecondaryTileThumbnailFolder ??= await ApplicationData.Current.LocalFolder.CreateFolderAsync(SecondaryTileThumbnailSaveFolderName, CreationCollisionOption.OpenIfExists);
    }


    public async Task SecondaryThumbnailDeleteNotExist(IEnumerable<string> tileIdList)
    {
        await Task.Run(async () =>
        {
            var thumbnailFolder = await GetSecondaryTileThumbnailFolderAsync();
            var existTileIdHashSet = tileIdList.ToHashSet();

            var folders = await thumbnailFolder.GetFoldersAsync();
            var deletedTileFolders = folders.Where(x => existTileIdHashSet.Contains(x.Name) is false);
            foreach (var folder in deletedTileFolders)
            {
                await folder.DeleteAsync(StorageDeleteOption.PermanentDelete);
                Debug.WriteLine("delete secondary tile thumbnail: " + folder.Name);
            }
        });
    }

    public Task<GenerateSecondaryTileThumbnailResult> GenerateSecondaryThumbnailImageAsync(IStorageItem storageItem, string tileId, CancellationToken ct)
    {
        if (storageItem is StorageFolder folder)
        {
            return GenerateSecondaryThumbnailImageAsync(folder, tileId, ct);
        }
        else if (storageItem is StorageFile file)
        {
            return GenerateSecondaryThumbnailImageAsync(file, tileId, ct);
        }
        else
        {
            throw new NotSupportedException();
        }
    }

    public async Task<GenerateSecondaryTileThumbnailResult> GenerateSecondaryThumbnailImageAsync(StorageFolder folder, string tileId, CancellationToken ct)
    {
#if WINDOWS_UWP
        var query = folder.CreateFileQueryWithOptions(new QueryOptions(CommonFileQuery.OrderByName, SupportedFileTypesHelper.GetAllSupportedFileExtensions()) { FolderDepth = FolderDepth.Deep });
        var count = await query.GetItemCountAsync().AsTask(ct);

        if (count == 0) { return null; }

        var files = await query.GetFilesAsync(0, 1).AsTask(ct);
        return await GenerateSecondaryThumbnailImageAsync(files[0], tileId, ct);
#endif
    }

    private async Task<GenerateSecondaryTileThumbnailResult> GenerateSecondaryThumbnailImageAsync(StorageFile file, string tileId, CancellationToken ct)
    {
        var thumbnailFolder = await GetSecondaryTileThumbnailFolderAsync();
        var itemFolder = await thumbnailFolder.CreateFolderAsync(tileId, CreationCollisionOption.ReplaceExisting);
        var wideThumbFile = await itemFolder.CreateFileAsync("thumb310x150.png", CreationCollisionOption.ReplaceExisting);
        var square310ThumbFile = await itemFolder.CreateFileAsync("thumb310x310.png", CreationCollisionOption.ReplaceExisting);
        var square150ThumbFile = await itemFolder.CreateFileAsync("thumb150x150.png", CreationCollisionOption.ReplaceExisting);

        try
        {
            //                
            var (result, stream) = await (file.FileType switch
            {
                SupportedFileTypesHelper.ZipFileType => ZipFileThumbnailImageWriteToStreamAsync(file, ct),
                SupportedFileTypesHelper.RarFileType => RarFileThumbnailImageWriteToStreamAsync(file, ct),
                SupportedFileTypesHelper.PdfFileType => PdfFileThumbnailImageWriteToStreamAsync(file, ct),
                SupportedFileTypesHelper.JpgFileType => ImageFileThumbnailImageWriteToStreamAsync(file, ct),
                SupportedFileTypesHelper.JpegFileType => ImageFileThumbnailImageWriteToStreamAsync(file, ct),
                SupportedFileTypesHelper.JfifFileType => ImageFileThumbnailImageWriteToStreamAsync(file, ct),
                SupportedFileTypesHelper.PngFileType => ImageFileThumbnailImageWriteToStreamAsync(file, ct),
                SupportedFileTypesHelper.BmpFileType => ImageFileThumbnailImageWriteToStreamAsync(file, ct),
                SupportedFileTypesHelper.GifFileType => ImageFileThumbnailImageWriteToStreamAsync(file, ct),
                SupportedFileTypesHelper.TifFileType => ImageFileThumbnailImageWriteToStreamAsync(file, ct),
                SupportedFileTypesHelper.TiffFileType => ImageFileThumbnailImageWriteToStreamAsync(file, ct),
                SupportedFileTypesHelper.SvgFileType => ImageFileThumbnailImageWriteToStreamAsync(file, ct),
                SupportedFileTypesHelper.WebpFileType => ImageFileThumbnailImageWriteToStreamAsync(file, ct),
                SupportedFileTypesHelper.AvifFileType => ImageFileThumbnailImageWriteToStreamAsync(file, ct),
                SupportedFileTypesHelper.JpegXRFileType => ImageFileThumbnailImageWriteToStreamAsync(file, ct),
                SupportedFileTypesHelper.EPubFileType => EPubFileThubnailImageWriteToStreamAsync(file, ct),
                _ => throw new NotSupportedException(file.FileType)
            });

            if (!result) { return null; }
            using (stream)
            {
                (StorageFile file, int width, int height)[] items = new[]
                {
                    (wideThumbFile, 310, 150),
                    (square310ThumbFile, 310, 310),
                    (square150ThumbFile, 150, 150),
                };

                var decoder = await BitmapDecoder.CreateAsync(stream);
                using (var memStream = _recyclableMemoryStreamManager.GetStream().AsRandomAccessStream())
                {
                    foreach (var item in items)
                    {
                        memStream.Seek(0);
                        var encoder = await BitmapEncoder.CreateForTranscodingAsync(memStream, decoder);
                        encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
                        if (decoder.PixelWidth < decoder.PixelHeight)
                        {
                            // 幅に合わせて高さをスケールさせる
                            // 縦長の場合に使用
                            var ratio = (float)item.width / decoder.PixelWidth;
                            encoder.BitmapTransform.ScaledWidth = (uint)item.width;
                            encoder.BitmapTransform.ScaledHeight = (uint)(decoder.PixelHeight * ratio);
                            // 一部で失敗するケースがあったのでコメントアウト
                            //encoder.BitmapTransform.Bounds = new BitmapBounds() { X = 0, Y = 0, Width = (uint)item.width, Height = (uint)item.height };
                        }
                        else
                        {
                            // 高さに合わせて幅をスケールさせる
                            // 横長の場合に使用
                            var ratio = (float)item.height / decoder.PixelHeight;
                            encoder.BitmapTransform.ScaledWidth = (uint)(decoder.PixelWidth * ratio);
                            encoder.BitmapTransform.ScaledHeight = (uint)item.height;
                            // 一部で失敗するケースがあったのでコメントアウト
                            //encoder.BitmapTransform.Bounds = new BitmapBounds() { X = 0, Y = 0, Width = (uint)item.width, Height = (uint)item.height };
                        }
                        await encoder.FlushAsync();
                        memStream.Seek(0);
                        using (var fileStream = await item.file.OpenAsync(FileAccessMode.ReadWrite))
                        {
                            await RandomAccessStream.CopyAsync(memStream, fileStream);
                        }
                    }
                }

                return new GenerateSecondaryTileThumbnailResult()
                {
                    Wide310x150Logo = wideThumbFile,
                    Square310x310Logo = square310ThumbFile,
                    Square150x150Logo = square150ThumbFile,
                };
            }
        }
        catch
        {
            await itemFolder.DeleteAsync(StorageDeleteOption.PermanentDelete);
            throw;
        }
    }

    #endregion
}
