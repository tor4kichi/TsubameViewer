using CommunityToolkit.Diagnostics;
using FFmpegInteropX;
using LiteDB;
using Microsoft.Graphics.Canvas;
using Microsoft.IO;
using Reactive.Bindings;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Tar;
using SharpCompress.Common;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.Xz;
using SharpCompress.Readers;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.IO.Pipes;
using System.Linq;
using System.Numerics;
using System.Reactive.Disposables;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Contracts.Maintenance;
using TsubameViewer.Core.Contracts.Models;
using TsubameViewer.Core.Infrastructure;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using TsubameViewer.Core.Models.SourceFolders;
using VersOne.Epub;
using Windows.Foundation;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Media.Core;
using Windows.Media.Editing;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;


#nullable enable
namespace TsubameViewer.Core.Models.FolderItemListing;


public struct ThumbnailSize
{
    public uint Width { get; set; }
    public uint Height { get; set; }
    public float RatioWH { get; set; }
}

public sealed class ThumbnailImageManager 
    : ISecondaryTileThumbnailImageService
    , IThumbnailImageMaintenanceService
{
    private readonly ILiteDatabase _temporaryDb;
    private readonly ILiteCollection<ThumbnailItemIdEntry> _thumbnailIdDb;
    private readonly ILiteStorage<string> _thumbnailDb;
    private readonly FolderListingSettings _folderListingSettings;
    private readonly ThumbnailImageInfoRepository _thumbnailImageInfoRepository;
    private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
    private readonly static AsyncLock _fileReadWriteLock = new(Math.Max(1, Environment.ProcessorCount));
    private readonly RecyclableMemoryStreamManager _recyclableMemoryStreamManager;
    private readonly ILiteCollection<ThumbnailGenerationIssueEntry> _thumnailGenerationIssueCollection;

    private string GetArchiveEntryPath(StorageFile file, IArchiveEntry entry)
    {
        return Path.Combine(file.Path, entry?.Key ?? "_");
    }

    Regex? _titlePriorityRegex;
    string? _lasttitlePriorityRegexText = null;
    Regex? GetTitlePriorityRegex()
    {
        if (_titlePriorityRegex != null)
        {
            if (_lasttitlePriorityRegexText != _folderListingSettings.ThumbnailPriorityTitleRegex)
            {
                _titlePriorityRegex = null;
            }
        }

        try
        {
            _titlePriorityRegex ??= new Regex(_folderListingSettings.ThumbnailPriorityTitleRegex);
        }
        catch { }

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
            encoder.BitmapTransform.ScaledHeight = (uint)Math.Floor(decoder.PixelHeight * ratio * _folderListingSettings.FolderItemThumbnailQuality);
            encoder.BitmapTransform.ScaledWidth = (uint)(_folderListingSettings.FolderItemThumbnailImageSize.Width * _folderListingSettings.FolderItemThumbnailQuality);
        }
        else
        {
            var ratio = _folderListingSettings.FolderItemThumbnailImageSize.Height / decoder.PixelHeight;
            encoder.BitmapTransform.ScaledWidth = (uint)Math.Floor(decoder.PixelWidth * ratio * _folderListingSettings.FolderItemThumbnailQuality);
            encoder.BitmapTransform.ScaledHeight = (uint)(_folderListingSettings.FolderItemThumbnailImageSize.Height * _folderListingSettings.FolderItemThumbnailQuality);
        }
        //encoder.BitmapTransform.Bounds = new BitmapBounds() { X = 0, Y = 0, Height = encoder.BitmapTransform.ScaledHeight, Width = encoder.BitmapTransform.ScaledWidth };
        encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
    }

    private void EncodingForImageFileThumbnailBitmap(BitmapDecoder decoder, BitmapEncoder encoder)
    {
        // 縦横比を維持したまま 高さ = LargeFileThumbnailImageHeight になるようにスケーリング
        var ratio = (double)_folderListingSettings.FolderItemThumbnailImageSize.Height / decoder.PixelHeight;
        encoder.BitmapTransform.ScaledWidth = (uint)Math.Floor(decoder.PixelWidth * ratio * _folderListingSettings.FolderItemThumbnailQuality);
        encoder.BitmapTransform.ScaledHeight = (uint)(_folderListingSettings.FolderItemThumbnailImageSize.Height * _folderListingSettings.FolderItemThumbnailQuality);
        encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
    }

    private SizeF CulcThumbnailSize(int width, int height)
    {
        var ratio = (double)_folderListingSettings.FolderItemThumbnailImageSize.Height / height;
        return new (MathF.Floor(width * (float)ratio * _folderListingSettings.FolderItemThumbnailQuality), (float)_folderListingSettings.FolderItemThumbnailImageSize.Height * _folderListingSettings.FolderItemThumbnailQuality);
    }

    class ThumbnailItemIdEntry
    {
        [BsonId]
        public string Id { get; set; } = "";

        public string InsideId { get; set; } = "";
    }

    public ThumbnailImageManager(
        ILiteDatabase temporaryDb,
        FolderListingSettings folderListingSettings,
        SourceStorageItemsRepository sourceStorageItemsRepository
        )
    {
        _temporaryDb = temporaryDb;
        _thumbnailIdDb = _temporaryDb.GetCollection<ThumbnailItemIdEntry>();
        _thumbnailDb = _temporaryDb.FileStorage;
        _folderListingSettings = folderListingSettings;
        _thumbnailImageInfoRepository = new ThumbnailImageInfoRepository(temporaryDb);
        _sourceStorageItemsRepository = sourceStorageItemsRepository;
        _recyclableMemoryStreamManager = new RecyclableMemoryStreamManager();
        _thumnailGenerationIssueCollection = _temporaryDb.GetCollection<ThumbnailGenerationIssueEntry>();        

        _canvasDevice = new CanvasDevice();
    }

    private readonly CanvasDevice _canvasDevice;

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
    public async ValueTask<Stream?> GetThumbnailImageStreamAsync(IImageSource imageSource, Stream? outputStream = null, CancellationToken ct = default)
    {
        var itemId = GetId(imageSource);
        if (await GetThumbnailFromIdAsync(itemId, ct) is not null and var cachedImageStream)
        {
            return cachedImageStream;
        }

        if (imageSource.StorageItem is StorageFolder folder)
        {
            outputStream ??= _recyclableMemoryStreamManager.GetStream();
            try
            {
                var file = await GetCoverThumbnailImageAsync(folder, ct);
                if (file != null
                    && await GenerateThumbnailImageToStreamAsync(file, outputStream, EncodingForFolderOrArchiveFileThumbnailBitmap, ct))
                {
                    UploadWithRetry(itemId, imageSource.Name, outputStream);
                    return outputStream;
                }
                else
                {
                    outputStream.Dispose();
                    return null;
                }
            }
            catch
            {
                outputStream.Dispose();
                throw;
            }
        }
        else if (imageSource is StorageItemImageSource && imageSource.StorageItem is StorageFile file 
            && (file.IsSupportedMangaFile() || file.IsSupportedEBookFile() || file.IsSupportedMovieFile()))
        {
            outputStream ??= _recyclableMemoryStreamManager.GetStream();
            try
            {
                if (await GenerateThumbnailImageToStreamAsync(file, outputStream, EncodingForFolderOrArchiveFileThumbnailBitmap, ct))
                {
                    UploadWithRetry(itemId, imageSource.Name, outputStream);
                    return outputStream;
                }
                else
                {
                    return null;
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
            outputStream ??= _recyclableMemoryStreamManager.GetStream();
            var stream = outputStream;
            if (await imageSource.TryGetSizedImageStreamAsync(200, stream, ct) is { } size)
            {
                // サムネイルサイズ情報を記録                
                _thumbnailImageInfoRepository.UpdateItem(new ThumbnailImageInfo()
                {
                    Path = ToId(imageSource.Path),
                    ImageWidth = (uint)size.Width,
                    ImageHeight = (uint)size.Height,
                    RatioWH = size.Width / size.Height
                });
                try
                {
                    UploadWithRetry(itemId, imageSource.Name, stream);
                    return outputStream;
                }
                catch
                {
                    outputStream.Dispose();
                    throw;
                }
            }
            else
            {
                try
                {
                    await TranscodeThumbnailImageToStreamAsync(imageSource.Path, async () => await imageSource.GetImageStreamAsync(ct), outputStream, EncodingForFolderOrArchiveFileThumbnailBitmap, ct);
                    UploadWithRetry(itemId, imageSource.Name, outputStream);
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
        using var stream = _recyclableMemoryStreamManager.GetStream();
        var imageMemoryStream = await GetThumbnailImageStreamAsync(childImageSource, stream, ct);
        if (imageMemoryStream == null) { return; }

        using (imageMemoryStream)
        {
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
    }

    public async ValueTask<ThumbnailSize> GetEnsureThumbnailSizeAsync(IImageSource imageSource, CancellationToken ct)
    {
        if (GetCachedThumbnailSize(imageSource) is { } sizeIfCached)
        {
            return sizeIfCached;
        }

        if (imageSource.PreCulcuratedSize is { } size)
        {
            return SetThumbnailSize(imageSource, (uint)size.Width, (uint)size.Height);
        }

        return await Task.Run(async () =>
        {            
            using (var imageStream = await imageSource.GetImageStreamAsync(ct))
            {
                var imageInfo = SKBitmap.DecodeBounds(imageStream);                
                if (imageInfo != SKImageInfo.Empty)
                {
                    return SetThumbnailSize(imageSource, (uint)imageInfo.Width, (uint)imageInfo.Height);
                }
            }
            using (var imageStream = await imageSource.GetImageStreamAsync(ct))
            {
                var decoder = await BitmapDecoder.CreateAsync(imageStream.AsRandomAccessStream()).AsTask(ct).ConfigureAwait(false);
                return SetThumbnailSize(imageSource, (uint)decoder.PixelWidth, (uint)decoder.PixelHeight);
            }
        });
    }


    public ThumbnailSize? GetCachedThumbnailSize(IImageSource imageSource)
    {
        return _thumbnailImageInfoRepository.TryGetSize(ToId(imageSource.Path));        
    }
    public ThumbnailSize? GetCachedThumbnailSize(string path)
    {
        return _thumbnailImageInfoRepository.TryGetSize(ToId(path));
    }


    public ThumbnailSize SetThumbnailSize(IImageSource imageSource, uint pixelWidth, uint pixelHeight)
    {
        var item = _thumbnailImageInfoRepository.UpdateItem(new ThumbnailImageInfo()
        {
            Path = ToId(imageSource.Path),
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

    public async Task SetThumbnailAsync(IStorageItem targetItem, Stream bitmapImage, bool requireTrancode, CancellationToken ct)
    {
        await Task.Run(async () =>
        {
            var itemId = ToId(targetItem);
            
            if (requireTrancode)
            {
                using (var memoryStream = _recyclableMemoryStreamManager.GetStream())
                {
                    await TranscodeThumbnailImageToStreamAsync(targetItem.Path, bitmapImage, memoryStream, EncodingForFolderOrArchiveFileThumbnailBitmap, ct);
                    using (await _fileReadWriteLock.LockAsync(ct))
                    {
                        UploadWithRetry(itemId, targetItem.Name, memoryStream);
                    }
                }
            }
            else
            {
                using (await _fileReadWriteLock.LockAsync(ct))
                {
                    UploadWithRetry(itemId, targetItem.Name, bitmapImage);
                }
            }
        });
    }


    public async Task SetArchiveEntryThumbnailAsync(StorageFile targetItem, IArchiveEntry entry, Stream bitmapImage, bool requireTrancode, CancellationToken ct)
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
                        await TranscodeThumbnailImageToStreamAsync(path, bitmapImage, memoryStream, EncodingForFolderOrArchiveFileThumbnailBitmap, ct);

                        memoryStream.Seek(0, SeekOrigin.Begin);
                        UploadWithRetry(itemId, targetItem.Name, memoryStream);

                        memoryStream.Seek(0, SeekOrigin.Begin);
                    }
                }
                else
                {
                    UploadWithRetry(itemId, targetItem.Name, bitmapImage);
                }
            }
        });
    }

    public async Task DeleteAllThumbnailsAsync()
    {
        _thumbnailImageInfoRepository.DeleteAll();

        using (await _fileReadWriteLock.LockAsync(CancellationToken.None))
        {
            foreach (var name in _temporaryDb.GetCollectionNames().ToList())
            {
                _temporaryDb.DropCollection(name);
            }
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
        var id = ToId(path);
        _thumbnailImageInfoRepository.DeleteAllUnderPath(id);
        using (await _fileReadWriteLock.LockAsync(CancellationToken.None))
        {
            foreach (var item in _thumbnailDb.Find(x => x.Id.StartsWith(id, StringComparison.Ordinal)).ToArray())
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
            foreach (var oldPathItem in _thumbnailDb.Find(x => x.Id.Equals(insideId, StringComparison.Ordinal)).ToArray())
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


    private ValueTask<Stream> GetThumbnailFromIdAsync(string itemId, CancellationToken ct)
    {
        if (TryGetThumbnailInsideId(itemId, out var insideId)
            && _thumbnailDb.Exists(insideId)
            )
        {
            return new (_thumbnailDb.OpenRead(insideId));
        }
        else
        {
            return new ValueTask<Stream>();
        }
    }


    public ValueTask<Stream> GetThumbnailAsync(IStorageItem storageItem, CancellationToken ct)
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

    public async ValueTask<Stream> GetFolderThumbnailImageFileAsync(StorageFolder folder, CancellationToken ct)
    {
        var itemId = ToId(folder);
        if (await GetThumbnailFromIdAsync(itemId, ct) is not null and var cachedFile)
        {
            if (cachedFile is not null && cachedFile.Length > 0)
            {
                return cachedFile;
            }
        }

#if WINDOWS_UWP

        var file = await GetCoverThumbnailImageAsync(folder, ct);
        if (file == null) { return Stream.Null; }
        return await GenerateThumbnailImageAsync(file, itemId, EncodingForFolderOrArchiveFileThumbnailBitmap, ct);
#else
        return null;
#endif
    }

    public async Task<Stream> GetFolderThumbnailImageStreamAsync(StorageFolder folder, CancellationToken ct)
    {
#if WINDOWS_UWP

        var file = await GetCoverThumbnailImageAsync(folder, ct);
        if (file == null) { return Stream.Null; }

        var outputStream = _recyclableMemoryStreamManager.GetStream();
        try
        {
            return await GenerateThumbnailImageToStreamAsync(file, outputStream, EncodingForFolderOrArchiveFileThumbnailBitmap, ct) ? outputStream : Stream.Null;
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

    readonly static QueryOptions _coverFileQueryOptions = new QueryOptions(CommonFileQuery.OrderByName, SupportedFileTypesHelper.SupportedImageFileExtensions)
    {
        FolderDepth = FolderDepth.Deep,
        ApplicationSearchFilter = "System.FileName:*cover*"
    };

    readonly static QueryOptions _allSupportedFileQueryOptions = new QueryOptions(CommonFileQuery.OrderByName, SupportedFileTypesHelper.GetAllSupportedFileExtensions()) { FolderDepth = FolderDepth.Deep };

    private async Task<StorageFile?> GetCoverThumbnailImageAsync(StorageFolder folder, CancellationToken ct)
    {
        StorageFile? file = null;
        // タイトルに "cover" を含む画像を優先してサムネイルとして採用する
        var coverFileQuery = folder.CreateFileQueryWithOptions(_coverFileQueryOptions);
        if (await coverFileQuery.GetItemCountAsync().AsTask(ct) >= 1)
        {
            var files = await coverFileQuery.GetFilesAsync(0, 1).AsTask(ct);
            file = files[0];
        }

        if (file == null)
        {
            var query = folder.CreateFileQueryWithOptions(_allSupportedFileQueryOptions);
            var files = await query.GetFilesAsync(0, 1);
            file = files.ElementAtOrDefault(0);
        }
        return file;
    }

    public async Task<Stream> GetThumbnailImageFromPathAsync(string path, CancellationToken ct)
    {
        var itemId = ToId(path);
        if (await GetThumbnailFromIdAsync(itemId, ct) is not null and var cachedFile)
        {
            return cachedFile;
        }
        else
        {
            return Stream.Null;
        }
    }

    public async ValueTask<Stream> GetFileThumbnailImageFileAsync(StorageFile file, CancellationToken ct)
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

    public async Task<Stream?> GetFileThumbnailImageStreamAsync(StorageFile file, CancellationToken ct)
    {
        var outputStream = _recyclableMemoryStreamManager.GetStream();
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

    //public async ValueTask<IRandomAccessStream> GetArchiveEntryThumbnailImageFileAsync(StorageFile sourceFile, IArchiveEntry archiveEntry, CancellationToken ct)
    //{
    //    var path = GetArchiveEntryPath(sourceFile, archiveEntry);
    //    var itemId = ToId(path);
    //    if (await GetThumbnailFromIdAsync(itemId, ct) is not null and var cachedFile)
    //    {
    //        return cachedFile;
    //    }

    //    if (archiveEntry.IsDirectory) { return null; }

    //    var outputStream = _recyclableMemoryStreamManager.GetStream();
    //    var outputRas = outputStream.AsRandomAccessStream();
    //    try
    //    {
    //        using (var memoryStream = _recyclableMemoryStreamManager.GetStream())
    //        {
    //            // アーカイブファイル内のシーク制御を確実に同期的に行わせるために別途ロックを仕掛ける                    
    //            lock (_lockForReadArchiveEntry)
    //                using (var entryStream = archiveEntry.OpenEntryStream())
    //                {
    //                    entryStream.CopyTo(memoryStream);
    //                    memoryStream.Seek(0, SeekOrigin.Begin);

    //                    ct.ThrowIfCancellationRequested();
    //                }

    //            await TranscodeThumbnailImageToStreamAsync(path, memoryStream, outputRas, archiveEntry.IsDirectory ? EncodingForFolderOrArchiveFileThumbnailBitmap : EncodingForImageFileThumbnailBitmap, ct);
    //            outputRas.Seek(0);

    //            UploadWithRetry(itemId, Path.GetFileName(path), outputStream);

    //            outputRas.Seek(0);
    //            return outputRas;
    //        }
    //    }
    //    catch
    //    {
    //        outputStream.Dispose();
    //        throw;
    //    }
    //}

    //public async Task<IRandomAccessStream> GetArchiveEntryThumbnailImageStreamAsync(StorageFile sourceFile, IArchiveEntry archiveEntry, CancellationToken ct)
    //{
    //    var path = GetArchiveEntryPath(sourceFile, archiveEntry);
    //    var itemId = ToId(path);
    //    if (await GetThumbnailFromIdAsync(itemId, ct) is not null and var cachedFile)
    //    {
    //        return cachedFile;
    //    }

    //    if (archiveEntry.IsDirectory) { return null; }

    //    var outputStream = _recyclableMemoryStreamManager.GetStream().AsRandomAccessStream();
    //    try
    //    {
    //        using (var memoryStream = _recyclableMemoryStreamManager.GetStream())
    //        {
    //            // アーカイブファイル内のシーク制御を確実に同期的に行わせるために別途ロックを仕掛ける
    //            lock (_lockForReadArchiveEntry)
    //                using (var entryStream = archiveEntry.OpenEntryStream())
    //                {
    //                    entryStream.CopyTo(memoryStream);
    //                    memoryStream.Seek(0, SeekOrigin.Begin);

    //                    ct.ThrowIfCancellationRequested();
    //                }

    //            await TranscodeThumbnailImageToStreamAsync(path, memoryStream.AsRandomAccessStream(), outputStream, archiveEntry.IsDirectory ? EncodingForFolderOrArchiveFileThumbnailBitmap : EncodingForImageFileThumbnailBitmap, ct);
    //        }
    //        return outputStream;
    //    }
    //    catch
    //    {
    //        outputStream.Dispose();
    //        throw;
    //    }
    //}

    //public async ValueTask<IRandomAccessStream> GetPdfPageThumbnailImageFileAsync(StorageFile sourceFile, PdfPage pdfPage, CancellationToken ct)
    //{
    //    var path = GetArchiveEntryPath(sourceFile, pdfPage);
    //    var itemId = ToId(path);
    //    if (await GetThumbnailFromIdAsync(itemId, ct) is not null and var cachedFile)
    //    {
    //        return cachedFile;
    //    }

    //    var outputStream = _recyclableMemoryStreamManager.GetStream();
    //    try
    //    {
    //        using (var memoryStream = _recyclableMemoryStreamManager.GetStream())
    //        {
    //            var ras = memoryStream.AsRandomAccessStream();
    //            using (await _fileReadWriteLock.LockAsync(ct))
    //            {
    //                await pdfPage.RenderToStreamAsync(ras).AsTask(ct);
    //                ras.Seek(0);

    //                ct.ThrowIfCancellationRequested();
    //            }

    //            await TranscodeThumbnailImageToStreamAsync(path, ras, outputStream.AsRandomAccessStream(), EncodingForImageFileThumbnailBitmap, ct);

    //            UploadWithRetry(itemId, Path.GetFileName(path), outputStream);
    //        }

    //        outputStream.Seek(0, SeekOrigin.Begin);
    //        return outputStream.AsRandomAccessStream();
    //    }
    //    catch
    //    {
    //        outputStream.Dispose();
    //        throw;
    //    }
    //}

    //public async Task<IRandomAccessStream> GetPdfPageThumbnailImageStreamAsync(StorageFile sourceFile, PdfPage pdfPage, CancellationToken ct)
    //{
    //    var path = GetArchiveEntryPath(sourceFile, pdfPage);

    //    var outputStream = _recyclableMemoryStreamManager.GetStream().AsRandomAccessStream();
    //    try
    //    {
    //        using (var memoryStream = _recyclableMemoryStreamManager.GetStream())
    //        {
    //            var ras = memoryStream.AsRandomAccessStream();
    //            using (await _fileReadWriteLock.LockAsync(ct))
    //            {
    //                await pdfPage.RenderToStreamAsync(ras).AsTask(ct);
    //                ras.Seek(0);

    //                ct.ThrowIfCancellationRequested();
    //            }

    //            await TranscodeThumbnailImageToStreamAsync(path, ras, outputStream, EncodingForImageFileThumbnailBitmap, ct);
    //        }

    //        return outputStream;
    //    }
    //    catch
    //    {
    //        outputStream.Dispose();
    //        throw;
    //    }
    //}

    private async ValueTask<Stream> GenerateThumbnailImageAsync(StorageFile file, string itemId, Action<BitmapDecoder, BitmapEncoder> setupEncoder, CancellationToken ct)
    {
        bool result = false;
        var memoryStream = _recyclableMemoryStreamManager.GetStream();        
        try
        {
            result = await GenerateThumbnailImageToStreamAsync(file, memoryStream, setupEncoder, ct);

            if (result is false) { return Stream.Null; }

            memoryStream.Seek(0, SeekOrigin.Begin);

            UploadWithRetry(itemId, file.Name, memoryStream);

            memoryStream.Seek(0, SeekOrigin.Begin);
            return memoryStream;
        }
        catch
        {
            memoryStream.Dispose();
            throw;
        }
    }

    private async ValueTask<bool> GenerateThumbnailImageToStreamAsync(StorageFile file, Stream outputStream, Action<BitmapDecoder, BitmapEncoder> setupEncoder, CancellationToken ct)
    {
        return await (file.FileType.ToLowerInvariant() switch
        {
            SupportedFileTypesHelper.ZipFileType => ZipFileThumbnailImageWriteToStreamAsync(file, outputStream, ct),
            SupportedFileTypesHelper.RarFileType => RarFileThumbnailImageWriteToStreamAsync(file, outputStream, ct),
            SupportedFileTypesHelper.PdfFileType => PdfFileThumbnailImageWriteToStreamAsync(file, outputStream, ct),
            SupportedFileTypesHelper.CbzFileType => ZipFileThumbnailImageWriteToStreamAsync(file, outputStream, ct),
            SupportedFileTypesHelper.CbrFileType => RarFileThumbnailImageWriteToStreamAsync(file, outputStream, ct),
            SupportedFileTypesHelper.SevenZipFileType => SevenZipFileThumbnailImageWriteToStreamAsync(file, outputStream, ct),
            SupportedFileTypesHelper.Cb7FileType => SevenZipFileThumbnailImageWriteToStreamAsync(file, outputStream, ct),
            SupportedFileTypesHelper.TarFileType => TarFileThumbnailImageWriteToStreamAsync(file, outputStream, ct),
            SupportedFileTypesHelper.JpgFileType => ImageFileThumbnailImageWriteToStreamAsync(file, outputStream, ct),
            SupportedFileTypesHelper.JpegFileType => ImageFileThumbnailImageWriteToStreamAsync(file, outputStream, ct),
            SupportedFileTypesHelper.JfifFileType => ImageFileThumbnailImageWriteToStreamAsync(file, outputStream, ct),
            SupportedFileTypesHelper.PngFileType => ImageFileThumbnailImageWriteToStreamAsync(file, outputStream, ct),
            SupportedFileTypesHelper.BmpFileType => ImageFileThumbnailImageWriteToStreamAsync(file, outputStream, ct),
            SupportedFileTypesHelper.GifFileType => ImageFileThumbnailImageWriteToStreamAsync(file, outputStream, ct),
            SupportedFileTypesHelper.TifFileType => ImageFileThumbnailImageWriteToStreamAsync(file, outputStream, ct),
            SupportedFileTypesHelper.TiffFileType => ImageFileThumbnailImageWriteToStreamAsync(file, outputStream, ct),
            SupportedFileTypesHelper.SvgFileType => ImageFileThumbnailImageWriteToStreamAsync(file, outputStream, ct),
            SupportedFileTypesHelper.WebpFileType => ImageFileThumbnailImageWriteToStreamAsync(file, outputStream, ct),
            SupportedFileTypesHelper.AvifFileType => ImageFileThumbnailImageWriteToStreamAsync(file, outputStream, ct),
            SupportedFileTypesHelper.JpegXRFileType => ImageFileThumbnailImageWriteToStreamAsync(file, outputStream, ct),
            SupportedFileTypesHelper.EPubFileType => EPubFileThubnailImageWriteToStreamAsync(file, outputStream, ct),
            SupportedFileTypesHelper.Movie_Mp4FileType=> FFMpeg_MovieFileThubnailImageWriteToStreamAsync(file, outputStream, ct),
            SupportedFileTypesHelper.Movie_WebMFileType => FFMpeg_MovieFileThubnailImageWriteToStreamAsync(file, outputStream, ct),
            SupportedFileTypesHelper.Movie_HevcFileType => FFMpeg_MovieFileThubnailImageWriteToStreamAsync(file, outputStream, ct),
            SupportedFileTypesHelper.Movie_MkvFileType => FFMpeg_MovieFileThubnailImageWriteToStreamAsync(file, outputStream, ct),
            SupportedFileTypesHelper.Movie_M4vFileType => FFMpeg_MovieFileThubnailImageWriteToStreamAsync(file, outputStream, ct),
            SupportedFileTypesHelper.Movie_MovFileType => FFMpeg_MovieFileThubnailImageWriteToStreamAsync(file, outputStream, ct),
            SupportedFileTypesHelper.Movie_TsFileType => FFMpeg_MovieFileThubnailImageWriteToStreamAsync(file, outputStream, ct),
            SupportedFileTypesHelper.Movie_MTsFileType => FFMpeg_MovieFileThubnailImageWriteToStreamAsync(file, outputStream, ct),
            SupportedFileTypesHelper.Movie_M2TsFileType => FFMpeg_MovieFileThubnailImageWriteToStreamAsync(file, outputStream, ct),
            SupportedFileTypesHelper.Movie_AviFileType => FFMpeg_MovieFileThubnailImageWriteToStreamAsync(file, outputStream, ct),
            SupportedFileTypesHelper.Movie_WmvFileType => FFMpeg_MovieFileThubnailImageWriteToStreamAsync(file, outputStream, ct),
            _ => throw new NotSupportedException(file.FileType)
        });
    }

    // see@ https://docs.microsoft.com/ja-jp/windows/win32/wic/jpeg-xr-codec        
    static readonly BitmapPropertySet _jpegPropertySet = new BitmapPropertySet()
    {
        { "ImageQuality", new BitmapTypedValue(0.8d, Windows.Foundation.PropertyType.Single) },
    };

    static AsyncLock _renderLock = new AsyncLock();
    private async ValueTask TranscodeThumbnailImageToStreamAsync(string path, Func<ValueTask<Stream>> streamOpener, Stream outputStream, Action<BitmapDecoder, BitmapEncoder> setupEncoder, CancellationToken ct)
    {
        try
        {
            if (path.EndsWith(".gif"))
            {
                using (var inputStream = await streamOpener())
                {
                    inputStream.CopyTo(outputStream);
                    inputStream.Seek(0, SeekOrigin.Begin);
                    var imageInfo = SKBitmap.DecodeBounds(inputStream);
                    if (imageInfo != SKImageInfo.Empty)
                    {
                        SetThumbnailSize(path, imageInfo);
                    }
                }
            }
            else
            {
                using (var inputStream = await streamOpener())
                {
                    if (_folderListingSettings.ThumbnailDecodeType == ThumbnailDecodeMethod.Skia)
                    {
                        if (inputStream.CanSeek)
                        {
                            await TranscodeSkiaAsync(path, inputStream, BitmapEncoder.JpegXREncoderId, _jpegPropertySet, outputStream, setupEncoder, ct);
                        }
                        else
                        {
                            inputStream.CopyTo(outputStream);
                            outputStream.Seek(0, SeekOrigin.Begin);
                            await TranscodeSkiaAsync(path, outputStream, BitmapEncoder.JpegXREncoderId, _jpegPropertySet, outputStream, setupEncoder, ct);
                        }
                    }
                    else if (_folderListingSettings.ThumbnailDecodeType == ThumbnailDecodeMethod.WindowsImageCodec)
                    {
                        if (inputStream.CanSeek)
                        {
                            await TranscodeWindowsImageCodecAsync(path, inputStream.AsRandomAccessStream(), BitmapEncoder.JpegXREncoderId, _jpegPropertySet, outputStream.AsRandomAccessStream(), setupEncoder, ct);
                        }
                        else
                        {
                            inputStream.CopyTo(outputStream);
                            outputStream.Seek(0, SeekOrigin.Begin);
                            await TranscodeWindowsImageCodecAsync(path, outputStream.AsRandomAccessStream(), BitmapEncoder.JpegXREncoderId, _jpegPropertySet, outputStream.AsRandomAccessStream(), setupEncoder, ct);
                        }
                    }
                    else if (_folderListingSettings.ThumbnailDecodeType == ThumbnailDecodeMethod.Win2D)
                    {
                        if (inputStream.CanSeek)
                        {
                            await TranscodeWithWin2DAsync(path, inputStream.AsRandomAccessStream(), BitmapEncoder.JpegXREncoderId, _jpegPropertySet, outputStream, setupEncoder, ct);
                        }
                        else
                        {
                            inputStream.CopyTo(outputStream);
                            outputStream.Seek(0, SeekOrigin.Begin);
                            await TranscodeWithWin2DAsync(path, outputStream.AsRandomAccessStream(), BitmapEncoder.JpegXREncoderId, _jpegPropertySet, outputStream, setupEncoder, ct);
                        }
                    }
                }
            }
        }
        catch (NotSupportedImageFormatException)
        {
            if (_folderListingSettings.ThumbnailDecodeType != ThumbnailDecodeMethod.Skia)
            {
                using (var inputStream = await streamOpener())
                {
                    inputStream.CopyTo(outputStream);
                    outputStream.Seek(0, SeekOrigin.Begin);
                    await TranscodeSkiaAsync(path, outputStream, BitmapEncoder.JpegXREncoderId, _jpegPropertySet, outputStream, setupEncoder, ct);
                }
            }
            else
            {
                using (var inputStream = await streamOpener())
                {
                    inputStream.CopyTo(outputStream);
                    outputStream.Seek(0, SeekOrigin.Begin);
                    await TranscodeWindowsImageCodecAsync(path, outputStream.AsRandomAccessStream(), BitmapEncoder.JpegXREncoderId, _jpegPropertySet, outputStream.AsRandomAccessStream(), setupEncoder, ct);
                }
            }
        }
        catch (NotSupportedException) // Seek不可な場合
        {
            using (var inputStream = await streamOpener())
            {                
                await RandomAccessStream.CopyAsync(inputStream.AsInputStream(), outputStream.AsOutputStream()).AsTask(ct);
                if (_folderListingSettings.ThumbnailDecodeType == ThumbnailDecodeMethod.Skia)
                {
                    await TranscodeSkiaAsync(path, outputStream, BitmapEncoder.JpegXREncoderId, _jpegPropertySet, outputStream, setupEncoder, ct);
                }
                else if (_folderListingSettings.ThumbnailDecodeType == ThumbnailDecodeMethod.WindowsImageCodec)
                {
                    await TranscodeWindowsImageCodecAsync(path, outputStream.AsRandomAccessStream(), BitmapEncoder.JpegXREncoderId, _jpegPropertySet, outputStream.AsRandomAccessStream(), setupEncoder, ct);
                }
                else if (_folderListingSettings.ThumbnailDecodeType == ThumbnailDecodeMethod.Win2D)
                {
                    await TranscodeWithWin2DAsync(path, outputStream.AsRandomAccessStream(), BitmapEncoder.JpegXREncoderId, _jpegPropertySet, outputStream, setupEncoder, ct);
                }
            }
        }
    }

    private async ValueTask TranscodeThumbnailImageToStreamAsync(string path, Stream stream, Stream outputStream, Action<BitmapDecoder, BitmapEncoder> setupEncoder, CancellationToken ct)
    {
        try
        {
            if (path.EndsWith(".gif"))
            {
                stream.CopyTo(outputStream);
                stream.Seek(0, SeekOrigin.Begin);
                var imageInfo = SKBitmap.DecodeBounds(stream);
                if (imageInfo != SKImageInfo.Empty)
                {
                    SetThumbnailSize(path, imageInfo);
                }
            }
            else
            {                
                if (_folderListingSettings.ThumbnailDecodeType == ThumbnailDecodeMethod.Skia)
                {
                    if (stream.CanSeek)
                    {
                        await TranscodeSkiaAsync(path, stream, BitmapEncoder.JpegXREncoderId, _jpegPropertySet, outputStream, setupEncoder, ct);
                    }
                    else
                    {
                        stream.CopyTo(outputStream);
                        outputStream.Seek(0, SeekOrigin.Begin);
                        await TranscodeSkiaAsync(path, outputStream, BitmapEncoder.JpegXREncoderId, _jpegPropertySet, outputStream, setupEncoder, ct);
                    }
                }                
                else if (_folderListingSettings.ThumbnailDecodeType == ThumbnailDecodeMethod.WindowsImageCodec)
                {
                    if (stream.CanSeek)
                    {
                        await TranscodeWindowsImageCodecAsync(path, stream.AsRandomAccessStream(), BitmapEncoder.JpegXREncoderId, _jpegPropertySet, outputStream.AsRandomAccessStream(), setupEncoder, ct);
                    }
                    else
                    {
                        stream.CopyTo(outputStream);
                        outputStream.Seek(0, SeekOrigin.Begin);
                        await TranscodeWindowsImageCodecAsync(path, outputStream.AsRandomAccessStream(), BitmapEncoder.JpegXREncoderId, _jpegPropertySet, outputStream.AsRandomAccessStream(), setupEncoder, ct);
                    }
                }
                else if (_folderListingSettings.ThumbnailDecodeType == ThumbnailDecodeMethod.Win2D)
                {
                    if (stream.CanSeek)
                    {
                        await TranscodeWithWin2DAsync(path, stream.AsRandomAccessStream(), BitmapEncoder.JpegXREncoderId, _jpegPropertySet, outputStream, setupEncoder, ct);
                    }
                    else
                    {
                        stream.CopyTo(outputStream);
                        outputStream.Seek(0, SeekOrigin.Begin);
                        await TranscodeWithWin2DAsync(path, outputStream.AsRandomAccessStream(), BitmapEncoder.JpegXREncoderId, _jpegPropertySet, outputStream, setupEncoder, ct);
                    }
                }
            }
        }
        catch (NotSupportedImageFormatException)
        {
            stream.Seek(0, SeekOrigin.Begin);
            if (_folderListingSettings.ThumbnailDecodeType != ThumbnailDecodeMethod.Skia)
            {
                stream.CopyTo(outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);
                await TranscodeSkiaAsync(path, outputStream, BitmapEncoder.JpegXREncoderId, _jpegPropertySet, outputStream, setupEncoder, ct);
            }
            else
            {
                stream.CopyTo(outputStream);
                outputStream.Seek(0, SeekOrigin.Begin);
                await TranscodeWindowsImageCodecAsync(path, outputStream.AsRandomAccessStream(), BitmapEncoder.JpegXREncoderId, _jpegPropertySet, outputStream.AsRandomAccessStream(), setupEncoder, ct);
            }
        }
        catch (NotSupportedException) // Seek不可な場合
        {
            stream.Seek(0, SeekOrigin.Begin);
            await RandomAccessStream.CopyAsync(stream.AsInputStream(), outputStream.AsOutputStream());
            outputStream.Seek(0, SeekOrigin.Begin);
            if (_folderListingSettings.ThumbnailDecodeType == ThumbnailDecodeMethod.Skia)
            {
                await TranscodeSkiaAsync(path, outputStream, BitmapEncoder.JpegXREncoderId, _jpegPropertySet, outputStream, setupEncoder, ct);
            }
            else if (_folderListingSettings.ThumbnailDecodeType == ThumbnailDecodeMethod.WindowsImageCodec)
            {
                await TranscodeWindowsImageCodecAsync(path, outputStream.AsRandomAccessStream(), BitmapEncoder.JpegXREncoderId, _jpegPropertySet, outputStream.AsRandomAccessStream(), setupEncoder, ct);
            }
            else if (_folderListingSettings.ThumbnailDecodeType == ThumbnailDecodeMethod.Win2D)
            {
                await TranscodeWithWin2DAsync(path, outputStream.AsRandomAccessStream(), BitmapEncoder.JpegXREncoderId, _jpegPropertySet, outputStream, setupEncoder, ct);
            }
        }
    }

    async ValueTask TranscodeSkiaAsync(string path, Stream stream, Guid encoderId, BitmapPropertySet propertySet, Stream outputStream, Action<BitmapDecoder, BitmapEncoder> setupEncoder, CancellationToken ct)
    {
        //using var skiaStream = new SKManagedStream(stream);            
        using var bitmap = SKBitmap.Decode(new SKManagedStream(stream, false));

        if (bitmap == null)
            throw new NotSupportedImageFormatException(Path.GetExtension(path));

        // サムネイルサイズ計算
        var scaledSize = CulcThumbnailSize(bitmap.Width, bitmap.Height);

        // SkiaSharpでリサイズ
        using var resizedBitmap = bitmap.Resize(new SKImageInfo((int)scaledSize.Width, (int)scaledSize.Height), SKFilterQuality.High);

        if (resizedBitmap == null)
            throw new Exception("SkiaSharp resize failed.");

        // サムネイルサイズ情報を記録
        _thumbnailImageInfoRepository.UpdateItem(new ThumbnailImageInfo()
        {
            Path = ToId(path),
            ImageWidth = (uint)resizedBitmap.Width,
            ImageHeight = (uint)resizedBitmap.Height,
            RatioWH = resizedBitmap.Width / (float)resizedBitmap.Height
        });

        // SKBitmap → SKImage → SKData (エンコード)
        using var image = SKImage.FromBitmap(resizedBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 80); // 80%品質        
        
        // SKData → outputStream
        outputStream.SetLength(0);
        data.SaveTo(outputStream);
        //await outputStream.FlushAsync(ct);
        outputStream.Seek(0, SeekOrigin.Begin);
    }


    async Task TranscodeWindowsImageCodecAsync(string path, IRandomAccessStream stream, Guid encoderId, BitmapPropertySet propertySet, IRandomAccessStream outputStream, Action<BitmapDecoder, BitmapEncoder> setupEncoder, CancellationToken ct)
    {
        // implement ref@ https://gist.github.com/alexsorokoletov/71431e403c0fa55f1b4c942845a3c850            

        try
        {
            var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(ct).ConfigureAwait(false);

            // サムネイルサイズ情報を記録                
            _thumbnailImageInfoRepository.UpdateItem(new ThumbnailImageInfo()
            {
                Path = ToId(path),
                ImageWidth = decoder.PixelWidth,
                ImageHeight = decoder.PixelHeight,
                RatioWH = decoder.PixelWidth / (float)decoder.PixelHeight
            });

            var pixelData = await decoder.GetPixelDataAsync().AsTask(ct).ConfigureAwait(false);
            var detachedPixelData = pixelData.DetachPixelData();
            pixelData = null;

            outputStream.Size = 0;

            var encoder = await BitmapEncoder.CreateAsync(encoderId, outputStream, propertySet).AsTask().ConfigureAwait(false);

            setupEncoder(decoder, encoder);

            Debug.WriteLine($"thumb out <{path}> size: w= {encoder.BitmapTransform.ScaledWidth} h= {encoder.BitmapTransform.ScaledHeight}");
            encoder.SetPixelData(decoder.BitmapPixelFormat, decoder.BitmapAlphaMode, decoder.OrientedPixelWidth, decoder.OrientedPixelHeight, decoder.DpiX, decoder.DpiY, detachedPixelData);

            await encoder.FlushAsync().AsTask(ct).ConfigureAwait(false);
            await outputStream.FlushAsync().AsTask(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex.HResult == -1072868846)
        {
            throw new NotSupportedImageFormatException(Path.GetExtension(path));
        }
        catch (COMException)
        {
            throw new NotSupportedImageFormatException(Path.GetExtension(path));
        }        
    }

    async Task TranscodeWithWin2DAsync(string path, IRandomAccessStream stream, Guid encoderId, BitmapPropertySet propertySet, Stream outputStream, Action<BitmapDecoder, BitmapEncoder> setupEncoder, CancellationToken ct)
    {
        try
        {
            using var bitmap = await CanvasBitmap.LoadAsync(_canvasDevice, stream).AsTask(ct);
            var scaledSize = CulcThumbnailSize((int)bitmap.Size.Width, (int)bitmap.Size.Height);
            using var canvas = new CanvasRenderTarget(bitmap, (float)scaledSize.Width, (float)scaledSize.Height);
            using (var ds = canvas.CreateDrawingSession())
            {
                float ratio = scaledSize.Width > scaledSize.Height
                    ? scaledSize.Width / (float)bitmap.Size.Width
                    : scaledSize.Height / (float)bitmap.Size.Height;
                ds.Transform = Matrix3x2.CreateScale(ratio);
                ds.Blend = CanvasBlend.Copy;
                ds.Antialiasing = CanvasAntialiasing.Aliased;
                ds.DrawImage(bitmap, 0, 0, new Rect(new Windows.Foundation.Point(), bitmap.Size), 1, CanvasImageInterpolation.HighQualityCubic);
            }

            // サムネイルサイズ情報を記録                
            _thumbnailImageInfoRepository.UpdateItem(new ThumbnailImageInfo()
            {
                Path = ToId(path),
                ImageWidth = (uint)scaledSize.Width,
                ImageHeight = (uint)scaledSize.Height,
                RatioWH = scaledSize.Width / scaledSize.Height
            });

            outputStream.SetLength(0);
            await canvas.SaveAsync(outputStream.AsRandomAccessStream(), CanvasBitmapFileFormat.JpegXR, 0.8f).AsTask(ct);
            outputStream.Seek(0, SeekOrigin.Begin);
        }
        catch (Exception ex) when (ex.HResult == -1072868846)
        {
            throw new NotSupportedImageFormatException(Path.GetExtension(path));
        }
    }


    private async ValueTask<bool> ImageFileThumbnailImageWriteToStreamAsync(StorageFile file, Stream outputStream, CancellationToken ct)
    {
        await TranscodeThumbnailImageToStreamAsync(file.Path, () => new (new FileStream(file.CreateSafeFileHandle(FileAccess.Read), FileAccess.Read)), outputStream, EncodingForImageFileThumbnailBitmap, ct);
        return true;
    }
    private async ValueTask<bool> ZipFileThumbnailImageWriteToStreamAsync(StorageFile file, Stream outputStream, CancellationToken ct)
    {
        using (var fileHandle = file.CreateSafeFileHandle(FileAccess.Read, options: FileOptions.SequentialScan))
        using (var fileStream = new FileStream(fileHandle, FileAccess.Read))
        using (var zipArchive = new ZipArchive(fileStream))
        {
            ct.ThrowIfCancellationRequested();

            ZipArchiveEntry? entry = null;
            //if (GetTitlePriorityRegex() is not null and Regex regex)
            //{
            //    entry = zipArchive.Entries.FirstOrDefault(x => regex.IsMatch(x.Name));
            //}

            entry ??= zipArchive.Entries.FirstOrDefault(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Name));
            if (entry == null) { return false; }
            await TranscodeThumbnailImageToStreamAsync(file.Path, () => new(entry.Open()), outputStream, EncodingForFolderOrArchiveFileThumbnailBitmap, ct);
            return true;
        }
    }

    private async ValueTask<bool> RarFileThumbnailImageWriteToStreamAsync(StorageFile file, Stream outputStream, CancellationToken ct)
    {
        using (var fileHandle = file.CreateSafeFileHandle(FileAccess.Read))
        using (var fileStream = new FileStream(fileHandle, FileAccess.Read))
        using (var rarArchive = RarArchive.OpenArchive(fileStream))
        {
            RarArchiveEntry? entry = null;
            //if (GetTitlePriorityRegex() is not null and Regex regex)
            //{
            //    entry = (RarArchiveEntry)rarArchive.Entries.FirstOrDefault(x => regex.IsMatch(x.Key));
            //}

            entry ??= (RarArchiveEntry)rarArchive.Entries.FirstOrDefault(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Key));
            if (entry == null) { return default; }
            await TranscodeThumbnailImageToStreamAsync(file.Path, () => new(entry.OpenEntryStream()), outputStream, EncodingForFolderOrArchiveFileThumbnailBitmap, ct);
            return true;
        }
    }

    private async ValueTask<bool> SevenZipFileThumbnailImageWriteToStreamAsync(StorageFile file, Stream outputStream, CancellationToken ct)
    {
        using (var fileHandle = file.CreateSafeFileHandle(FileAccess.Read))
        using (var fileStream = new FileStream(fileHandle, FileAccess.Read))
        using (var archive = SevenZipArchive.OpenArchive(fileStream))
        {
            SevenZipArchiveEntry? entry = null;
            //if (GetTitlePriorityRegex() is not null and Regex regex)
            //{
            //    entry = (SevenZipArchiveEntry)archive.Entries.FirstOrDefault(x => regex.IsMatch(x.Key));
            //}

            entry ??= (SevenZipArchiveEntry)archive.Entries.FirstOrDefault(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Key));
            if (entry == null) { return default; }
            await TranscodeThumbnailImageToStreamAsync(file.Path, () => new(entry.OpenEntryStream()), outputStream, EncodingForFolderOrArchiveFileThumbnailBitmap, ct);
            return true;
        }
    }

    private async ValueTask<bool> TarFileThumbnailImageWriteToStreamAsync(StorageFile file, Stream outputStream, CancellationToken ct)
    {
        using (var fileHandle = file.CreateSafeFileHandle(FileAccess.Read))
        using (var fileStream = new FileStream(fileHandle, FileAccess.Read))
        using (var archive = TarArchive.OpenArchive(fileStream))
        {
            TarArchiveEntry? entry = null;
            //if (GetTitlePriorityRegex() is not null and Regex regex)
            //{
            //    entry = (TarArchiveEntry)archive.Entries.FirstOrDefault(x => regex.IsMatch(x.Key));
            //}

            entry ??= (TarArchiveEntry)archive.Entries.FirstOrDefault(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Key));
            if (entry == null) { return default; }
            await TranscodeThumbnailImageToStreamAsync(file.Path, () => new(entry.OpenEntryStream()), outputStream, EncodingForFolderOrArchiveFileThumbnailBitmap, ct);
            return true;
        }
    }

    private async ValueTask<bool> PdfFileThumbnailImageWriteToStreamAsync(StorageFile file, Stream outputStream, CancellationToken ct)
    {
        using var pdfStream = await file.OpenStreamForReadAsync();
        using var image = PDFtoImage.Conversion.ToImage(pdfStream, options: new PDFtoImage.RenderOptions()
        {
            Dpi = 96,
            Width = (int)_folderListingSettings.FolderItemThumbnailImageSize.Width,
            WithAspectRatio = true,
        });
       
        // サムネイルサイズ情報を記録                
        _thumbnailImageInfoRepository.UpdateItem(new ThumbnailImageInfo()
        {
            Path = ToId(file.Path),
            ImageWidth = (uint)image.Width,
            ImageHeight = (uint)image.Height,
            RatioWH = (uint)image.Width / (float)image.Height
        });

        image.Encode(outputStream, SKEncodedImageFormat.Png, 100);

        return true;
    }

    private async ValueTask<bool> EPubFileThubnailImageWriteToStreamAsync(StorageFile file, Stream outputStream, CancellationToken ct)
    {
        using var fileHandle = file.CreateSafeFileHandle(FileAccess.Read);
        using var fileStream = new FileStream(fileHandle, FileAccess.Read);
        var epubBook = await EpubReader.OpenBookAsync(fileStream);
        
        if (await epubBook.ReadCoverAsync() is not null and var coverBytes)
        {
            await TranscodeThumbnailImageToStreamAsync(file.Path, () => new(new MemoryStream(coverBytes)), outputStream, EncodingForFolderOrArchiveFileThumbnailBitmap, ct);
            return true;
        }
        else if (epubBook.Content.Images.Local.Any())
        {
            var firstImage = epubBook.Content.Images.Local.First();
            var bytes = await firstImage.ReadContentAsync();
            await TranscodeThumbnailImageToStreamAsync(file.Path, () => new(new MemoryStream(bytes)), outputStream, EncodingForFolderOrArchiveFileThumbnailBitmap, ct);
            return true;
        }
        else
        {
            return false;
        }
    }
    
    private async ValueTask<bool> MovieFileThubnailImageWriteToStreamAsync(StorageFile file, Stream outputStream, CancellationToken ct)
    {
        // 1. サムネイルの取得設定
        // ThumbnailMode.Videos を指定することで、動画に最適なサムネイルを取得します
        uint requestedSize = 200; // 要求するピクセルサイズ（長辺）

        try
        {
            var clip = await MediaClip.CreateFromFileAsync(file);
            var mc = new MediaComposition();
            mc.Clips.Add(clip);

            await TranscodeThumbnailImageToStreamAsync(file.Path, async () =>
            {
                return (await mc.GetThumbnailAsync(TimeSpan.FromSeconds(3), (int)requestedSize, 0, VideoFramePrecision.NearestFrame)).AsStreamForRead();
            }, outputStream, EncodingForFolderOrArchiveFileThumbnailBitmap, ct);
            return true;
        }
        catch
        {
            try
            {
                await FFMpeg_MovieFileThubnailImageWriteToStreamAsync(file, outputStream, ct);
                return true;
            }
            catch
            {
                ThumbnailOptions options = ThumbnailOptions.None;
                {
                    await TranscodeThumbnailImageToStreamAsync(file.Path, async () =>
                    {
                        return (await file.GetScaledImageAsThumbnailAsync(ThumbnailMode.VideosView, requestedSize, options).AsTask(ct)).AsStreamForRead();
                    }, outputStream, EncodingForFolderOrArchiveFileThumbnailBitmap, ct);
                    return true;
                }
            }
        }
    }
    private async ValueTask<bool> FFMpeg_MovieFileThubnailImageWriteToStreamAsync(StorageFile file, Stream outputStream, CancellationToken ct)
    {
        // 1. サムネイルの取得設定
        // ThumbnailMode.Videos を指定することで、動画に最適なサムネイルを取得します
        uint requestedSize = 200; // 要求するピクセルサイズ（長辺）

        using var fileStream = await file.OpenReadAsync().AsTask(ct);
        using var fg = await FrameGrabber.CreateFromStreamAsync(fileStream).AsTask(ct);
        fg.DecodePixelHeight = (int)requestedSize;

        if (GetThumbnailGenerationStatus(fg.CurrentVideoStream.CodecName) == ThumbnailGenerationStatus.Failed)
        {
            using var thumb = await file.GetScaledImageAsThumbnailAsync(ThumbnailMode.VideosView);
            await RandomAccessStream.CopyAsync(thumb, outputStream.AsOutputStream());
            return true;
        }
        else
        {
            using CancellationTokenSource cts = new CancellationTokenSource(3000);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, ct);
            var linkedCt = linkedCts.Token;
            try
            {
                using var frame = await fg.ExtractVideoFrameAsync(TimeSpan.FromSeconds(5)).AsTask(linkedCt);
                await frame.EncodeAsJpegAsync(outputStream.AsRandomAccessStream()).AsTask(linkedCt);
            }
            catch
            {
                ThumbnailGenerationFailed(fg.CurrentVideoStream.CodecName);
                using var thumb = await file.GetScaledImageAsThumbnailAsync(ThumbnailMode.VideosView);
                await RandomAccessStream.CopyAsync(thumb, outputStream.AsOutputStream());
            }

            return true;
        }
    }



    #region Thumbnail Size

    public ThumbnailSize? GetThumbnailOriginalSize(IStorageItem file)
    {
        return _thumbnailImageInfoRepository.TryGetSize(ToId(file.Path));
    }

    public ThumbnailSize? GetThumbnailOriginalSize(string path)
    {
        return _thumbnailImageInfoRepository.TryGetSize(ToId(path));
    }

    public ThumbnailSize? GetThumbnailOriginalSize(StorageFile file, IArchiveEntry archiveEntry)
    {
        return _thumbnailImageInfoRepository.TryGetSize(ToId(GetArchiveEntryPath(file, archiveEntry)));
    }


    public ThumbnailSize SetThumbnailSize(string path, BitmapImage image)
    {
        var item = _thumbnailImageInfoRepository.UpdateItem(new ThumbnailImageInfo()
        {
            Path = ToId(path),
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

    private ThumbnailSize SetThumbnailSize(string path, SKImageInfo imageInfo)
    {
        var item = _thumbnailImageInfoRepository.UpdateItem(new ThumbnailImageInfo()
        {
            Path = ToId(path),
            ImageHeight = (uint)imageInfo.Height,
            ImageWidth = (uint)imageInfo.Width,
            RatioWH = imageInfo.Width / (float)imageInfo.Height
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
        public string Path { get; set; } = "";

        [BsonField]
        public uint ImageWidth { get; set; }

        [BsonField]
        public uint ImageHeight { get; set; }

        [BsonField]
        public float RatioWH { get; set; }
    }

    private class ThumbnailImageInfoRepository : LiteDBServiceBase<ThumbnailImageInfo>
    {
        public ThumbnailImageInfoRepository(ILiteDatabase liteDatabase) : base(liteDatabase)
        {
            _collection.EnsureIndex(x => x.Path);
        }


        public ThumbnailSize? TryGetSize(string path)
        {
            try
            {
                var thumbInfo = _collection.FindById(path);
                //Debug.WriteLine(path);
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
            catch { return default; }
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
            return _collection.DeleteMany(x => path.StartsWith(x.Path, StringComparison.Ordinal));
        }
    }


    #endregion



    public class ThumbnailGenerationIssueEntry
    {
        [BsonId]
        public string CodecName { get; set; }

        public ThumbnailGenerationStatus Status { get; set; } = ThumbnailGenerationStatus.NotChecked;
    }

    public enum ThumbnailGenerationStatus
    {
        NotChecked,
        ProgressChecking,
        Checked_FFmpeg,
        Checked_MediaComposition,
        Failed,
    }

    public ThumbnailGenerationIssueEntry GetEnsureThumbnailGenerationIssueEntry(string codecName)
    {
        Guard.IsNotNullOrWhiteSpace(codecName);
        var entry = _thumnailGenerationIssueCollection.FindById(codecName);
        if (entry == null)
        {
            entry = new ThumbnailGenerationIssueEntry() { CodecName = codecName };
        }
        return entry;
    }

    public ThumbnailGenerationStatus GetThumbnailGenerationStatus(string codecName)
    {
        var entry = _thumnailGenerationIssueCollection.FindById(codecName);
        return entry?.Status ?? ThumbnailGenerationStatus.NotChecked;
    }

    public void SetThumbnailGenerationProgress(string codecName)
    {
        var entry = GetEnsureThumbnailGenerationIssueEntry(codecName);
        entry.Status = ThumbnailGenerationStatus.ProgressChecking;
        _thumnailGenerationIssueCollection.Upsert(entry);
    }

    public void SetThumbnailGenerationCheckedFFmpeg(string codecName)
    {
        var entry = GetEnsureThumbnailGenerationIssueEntry(codecName);
        entry.Status = ThumbnailGenerationStatus.Checked_FFmpeg;
        _thumnailGenerationIssueCollection.Upsert(entry);
    }

    public void SetThumbnailGenerationCheckedMediaComposition(string codecName)
    {
        var entry = GetEnsureThumbnailGenerationIssueEntry(codecName);
        entry.Status = ThumbnailGenerationStatus.Checked_MediaComposition;
        _thumnailGenerationIssueCollection.Upsert(entry);
    }


    public ThumbnailGenerationStatus GetThumbanilGenerationStatusIfProgressAsFailed(string codecName, out bool nowFailed)
    {
        var entry = GetEnsureThumbnailGenerationIssueEntry(codecName);
        nowFailed = false;
        if (entry == null) 
        {
            return ThumbnailGenerationStatus.NotChecked; 
        }
        if (entry.Status == ThumbnailGenerationStatus.ProgressChecking)
        {
            nowFailed = true;
            entry.Status = ThumbnailGenerationStatus.Failed;
            _thumnailGenerationIssueCollection.Upsert(entry);
        }
        return entry.Status;
    }

    public void ThumbnailGenerationFailed(string codecName)
    {
        var entry = GetEnsureThumbnailGenerationIssueEntry(codecName);
        entry.Status= ThumbnailGenerationStatus.Failed;
        _thumnailGenerationIssueCollection.Upsert(entry);
    }

    public void ClearThumbnailGenerationIssue(string codecName)
    {
        _thumnailGenerationIssueCollection.Delete(codecName);
    }


    #region Secondary Tile

    public const string SecondaryTileThumbnailSaveFolderName = "SecondaryTile";
    static StorageFolder? _secondaryTileThumbnailFolder;
    public static async ValueTask<StorageFolder> GetSecondaryTileThumbnailFolderAsync()
    {
        return _secondaryTileThumbnailFolder ??= await ApplicationData.Current.LocalFolder.CreateFolderAsync(SecondaryTileThumbnailSaveFolderName, CreationCollisionOption.OpenIfExists);
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

    public Task<GenerateSecondaryTileThumbnailResult?> GenerateSecondaryThumbnailImageAsync(IStorageItem storageItem, string tileId, CancellationToken ct)
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

    public async Task<GenerateSecondaryTileThumbnailResult?> GenerateSecondaryThumbnailImageAsync(StorageFolder folder, string tileId, CancellationToken ct)
    {
#if WINDOWS_UWP
        var query = folder.CreateFileQueryWithOptions(new QueryOptions(CommonFileQuery.OrderByName, SupportedFileTypesHelper.GetAllSupportedFileExtensions()) { FolderDepth = FolderDepth.Deep });
        var count = await query.GetItemCountAsync().AsTask(ct);

        if (count == 0) { return null; }

        var files = await query.GetFilesAsync(0, 1).AsTask(ct);
        return await GenerateSecondaryThumbnailImageAsync(files[0], tileId, ct);
#endif
    }

    private async Task<GenerateSecondaryTileThumbnailResult?> GenerateSecondaryThumbnailImageAsync(StorageFile file, string tileId, CancellationToken ct)
    {
        var thumbnailFolder = await GetSecondaryTileThumbnailFolderAsync();
        var itemFolder = await thumbnailFolder.CreateFolderAsync(tileId, CreationCollisionOption.ReplaceExisting);
        var wideThumbFile = await itemFolder.CreateFileAsync("thumb310x150.png", CreationCollisionOption.ReplaceExisting);
        var square310ThumbFile = await itemFolder.CreateFileAsync("thumb310x310.png", CreationCollisionOption.ReplaceExisting);
        var square150ThumbFile = await itemFolder.CreateFileAsync("thumb150x150.png", CreationCollisionOption.ReplaceExisting);


        try
        {
            using var stream = _recyclableMemoryStreamManager.GetStream();
            var result = await (file.FileType.ToLowerInvariant() switch
            {
                SupportedFileTypesHelper.ZipFileType => ZipFileThumbnailImageWriteToStreamAsync(file, stream, ct),
                SupportedFileTypesHelper.RarFileType => RarFileThumbnailImageWriteToStreamAsync(file, stream, ct),
                SupportedFileTypesHelper.PdfFileType => PdfFileThumbnailImageWriteToStreamAsync(file, stream, ct),
                SupportedFileTypesHelper.JpgFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream, ct),
                SupportedFileTypesHelper.JpegFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream, ct),
                SupportedFileTypesHelper.JfifFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream, ct),
                SupportedFileTypesHelper.PngFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream, ct),
                SupportedFileTypesHelper.BmpFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream, ct),
                SupportedFileTypesHelper.GifFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream, ct),
                SupportedFileTypesHelper.TifFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream, ct),
                SupportedFileTypesHelper.TiffFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream, ct),
                SupportedFileTypesHelper.SvgFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream, ct),
                SupportedFileTypesHelper.WebpFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream, ct),
                SupportedFileTypesHelper.AvifFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream, ct),
                SupportedFileTypesHelper.JpegXRFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream, ct),
                SupportedFileTypesHelper.EPubFileType => EPubFileThubnailImageWriteToStreamAsync(file, stream, ct),
                SupportedFileTypesHelper.Movie_Mp4FileType => MovieFileThubnailImageWriteToStreamAsync(file, stream, ct),
                SupportedFileTypesHelper.Movie_WebMFileType => FFMpeg_MovieFileThubnailImageWriteToStreamAsync(file, stream, ct),
                SupportedFileTypesHelper.Movie_HevcFileType => FFMpeg_MovieFileThubnailImageWriteToStreamAsync(file, stream, ct),
                SupportedFileTypesHelper.Movie_MkvFileType => FFMpeg_MovieFileThubnailImageWriteToStreamAsync(file, stream, ct),
                SupportedFileTypesHelper.Movie_M4vFileType => FFMpeg_MovieFileThubnailImageWriteToStreamAsync(file, stream, ct),
                SupportedFileTypesHelper.Movie_MovFileType => FFMpeg_MovieFileThubnailImageWriteToStreamAsync(file, stream, ct),
                SupportedFileTypesHelper.Movie_TsFileType => FFMpeg_MovieFileThubnailImageWriteToStreamAsync(file, stream, ct),
                SupportedFileTypesHelper.Movie_MTsFileType => FFMpeg_MovieFileThubnailImageWriteToStreamAsync(file, stream, ct),
                SupportedFileTypesHelper.Movie_M2TsFileType => FFMpeg_MovieFileThubnailImageWriteToStreamAsync(file, stream, ct),
                SupportedFileTypesHelper.Movie_AviFileType => FFMpeg_MovieFileThubnailImageWriteToStreamAsync(file, stream, ct),
                SupportedFileTypesHelper.Movie_WmvFileType => FFMpeg_MovieFileThubnailImageWriteToStreamAsync(file, stream, ct),
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

                var decoder = await BitmapDecoder.CreateAsync(stream.AsRandomAccessStream());
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

                return new GenerateSecondaryTileThumbnailResult(wideThumbFile, square310ThumbFile, square150ThumbFile);
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
