using LiteDB;
using Microsoft.IO;
using Microsoft.Toolkit.Uwp.Helpers;
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
using System.Text;
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
        private readonly RecyclableMemoryStreamManager _recyclableMemoryStreamManager;
        private readonly FastAsyncLock _fileReadWriteLock = new FastAsyncLock();

        public ThumbnailManager(
            FolderListingSettings folderListingSettings,
            ThumbnailImageInfoRepository thumbnailImageInfoRepository,
            RecyclableMemoryStreamManager recyclableMemoryStreamManager
            )
        {
            _folderListingSettings = folderListingSettings;
            _thumbnailImageInfoRepository = thumbnailImageInfoRepository;
            _recyclableMemoryStreamManager = recyclableMemoryStreamManager;
        }


        public ThumbnailSize? GetThubmnailOriginalSize(IStorageItem file)
        {
            return _thumbnailImageInfoRepository.GetSize(file.Path);
        }

        public ThumbnailSize? GetThubmnailOriginalSize(string path)
        {
            return _thumbnailImageInfoRepository.GetSize(path);
        }




        public static async ValueTask<StorageFolder> GetTempFolderAsync()
        {
            return await Task.FromResult(ApplicationData.Current.TemporaryFolder);
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
            var thumbnailFile = await tempFolder.CreateFileAsync(itemId, CreationCollisionOption.ReplaceExisting);
            using (await _fileReadWriteLock.LockAsync(ct))
            {
                await CopyImageToFile(targetItem.Path, bitmapImage, thumbnailFile, EncodingForFolderOrArchiveFileThumbnailBitmap, ct);
            }
        }


        public async Task<StorageFile> SetArchiveEntryThumbnailAsync(StorageFile targetItem, IArchiveEntry entry, IRandomAccessStream bitmapImage, CancellationToken ct)
        {
            var tempFolder = await GetTempFolderAsync();
            var path = GetArchiveEntryPath(targetItem, entry);
            var itemId = GetStorageItemId(path);
            var thumbnailFile = await tempFolder.CreateFileAsync(itemId, CreationCollisionOption.ReplaceExisting);
            using (await _fileReadWriteLock.LockAsync(ct))
            {
                await CopyImageToFile(path, bitmapImage, thumbnailFile, entry.IsDirectory ? EncodingForFolderOrArchiveFileThumbnailBitmap : EncodingForImageFileThumbnailBitmap, ct);
            }

            return thumbnailFile;
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

        public async Task DeleteFromPath(string path)
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

        public async Task<StorageFile> GetFileThumbnailImageAsync(StorageFile file, CancellationToken ct)
        {
            var tempFolder = await GetTempFolderAsync();
            var itemId = GetStorageItemId(file);
            if (await tempFolder.FileExistsAsync(itemId))
            {
                var cachedThumbnailFile = await tempFolder.GetFileAsync(itemId);
                using (var stream = await cachedThumbnailFile.OpenReadAsync())
                {
                    if (stream.Size != 0)
                    {
                        return cachedThumbnailFile;
                    }
                }
            }

            var thumbnailFile = await tempFolder.CreateFileAsync(itemId, CreationCollisionOption.ReplaceExisting);
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

        public async Task<StorageFile> GetArchiveEntryThumbnailImageAsync(StorageFile sourceFile, IArchiveEntry archiveEntry, CancellationToken ct)
        {
            var tempFolder = await GetTempFolderAsync();
            var path = GetArchiveEntryPath(sourceFile, archiveEntry);
            var itemId = GetStorageItemId(path);
            if (await tempFolder.FileExistsAsync(itemId))
            {
                var cachedThumbnailFile = await tempFolder.GetFileAsync(itemId).AsTask(ct);
                using (var stream = await cachedThumbnailFile.OpenReadAsync())
                {
                    if (stream.Size != 0)
                    {
                        return cachedThumbnailFile;
                    }
                }
            }

            if (archiveEntry.IsDirectory) { return null; }

            var thumbnailFile = await tempFolder.CreateFileAsync(itemId, CreationCollisionOption.ReplaceExisting);
            using (var memoryStream = _recyclableMemoryStreamManager.GetStream())
            using (await _fileReadWriteLock.LockAsync(ct))
            {
                using (var entryStream = archiveEntry.OpenEntryStream())
                {
                    await entryStream.CopyToAsync(memoryStream, 81920, ct);
                    memoryStream.Seek(0, SeekOrigin.Begin);

                    ct.ThrowIfCancellationRequested();
                }

                await CopyImageToFile(path, memoryStream.AsRandomAccessStream(), thumbnailFile, archiveEntry.IsDirectory ? EncodingForFolderOrArchiveFileThumbnailBitmap : EncodingForImageFileThumbnailBitmap, ct);
            }

            return thumbnailFile;
        }

        public async Task<StorageFile> GetPdfPageThumbnailImageAsync(StorageFile sourceFile, PdfPage pdfPage, CancellationToken ct)
        {
            var tempFolder = await GetTempFolderAsync();
            var path = GetArchiveEntryPath(sourceFile, pdfPage);
            var itemId = GetStorageItemId(path);
            if (await tempFolder.FileExistsAsync(itemId))
            {
                var cachedThumbnailFile = await tempFolder.GetFileAsync(itemId);
                using (var stream = await cachedThumbnailFile.OpenReadAsync())
                {
                    if (stream.Size != 0)
                    {
                        return cachedThumbnailFile;
                    }
                }
            }

            var thumbnailFile = await tempFolder.CreateFileAsync(itemId, CreationCollisionOption.ReplaceExisting).AsTask(ct);
            using (var memoryStream = _recyclableMemoryStreamManager.GetStream())
            using (await _fileReadWriteLock.LockAsync(ct))
            {
                await pdfPage.RenderToStreamAsync(memoryStream.AsRandomAccessStream()).AsTask(ct);
                memoryStream.Seek(0, SeekOrigin.Begin);

                ct.ThrowIfCancellationRequested();

                await CopyImageToFile(path, memoryStream.AsRandomAccessStream(), thumbnailFile, EncodingForImageFileThumbnailBitmap, ct);
            }

            return thumbnailFile;
        }


        private void EncodingForImageFileThumbnailBitmap(BitmapDecoder decoder, BitmapEncoder encoder)
        {
            // 縦横比を維持したまま 高さ = LargeFileThumbnailImageHeight になるようにスケーリング
            var ratio = (double)ListingImageConstants.LargeFileThumbnailImageHeight / decoder.PixelHeight;
            encoder.BitmapTransform.ScaledWidth = (uint)Math.Floor(decoder.PixelWidth * ratio);
            encoder.BitmapTransform.ScaledHeight = ListingImageConstants.LargeFileThumbnailImageHeight;
            encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
        }

        private Task<StorageFile> GenerateThumbnailImageAsync(StorageFile file, StorageFile outputFile, Action<BitmapDecoder, BitmapEncoder> setupEncoder, CancellationToken ct)
        {
            return Task.Run(async () => 
            {
                try
                {
                    using (var stream = _recyclableMemoryStreamManager.GetStream())
                    using (await _fileReadWriteLock.LockAsync(ct))
                    { 
                        var result = await (file.FileType switch
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

                            SupportedFileTypesHelper.EPubFileType => EPubFileThubnailImageWriteToStreamAsync(file, stream, ct),
                            _ => throw new NotSupportedException(file.FileType)
                        });

                        if (!result || stream.Length == 0) { return null; }

                        ct.ThrowIfCancellationRequested();
                        await CopyImageToFile(file.Path, stream.AsRandomAccessStream(), outputFile, setupEncoder, ct);

                        return outputFile;
                    }
                }
                catch
                {
                    await outputFile.DeleteAsync();
                    return null;
                }
            }, ct);
        }
        private readonly RecyclableMemoryStreamManager _recyclableMemoryStreamManagerForCopy = new RecyclableMemoryStreamManager();
        private async Task CopyImageToFile(string path, IRandomAccessStream stream, StorageFile outputFile, Action<BitmapDecoder, BitmapEncoder> setupEncoder, CancellationToken ct)
        {
            var decoder = await BitmapDecoder.CreateAsync(stream);


            //using (var memStream = _recyclableMemoryStreamManagerForCopy.GetStream().AsRandomAccessStream())
            using (var memStream = new InMemoryRandomAccessStream())
            {
                var encoder = await BitmapEncoder.CreateForTranscodingAsync(memStream, decoder);

                // サムネイルサイズ情報を記録
                _thumbnailImageInfoRepository.UpdateItem(new ThumbnailImageInfo()
                {
                    Path = path,
                    ImageWidth = decoder.PixelWidth,
                    ImageHeight = decoder.PixelHeight
                });

                setupEncoder(decoder, encoder);

                Debug.WriteLine($"thumb out <{path}> size: w= {encoder.BitmapTransform.ScaledWidth} h= {encoder.BitmapTransform.ScaledHeight}");

                await encoder.FlushAsync().AsTask(ct);

                memStream.Seek(0);
                using (await _fileReadWriteLock.LockAsync(ct))
                using (var fileStream = await outputFile.OpenAsync(FileAccessMode.ReadWrite).AsTask(ct))
                {
                    await RandomAccessStream.CopyAsync(memStream, fileStream).AsTask(ct);
                }
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
                var archiveImageItem = zipArchive.Entries.OrderBy(x=> x.Name).FirstOrDefault(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Name));
                if (archiveImageItem == null) { return false; }

                using (var inputStream = archiveImageItem.Open())
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
                var archiveImageItem = rarArchive.Entries.OrderBy(x => x.Key).FirstOrDefault(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Key));                
                if (archiveImageItem == null) { return false; }

                using (var inputStream = archiveImageItem.OpenEntryStream())
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
            using (var zipArchive = SevenZipArchive.Open(archiveStream))
            {
                var archiveImageItem = zipArchive.Entries.OrderBy(x => x.Key).FirstOrDefault(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Key));
                if (archiveImageItem == null) { return false; }

                using (var inputStream = archiveImageItem.OpenEntryStream())
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
            using (var zipArchive = TarArchive.Open(archiveStream))
            {
                var archiveImageItem = zipArchive.Entries.OrderBy(x => x.Key).FirstOrDefault(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Key));
                if (archiveImageItem == null) { return false; }

                using (var inputStream = archiveImageItem.OpenEntryStream())
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

        public async Task<StorageFile> GetFolderThumbnailAsync(StorageFolder folder, CancellationToken ct)
        {
            var itemId = GetStorageItemId(folder);
            if (await ApplicationData.Current.TemporaryFolder.FileExistsAsync(itemId))
            {
                return await ApplicationData.Current.TemporaryFolder.GetFileAsync(itemId);
            }
            else
            {
#if WINDOWS_UWP
                var query = folder.CreateFileQueryWithOptions(new QueryOptions(CommonFileQuery.OrderByName, SupportedFileTypesHelper.GetAllSupportedFileExtensions()) { FolderDepth = FolderDepth.Deep });
                var count = await query.GetItemCountAsync();

                if (count == 0) { return null; }

                var tempFolder = await GetTempFolderAsync();

                var thumbnailFile = await tempFolder.CreateFileAsync(itemId);
                var files = await query.GetFilesAsync(0, 1);
                return await GenerateThumbnailImageAsync(files[0], thumbnailFile, EncodingForFolderOrArchiveFileThumbnailBitmap, ct);
#else
                return null;
#endif
            }
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

        public Task<GenerateSecondaryTileThumbnailResult> GenerateSecondaryThumbnailImageAsync(IStorageItem storageItem)
        {
            if (storageItem is StorageFolder folder)
            {
                return GenerateSecondaryThumbnailImageAsync(folder);
            }
            else if (storageItem is StorageFile file)
            {
                return GenerateSecondaryThumbnailImageAsync(file);
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

        private  Task<GenerateSecondaryTileThumbnailResult> GenerateSecondaryThumbnailImageAsync(StorageFile file, string itemId, CancellationToken ct)
        {
            return Task.Run(async () =>
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
                        var result = await (file.FileType switch
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
            }, ct);
        }

        #endregion
    }
}
