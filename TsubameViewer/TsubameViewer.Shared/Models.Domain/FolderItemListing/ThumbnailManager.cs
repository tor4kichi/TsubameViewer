using LiteDB;
using Microsoft.Toolkit.Uwp.Helpers;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Tar;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Infrastructure;
using Uno;
using Uno.Threading;
using VersOne.Epub;
using Windows.Data.Pdf;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace TsubameViewer.Models.Domain.FolderItemListing
{
    public sealed class ThumbnailManager
    {
        private readonly FolderListingSettings _folderListingSettings;
        private readonly ThumbnailImageInfoRepository _thumbnailImageInfoRepository;
        private readonly static FastAsyncLock _fileReadWriteLock = new ();

        public ThumbnailManager(
            FolderListingSettings folderListingSettings,
            ThumbnailImageInfoRepository thumbnailImageInfoRepository
            )
        {
            _folderListingSettings = folderListingSettings;
            _thumbnailImageInfoRepository = thumbnailImageInfoRepository;

            _TitlePriorityRegex ??= _folderListingSettings.ObserveProperty(x => x.ThumbnailPriorityTitleRegexString)
                .Select(x => x is not null ? new Regex(x) : null)
                .ToReadOnlyReactivePropertySlim();
        }


        static ReadOnlyReactivePropertySlim<Regex> _TitlePriorityRegex;

        public ThumbnailSize? GetThubmnailOriginalSize(IStorageItem file)
        {
            return _thumbnailImageInfoRepository.GetSize(file.Path);
        }

        public ThumbnailSize? GetThubmnailOriginalSize(string path)
        {
            return _thumbnailImageInfoRepository.GetSize(path);
        }




        public static ValueTask<StorageFolder> GetTempFolderAsync()
        {
            return new (ApplicationData.Current.TemporaryFolder);
        }


        public const string SecondaryTileThumbnailSaveFolderName = "SecondaryTile";
        static StorageFolder _SecondaryTileThumbnailFolder;
        public static async ValueTask<StorageFolder> GetSecondaryTileThumbnailFolderAsync()
        {
            return _SecondaryTileThumbnailFolder ??= await ApplicationData.Current.LocalFolder.CreateFolderAsync(SecondaryTileThumbnailSaveFolderName, CreationCollisionOption.OpenIfExists);
        }


        public async Task SetThumbnailAsync(IStorageItem targetItem, IRandomAccessStream bitmapImage, CancellationToken ct)
        {
            var tempFolder = await GetTempFolderAsync();
            var itemId = GetStorageItemId(targetItem.Path);
            using (await _fileReadWriteLock.LockAsync(ct))
            {
                var thumbnailFile = await tempFolder.CreateFileAsync(itemId, CreationCollisionOption.ReplaceExisting);
                await TranscodeThumbnailImageToFileAsync(targetItem.Path, bitmapImage, thumbnailFile, EncodingForFolderOrArchiveFileThumbnailBitmap, ct);
            }
        }


        public async Task<StorageFile> SetArchiveEntryThumbnailAsync(StorageFile targetItem, IArchiveEntry entry, IRandomAccessStream bitmapImage, CancellationToken ct)
        {
            var tempFolder = await GetTempFolderAsync();
            var path = GetArchiveEntryPath(targetItem, entry);
            var itemId = GetStorageItemId(path);
            using (await _fileReadWriteLock.LockAsync(ct))
            {
                var thumbnailFile = await tempFolder.CreateFileAsync(itemId, CreationCollisionOption.ReplaceExisting);
                await TranscodeThumbnailImageToFileAsync(path, bitmapImage, thumbnailFile, entry.IsDirectory ? EncodingForFolderOrArchiveFileThumbnailBitmap : EncodingForImageFileThumbnailBitmap, ct);

                return thumbnailFile;
            }
        }

        public async Task DeleteAllThumnnailsAsync()
        {
            await Task.Run(async () =>
            {
                var deleteCount = _thumbnailImageInfoRepository.DeleteAll();
                Debug.WriteLine("Delete Thubmnail Db : " + deleteCount);

                var tempFolder = await GetTempFolderAsync();
                var files = await tempFolder.GetFilesAsync();
                using (await _fileReadWriteLock.LockAsync(CancellationToken.None))
                {
                    foreach (var file in files)
                    {
                        await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    }
                }
            });
        }

        public async Task DeleteThumbnailFromPathAsync(string path)
        {
            _thumbnailImageInfoRepository.DeleteItem(path);
            var tempFolder = await GetTempFolderAsync();
            var itemId = GetStorageItemId(path);
            if (await tempFolder.FileExistsAsync(itemId))
            {
                using (await _fileReadWriteLock.LockAsync(CancellationToken.None))
                {
                    var file = await ApplicationData.Current.TemporaryFolder.GetFileAsync(itemId);
                    await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }
            }
        }

        public async Task FolderChangedAsync(string oldPath, string newPath)
        {
            using (await _fileReadWriteLock.LockAsync(CancellationToken.None))
            {
                var oldPathId = GetStorageItemId(oldPath);
                var newPathId = GetStorageItemId(newPath);

                var tempFolder = await GetTempFolderAsync();
                var oldFilesQuery = tempFolder.CreateItemQueryWithOptions(new QueryOptions(CommonFileQuery.DefaultQuery, SupportedFileTypesHelper.GetAllSupportedFileExtensions())
                {
                    ApplicationSearchFilter = $"System.FileName:{oldPathId}*"
                });

                int currentIndex = 0;
                while (await oldFilesQuery.GetItemsAsync((uint)currentIndex, 100) is not null and var items && items.Any())
                {
                    currentIndex += items.Count;
                    foreach (var item in items)
                    {
                        var oldName = item.Name;
                        await item.RenameAsync(item.Name.Replace(oldPathId, newPathId), NameCollisionOption.ReplaceExisting);
                        Debug.WriteLine($"rename {oldName} ===> {item.Name}");
                    }
                }
            }
        }


        Dictionary<string, string> _FilePathToHashCodeStringMap = new Dictionary<string, string>();

        public string GetArchiveEntryPath(StorageFile file, IArchiveEntry entry)
        {
            return Path.Combine(file.Path, entry.Key);
        }
        public string GetArchiveEntryPath(StorageFile file, PdfPage pdfPage)
        {
            return Path.Combine(file.Path, pdfPage.Index.ToString());
        }
        public string GetStorageItemId(StorageFile file, IArchiveEntry entry)
        {
            return GetStorageItemId(GetArchiveEntryPath(file, entry));
        }
        public string GetStorageItemId(IStorageItem item)
        {
            return GetStorageItemId(item.Path);
        }

        public string GetStorageItemId(string path)
        {
            return _FilePathToHashCodeStringMap.TryGetValue(path, out var code)
                ? code
                : _FilePathToHashCodeStringMap[path] = new String(path.Select(x => Path.GetInvalidFileNameChars().Any(c => x == c) ? '_' : x).ToArray())
                ;
        }

        private async Task<StorageFile> GetThumbnailFromIdAsync(string itemId, CancellationToken ct)
        {
            var tempFolder = await GetTempFolderAsync();
            using (await _fileReadWriteLock.LockAsync(ct))
            if (await tempFolder.FileExistsAsync(itemId))
            {
                var cachedThumbnailFile = await tempFolder.GetFileAsync(itemId).AsTask(ct);
                using (await _fileReadWriteLock.LockAsync(ct))
                using (var stream = await cachedThumbnailFile.OpenReadAsync().AsTask(ct))
                {
                    if (stream.Size != 0)
                    {
                        return cachedThumbnailFile;
                    }
                }
            }

            return null;
        }

        public async Task<StorageFile> GetFolderThumbnailAsync(StorageFolder folder, CancellationToken ct)
        {
            var itemId = GetStorageItemId(folder);
            if (await GetThumbnailFromIdAsync(itemId, ct) is not null and var cachedFile)
            {
                return cachedFile;
            }

#if WINDOWS_UWP


            StorageFile file = null;
            using (await _fileReadWriteLock.LockAsync(ct))
            {
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
                    var count = await query.GetItemCountAsync();

                    if (count == 0) { return null; }

                    var files = await query.GetFilesAsync(0, 1);
                    file = files[0];
                }
            }
            
            var tempFolder = await GetTempFolderAsync();
            var thumbnailFile = await tempFolder.CreateFileAsync(itemId, CreationCollisionOption.ReplaceExisting);
            return await GenerateThumbnailImageAsync(file, thumbnailFile, EncodingForFolderOrArchiveFileThumbnailBitmap, ct);
#else
            return null;
#endif

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



        public async Task<StorageFile> GetFileThumbnailImageAsync(StorageFile file, CancellationToken ct)
        {
            var itemId = GetStorageItemId(file);
            if (await GetThumbnailFromIdAsync(itemId, ct) is not null and var cachedFile)
            {
                return cachedFile;
            }

            var tempFolder = await GetTempFolderAsync();
            var thumbnailFile = await tempFolder.CreateFileAsync(itemId, CreationCollisionOption.ReplaceExisting).AsTask(ct);
            if (SupportedFileTypesHelper.IsSupportedArchiveFileExtension(file.FileType)
                )
            {
                return await GenerateThumbnailImageAsync(file, thumbnailFile, EncodingForFolderOrArchiveFileThumbnailBitmap, ct);
            }
            else
            {
                return await GenerateThumbnailImageAsync(file, thumbnailFile, EncodingForImageFileThumbnailBitmap, ct);
            }
        }


        static readonly object _lockForReadArchiveEntry = new object();

        public async Task<StorageFile> GetArchiveEntryThumbnailImageAsync(StorageFile sourceFile, IArchiveEntry archiveEntry, CancellationToken ct)
        {
            var path = GetArchiveEntryPath(sourceFile, archiveEntry);
            var itemId = GetStorageItemId(path);
            if (await GetThumbnailFromIdAsync(itemId, ct) is not null and var cachedFile)
            {
                return cachedFile;
            }

            if (archiveEntry.IsDirectory) { return null; }

            using (await _fileReadWriteLock.LockAsync(ct))
            using (var memoryStream = new MemoryStream())
            {
                var tempFolder = await GetTempFolderAsync();
                var thumbnailFile = await tempFolder.CreateFileAsync(itemId, CreationCollisionOption.ReplaceExisting).AsTask(ct);

                // アーカイブファイル内のシーク制御を確実に同期的に行わせるために別途ロックを仕掛ける
                lock (_lockForReadArchiveEntry)
                using (var entryStream = archiveEntry.OpenEntryStream())
                {
                    entryStream.CopyTo(memoryStream);
                    memoryStream.Seek(0, SeekOrigin.Begin);

                    ct.ThrowIfCancellationRequested();
                }

                await TranscodeThumbnailImageToFileAsync(path, memoryStream.AsRandomAccessStream(), thumbnailFile, archiveEntry.IsDirectory ? EncodingForFolderOrArchiveFileThumbnailBitmap : EncodingForImageFileThumbnailBitmap, ct);
                return thumbnailFile;
            }
        }

        public async Task<StorageFile> GetPdfPageThumbnailImageAsync(StorageFile sourceFile, PdfPage pdfPage, CancellationToken ct)
        {
            var path = GetArchiveEntryPath(sourceFile, pdfPage);
            var itemId = GetStorageItemId(path);
            if (await GetThumbnailFromIdAsync(itemId, ct) is not null and var cachedFile)
            {
                return cachedFile;
            }

            using (var memoryStream = new InMemoryRandomAccessStream())
            using (await _fileReadWriteLock.LockAsync(ct))
            {
                var tempFolder = await GetTempFolderAsync();
                var thumbnailFile = await tempFolder.CreateFileAsync(itemId, CreationCollisionOption.ReplaceExisting).AsTask(ct);
                await pdfPage.RenderToStreamAsync(memoryStream).AsTask(ct);
                memoryStream.Seek(0);

                ct.ThrowIfCancellationRequested();

                await TranscodeThumbnailImageToFileAsync(path, memoryStream, thumbnailFile, EncodingForImageFileThumbnailBitmap, ct);

                return thumbnailFile;
            }
        }


        private void EncodingForImageFileThumbnailBitmap(BitmapDecoder decoder, BitmapEncoder encoder)
        {
            // 縦横比を維持したまま 高さ = LargeFileThumbnailImageHeight になるようにスケーリング
            var ratio = (double)ListingImageConstants.LargeFileThumbnailImageHeight / decoder.PixelHeight;
            encoder.BitmapTransform.ScaledWidth = (uint)Math.Floor(decoder.PixelWidth * ratio);
            encoder.BitmapTransform.ScaledHeight = ListingImageConstants.LargeFileThumbnailImageHeight;
            encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
        }

        private async Task<StorageFile> GenerateThumbnailImageAsync(StorageFile file, StorageFile outputFile, Action<BitmapDecoder, BitmapEncoder> setupEncoder, CancellationToken ct)
        {
            try
            {
                using (await _fileReadWriteLock.LockAsync(ct))
                using (var stream = new InMemoryRandomAccessStream().AsStream())
                {
                    var result = await(file.FileType switch
                    {
                        SupportedFileTypesHelper.ZipFileType => ZipFileThumbnailImageWriteToStreamAsync(file, stream, ct),
                        SupportedFileTypesHelper.RarFileType => RarFileThumbnailImageWriteToStreamAsync(file, stream, ct),
                        SupportedFileTypesHelper.PdfFileType => PdfFileThumbnailImageWriteToStreamAsync(file, stream, ct),
                        SupportedFileTypesHelper.CbzFileType => ZipFileThumbnailImageWriteToStreamAsync(file, stream, ct),
                        SupportedFileTypesHelper.CbrFileType => RarFileThumbnailImageWriteToStreamAsync(file, stream, ct),
                        SupportedFileTypesHelper.SevenZipFileType => SevenZipFileThumbnailImageWriteToStreamAsync(file, stream, ct),
                        SupportedFileTypesHelper.Cb7FileType => SevenZipFileThumbnailImageWriteToStreamAsync(file, stream, ct),
                        SupportedFileTypesHelper.TarFileType => TarFileThumbnailImageWriteToStreamAsync(file, stream, ct),


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

                        SupportedFileTypesHelper.EPubFileType => EPubFileThubnailImageWriteToStreamAsync(file, stream, ct),
                        _ => throw new NotSupportedException(file.FileType)
                    });

                    if (!result || stream.Length == 0) { return null; }

                    ct.ThrowIfCancellationRequested();
                    await TranscodeThumbnailImageToFileAsync(file.Path, stream.AsRandomAccessStream(), outputFile, setupEncoder, ct);

                    return outputFile;
                }
            }
            catch
            {
                await outputFile.DeleteAsync();
                return null;
            }
        }

        // see@ https://docs.microsoft.com/ja-jp/windows/win32/wic/jpeg-xr-codec
        
        static readonly BitmapPropertySet _jpegPropertySet = new BitmapPropertySet()
        {
            { "ImageQuality", new BitmapTypedValue(0.8d, Windows.Foundation.PropertyType.Single) },
        };

        private Task TranscodeThumbnailImageToFileAsync(string path, IRandomAccessStream stream, StorageFile outputFile, Action<BitmapDecoder, BitmapEncoder> setupEncoder, CancellationToken ct)
        {
            return TranscodeToFileAsync(path, stream, BitmapEncoder.JpegXREncoderId, _jpegPropertySet, outputFile, setupEncoder, ct);
        }

        private async Task TranscodeToFileAsync(string path, IRandomAccessStream stream, Guid encoderId, BitmapPropertySet propertySet, StorageFile outputFile, Action<BitmapDecoder, BitmapEncoder> setupEncoder, CancellationToken ct)
        {
            // implement ref@ https://gist.github.com/alexsorokoletov/71431e403c0fa55f1b4c942845a3c850
                
            var decoder = await BitmapDecoder.CreateAsync(stream);

            // サムネイルサイズ情報を記録
            _thumbnailImageInfoRepository.UpdateItem(new ThumbnailImageInfo()
            {
                Path = path,
                ImageWidth = decoder.PixelWidth,
                ImageHeight = decoder.PixelHeight
            });

            var pixelData = await decoder.GetPixelDataAsync();
            var detachedPixelData = pixelData.DetachPixelData();
            pixelData = null;

            using (var fileStream = await outputFile.OpenAsync(FileAccessMode.ReadWrite).AsTask(ct))
            {
                var encoder = await BitmapEncoder.CreateAsync(encoderId, fileStream, propertySet);

                setupEncoder(decoder, encoder);

                Debug.WriteLine($"thumb out <{path}> size: w= {encoder.BitmapTransform.ScaledWidth} h= {encoder.BitmapTransform.ScaledHeight}");
                encoder.SetPixelData(decoder.BitmapPixelFormat, decoder.BitmapAlphaMode, decoder.OrientedPixelWidth, decoder.OrientedPixelHeight, decoder.DpiX, decoder.DpiY, detachedPixelData);

                await encoder.FlushAsync().AsTask(ct);
                await fileStream.FlushAsync().AsTask(ct);
            }
        }

        private static async Task<bool> ImageFileThumbnailImageWriteToStreamAsync(StorageFile file, Stream outputStream, CancellationToken ct)
        {
            using (var fileStream = await file.OpenReadAsync().AsTask(ct))
            {
                await RandomAccessStream.CopyAsync(fileStream, outputStream.AsOutputStream()).AsTask(ct);
                return true;
            }
        }
        private static async Task<bool> ZipFileThumbnailImageWriteToStreamAsync(StorageFile file, Stream outputStream, CancellationToken ct)
        {
            using (var archiveStream = (await file.OpenReadAsync().AsTask(ct)).AsStreamForRead())
            using (var zipArchive = new ZipArchive(archiveStream))
            {
                ct.ThrowIfCancellationRequested();

                ZipArchiveEntry entry = null;
                if (_TitlePriorityRegex.Value is not null and Regex regex)
                {
                    entry = zipArchive.Entries.FirstOrDefault(x => regex.IsMatch(x.Name));
                }

                entry ??= zipArchive.Entries.OrderBy(x => x.Name).FirstOrDefault(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Name));

                if (entry == null) { return false; }

                using (var inputStream = entry.Open())
                {
                    await inputStream.CopyToAsync(outputStream, 81920, ct);
                    ct.ThrowIfCancellationRequested();
                    await outputStream.FlushAsync();
                }

                return true;
            }
        }

        private static async Task<bool> RarFileThumbnailImageWriteToStreamAsync(StorageFile file, Stream outputStream, CancellationToken ct)
        {
            using (var archiveStream = (await file.OpenReadAsync().AsTask(ct)).AsStreamForRead())
            using (var rarArchive = RarArchive.Open(archiveStream))
            {
                RarArchiveEntry entry = null;
                if (_TitlePriorityRegex.Value is not null and Regex regex)
                {
                    entry = rarArchive.Entries.FirstOrDefault(x => regex.IsMatch(x.Key));
                }

                entry ??= rarArchive.Entries.OrderBy(x => x.Key).FirstOrDefault(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Key));

                if (entry == null) { return false; }

                using (var inputStream = entry.OpenEntryStream())
                {
                    await inputStream.CopyToAsync(outputStream, 81920, ct);
                    await outputStream.FlushAsync();
                }

                return true;
            }
        }

        private static async Task<bool> SevenZipFileThumbnailImageWriteToStreamAsync(StorageFile file, Stream outputStream, CancellationToken ct)
        {
            using (var archiveStream = (await file.OpenReadAsync().AsTask(ct)).AsStreamForRead())
            using (var archive = SevenZipArchive.Open(archiveStream))
            {
                SevenZipArchiveEntry entry = null;
                if (_TitlePriorityRegex.Value is not null and Regex regex)
                {
                    entry = archive.Entries.FirstOrDefault(x => regex.IsMatch(x.Key));
                }

                entry ??= archive.Entries.OrderBy(x => x.Key).FirstOrDefault(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Key));

                if (entry == null) { return false; }

                using (var inputStream = entry.OpenEntryStream())
                {
                    await inputStream.CopyToAsync(outputStream, 81920, ct);
                    await outputStream.FlushAsync();
                }

                return true;
            }
        }

        private static async Task<bool> TarFileThumbnailImageWriteToStreamAsync(StorageFile file, Stream outputStream, CancellationToken ct)
        {
            using (var archiveStream = (await file.OpenReadAsync().AsTask(ct)).AsStreamForRead())
            using (var archive = TarArchive.Open(archiveStream))
            {
                TarArchiveEntry entry = null;
                if (_TitlePriorityRegex.Value is not null and Regex regex)
                {
                    entry = archive.Entries.FirstOrDefault(x => regex.IsMatch(x.Key));
                }

                entry ??= archive.Entries.OrderBy(x => x.Key).FirstOrDefault(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Key));

                if (entry == null) { return false; }

                using (var inputStream = entry.OpenEntryStream())
                {
                    await inputStream.CopyToAsync(outputStream, 81920, ct);
                    await outputStream.FlushAsync();
                }

                return true;
            }
        }

        private static async Task<bool> PdfFileThumbnailImageWriteToStreamAsync(StorageFile file, Stream outputStream, CancellationToken ct)
        {
            var pdfDocument = await PdfDocument.LoadFromFileAsync(file).AsTask(ct);
            if (pdfDocument.PageCount == 0) { return false; }

            using var page = pdfDocument.GetPage(0);
            await page.RenderToStreamAsync(outputStream.AsRandomAccessStream()).AsTask(ct);
            return true;
        }

        public async Task<StorageFile> GetThumbnailAsync(IStorageItem storageItem, CancellationToken ct)
        {
            if (storageItem is StorageFolder folder)
            {
                return await GetFolderThumbnailAsync(folder, ct);
            }
            else if (storageItem is StorageFile file)
            {
                return await GetFileThumbnailImageAsync(file, ct);
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        readonly static QueryOptions _CoverFileQueryOptions = new QueryOptions(CommonFileQuery.OrderByName, SupportedFileTypesHelper.SupportedImageFileExtensions)
        {
            FolderDepth = FolderDepth.Deep,
            ApplicationSearchFilter = "System.FileName:*cover*"
        };

        readonly static QueryOptions _AllSupportedFileQueryOptions = new QueryOptions(CommonFileQuery.OrderByName, SupportedFileTypesHelper.GetAllSupportedFileExtensions()) { FolderDepth = FolderDepth.Deep };



        private async Task<bool> EPubFileThubnailImageWriteToStreamAsync(StorageFile file, Stream outputStream, CancellationToken ct)
        {
            using var fileStream = (await file.OpenReadAsync().AsTask(ct)).AsStreamForRead();

            var epubBook = await EpubReader.OpenBookAsync(fileStream);

            var cover = await epubBook.ReadCoverAsync();
            if (cover != null)
            {
                await outputStream.WriteAsync(cover, 0, cover.Length);
            }
            else if (epubBook.Content.Images.Any())
            {
                var firstImage = epubBook.Content.Images.First().Value;
                var bytes = await firstImage.ReadContentAsync();
                await outputStream.WriteAsync(bytes, 0, bytes.Length);
            }
            else
            {
                return false;
            }

            return true;
        }

        public class ThumbnailImageInfo
        {
            [BsonId]
            public string Path { get; set; }

            [BsonField]
            public uint ImageWidth { get; set; }

            [BsonField]
            public uint ImageHeight { get; set; }            
        }

        public class ThumbnailImageInfoRepository : LiteDBServiceBase<ThumbnailImageInfo>
        {
            public ThumbnailImageInfoRepository(ILiteDatabase liteDatabase) : base(liteDatabase)
            {

            }


            public ThumbnailSize? GetSize(string path)
            {
                var thumbInfo = _collection.FindById(path);
                return thumbInfo != null 
                    ? new ThumbnailSize()
                    {
                        Width = thumbInfo.ImageWidth,
                        Height = thumbInfo.ImageHeight,
                    }
                    : default(ThumbnailSize?)
                    ;
            }


            public int DeleteAll()
            {
                return _collection.DeleteAll();
            }
        }

        public struct ThumbnailSize
        {
            public uint Width { get; set; }
            public uint Height { get; set; }
        }



        #region Secondary Tile



        public sealed class GenerateSecondaryTileThumbnailResult
        {
            public StorageFile Wide310x150Logo { get; set; }
            public StorageFile Square310x310Logo { get; set; }
            public StorageFile Square150x150Logo { get; set; }
        }

        public async Task SecondaryThumbnailDeleteNotExist(IEnumerable<string> itemIdList)
        {
            await Task.Run(async () => 
            {
                var thumbnailFolder = await GetSecondaryTileThumbnailFolderAsync();
                var idSet = itemIdList.ToHashSet();

                var folders = await thumbnailFolder.GetFoldersAsync();
                var alreadDeleteFolders = folders.Where(x => !idSet.Contains(x.Path));
                foreach (var folder in alreadDeleteFolders)
                {
                    await folder.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    Debug.WriteLine("delete secondary tile thumbnail: " + folder.Name);
                }
            });
        }

        public Task<GenerateSecondaryTileThumbnailResult> GenerateSecondaryThumbnailImageAsync(IStorageItem storageItem, CancellationToken ct)
        {
            if (storageItem is StorageFolder folder)
            {
                return GenerateSecondaryThumbnailImageAsync(folder, ct);
            }
            else if (storageItem is StorageFile file)
            {
                return GenerateSecondaryThumbnailImageAsync(file, ct);
            }
            else
            {
                throw new NotSupportedException(); 
            }
        }

        public async Task<GenerateSecondaryTileThumbnailResult> GenerateSecondaryThumbnailImageAsync(StorageFolder folder, CancellationToken ct)
        {
            var itemId = GetStorageItemId(folder);

#if WINDOWS_UWP
            var query = folder.CreateFileQueryWithOptions(new QueryOptions(CommonFileQuery.OrderByName, SupportedFileTypesHelper.GetAllSupportedFileExtensions()) { FolderDepth = FolderDepth.Deep });
            var count = await query.GetItemCountAsync().AsTask(ct);

            if (count == 0) { return null; }

            var files = await query.GetFilesAsync(0, 1).AsTask(ct);
            return await GenerateSecondaryThumbnailImageAsync(files[0], itemId, ct);
#endif
        }

        public Task<GenerateSecondaryTileThumbnailResult> GenerateSecondaryThumbnailImageAsync(StorageFile file, CancellationToken ct)
        {
            var itemId = GetStorageItemId(file);
            return GenerateSecondaryThumbnailImageAsync(file, itemId, ct);
        }

        private async Task<GenerateSecondaryTileThumbnailResult> GenerateSecondaryThumbnailImageAsync(StorageFile file, string itemId, CancellationToken ct)
        {
            var thumbnailFolder = await GetSecondaryTileThumbnailFolderAsync();
            var itemFolder = await thumbnailFolder.CreateFolderAsync(itemId, CreationCollisionOption.ReplaceExisting);
            var wideThumbFile = await itemFolder.CreateFileAsync("thumb310x150.png", CreationCollisionOption.ReplaceExisting);
            var square310ThumbFile = await itemFolder.CreateFileAsync("thumb310x310.png", CreationCollisionOption.ReplaceExisting);
            var square150ThumbFile = await itemFolder.CreateFileAsync("thumb150x150.png", CreationCollisionOption.ReplaceExisting);

            try
            {
                using (var stream = new InMemoryRandomAccessStream())
                {
                    var result = await(file.FileType switch
                    {
                        SupportedFileTypesHelper.ZipFileType => ZipFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite(), ct),
                        SupportedFileTypesHelper.RarFileType => RarFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite(), ct),
                        SupportedFileTypesHelper.PdfFileType => PdfFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite(), ct),
                        SupportedFileTypesHelper.JpgFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite(), ct),
                        SupportedFileTypesHelper.JpegFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite(), ct),
                        SupportedFileTypesHelper.JfifFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite(), ct),
                        SupportedFileTypesHelper.PngFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite(), ct),
                        SupportedFileTypesHelper.BmpFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite(), ct),
                        SupportedFileTypesHelper.GifFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite(), ct),
                        SupportedFileTypesHelper.TifFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite(), ct),
                        SupportedFileTypesHelper.TiffFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite(), ct),
                        SupportedFileTypesHelper.SvgFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite(), ct),
                        SupportedFileTypesHelper.WebpFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite(), ct),
                        SupportedFileTypesHelper.EPubFileType => EPubFileThubnailImageWriteToStreamAsync(file, stream.AsStreamForWrite(), ct),
                        _ => throw new NotSupportedException(file.FileType)
                    });

                    if (!result) { return null; }


                    (StorageFile file, int width, int height)[] items = new[]
                    {
                            (wideThumbFile, 310, 150),
                            (square310ThumbFile, 310, 310),
                            (square150ThumbFile, 150, 150),
                        };

                    var decoder = await BitmapDecoder.CreateAsync(stream);
                    using (var memStream = new InMemoryRandomAccessStream())
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
                                encoder.BitmapTransform.Bounds = new BitmapBounds() { X = 0, Y = 0, Width = (uint)item.width, Height = (uint)item.height };
                            }
                            else
                            {
                                // 高さに合わせて幅をスケールさせる
                                // 横長の場合に使用
                                var ratio = (float)item.height / decoder.PixelHeight;
                                encoder.BitmapTransform.ScaledWidth = (uint)(decoder.PixelWidth * ratio);
                                encoder.BitmapTransform.ScaledHeight = (uint)item.height;
                                encoder.BitmapTransform.Bounds = new BitmapBounds() { X = 0, Y = 0, Width = (uint)item.width, Height = (uint)item.height };
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
}
