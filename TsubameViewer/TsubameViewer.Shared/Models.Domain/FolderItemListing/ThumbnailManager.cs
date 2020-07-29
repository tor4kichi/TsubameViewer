using LiteDB;
using Microsoft.Toolkit.Uwp.Helpers;
using MonkeyCache;
using SharpCompress.Archives.Rar;
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
using Uno.Threading;
using Windows.Data.Pdf;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Search;
using Windows.Storage.Streams;

namespace TsubameViewer.Models.Domain.FolderItemListing
{
    public sealed class ThumbnailManager
    {
        private readonly ThumbnailImageInfoRepository _thumbnailImageInfoRepository;


        public ThumbnailManager(
            ThumbnailImageInfoRepository thumbnailImageInfoRepository 
            )
        {
            _thumbnailImageInfoRepository = thumbnailImageInfoRepository;
        }


        public ThumbnailSize? GetThubmnailOriginalSize(StorageFile file)
        {
            return _thumbnailImageInfoRepository.GetSize(file.Path);
        }




        public static async ValueTask<StorageFolder> GetTempFolderAsync()
        {
            return ApplicationData.Current.TemporaryFolder;
        }


        public async Task DeleteAllThumnnailsAsync()
        {
            await Task.Run(async () =>
            {
                var deleteCount = _thumbnailImageInfoRepository.DeleteAll();
                Debug.WriteLine("Delete Thubmnail Db : " + deleteCount);

                var tempFolder = await GetTempFolderAsync();
                var files = await tempFolder.GetFilesAsync();
                foreach (var file in files)
                {
                    await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }
            });
        }

        Dictionary<string, string> _FilePathToHashCodeStringMap = new Dictionary<string, string>();

        private string GetStorageItemId(StorageFile item)
        {
            return _FilePathToHashCodeStringMap.TryGetValue(item.Path, out var code)
                ? code
                : _FilePathToHashCodeStringMap[item.Path] = new String(item.Path.Select(x => Path.GetInvalidFileNameChars().Any(c => x == c) ? '_' : x).ToArray())
                ;
        }

        private string GetStorageItemId(StorageFolder item)
        {
            
            return _FilePathToHashCodeStringMap.TryGetValue(item.Path, out var code)
                ? code
                : _FilePathToHashCodeStringMap[item.Path] = new String(item.Path.Select(x => Path.GetInvalidFileNameChars().Any(c => x == c) ? '_' : x).ToArray())
                ;
        }

        public async Task<StorageFile> GetArchiveThumbnailAsync(StorageFile file, CancellationToken ct = default)
        {
            var tempFolder = await GetTempFolderAsync();
            var itemId = GetStorageItemId(file);
            if (await ApplicationData.Current.TemporaryFolder.FileExistsAsync(itemId))
            {
                return await ApplicationData.Current.TemporaryFolder.GetFileAsync(itemId);
            }
            else
            {
                var thumbnailFile = await tempFolder.CreateFileAsync(itemId, CreationCollisionOption.ReplaceExisting);
                return await GenerateThumbnailImageAsync(file, thumbnailFile);
            }
        }


        private Task<StorageFile> GenerateThumbnailImageAsync(StorageFile file, StorageFile outputFile)
        {
            return Task.Run(async () => 
            {
                try
                {
                    using (var stream = new InMemoryRandomAccessStream())
                    {
                        var result = await (file.FileType switch
                        {
                            SupportedFileTypesHelper.ZipFileType => ZipFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite()),
                            SupportedFileTypesHelper.RarFileType => RarFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite()),
                            SupportedFileTypesHelper.PdfFileType => PdfFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite()),
                            SupportedFileTypesHelper.JpgFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite()),
                            SupportedFileTypesHelper.JpegFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite()),
                            SupportedFileTypesHelper.PngFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite()),
                            SupportedFileTypesHelper.BmpFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite()),
                            SupportedFileTypesHelper.GifFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite()),
                            SupportedFileTypesHelper.TifFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite()),
                            SupportedFileTypesHelper.TiffFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite()),
                            SupportedFileTypesHelper.SvgFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite()),
                            _ => throw new NotSupportedException(file.FileType)
                        });

                        if (!result) { return null; }

                        var decoder = await BitmapDecoder.CreateAsync(stream);
                        using (var memStream = new InMemoryRandomAccessStream())
                        {
                            var encoder = await BitmapEncoder.CreateForTranscodingAsync(memStream, decoder);

                            // サムネイルサイズ情報を記録
                            _thumbnailImageInfoRepository.UpdateItem(new ThumbnailImageInfo()
                            {
                                Path = file.Path,
                                ImageWidth = decoder.PixelWidth,
                                ImageHeight = decoder.PixelHeight
                            });

                            // 縦横比を維持したまま 高さ = LargeFileThumbnailImageHeight になるようにスケーリング
                            var ratio = (double)ListingImageConstants.LargeFileThumbnailImageHeight / decoder.PixelHeight;
                            encoder.BitmapTransform.ScaledWidth = (uint)Math.Floor(decoder.PixelWidth * ratio);
                            encoder.BitmapTransform.ScaledHeight = ListingImageConstants.LargeFileThumbnailImageHeight;
                            encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
                            
                            await encoder.FlushAsync();

                            memStream.Seek(0);
                            using (var fileStream = await outputFile.OpenAsync(FileAccessMode.ReadWrite))
                            {
                                await RandomAccessStream.CopyAsync(memStream, fileStream);
                            }
                        }
                    }
                    return outputFile;
                }
                catch
                {
                    await outputFile.DeleteAsync();
                    return null;
                }
            });
        }

        private static async Task<bool> ImageFileThumbnailImageWriteToStreamAsync(StorageFile file, Stream outputStream)
        {
            using (var fileStream = await file.OpenStreamForReadAsync())
            {
                await RandomAccessStream.CopyAsync(fileStream.AsRandomAccessStream(), outputStream.AsOutputStream());
                return true;
            }
        }
        private static async Task<bool> ZipFileThumbnailImageWriteToStreamAsync(StorageFile file, Stream outputStream)
        {
            using (var archiveStream = await file.OpenStreamForReadAsync())
            using (var zipArchive = new ZipArchive(archiveStream))
            {
                var archiveImageItem = zipArchive.Entries.AsParallel().OrderBy(x=> x.Name).FirstOrDefault(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Name));
                if (archiveImageItem == null) { return false; }

                using (var inputStream = archiveImageItem.Open())
                {
                    await inputStream.CopyToAsync(outputStream);
                    await outputStream.FlushAsync();
                }

                return true;
            }
        }

        private static async Task<bool> RarFileThumbnailImageWriteToStreamAsync(StorageFile file, Stream outputStream)
        {
            using (var archiveStream = await file.OpenAsync(FileAccessMode.Read))
            using (var rarArchive = RarArchive.Open(archiveStream.AsStreamForRead()))
            {
                var archiveImageItem = rarArchive.Entries.AsParallel().OrderBy(x => x.Key).FirstOrDefault(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Key));                
                if (archiveImageItem == null) { return false; }

                using (var inputStream = archiveImageItem.OpenEntryStream())
                {
                    await inputStream.CopyToAsync(outputStream);
                    await outputStream.FlushAsync();
                }

                return true;
            }
        }

        private static async Task<bool> PdfFileThumbnailImageWriteToStreamAsync(StorageFile file, Stream outputStream)
        {
            var pdfDocument = await PdfDocument.LoadFromFileAsync(file);
            if (pdfDocument.PageCount == 0) { return false; }

            var page = pdfDocument.GetPage(0);
            await page.RenderToStreamAsync(outputStream.AsRandomAccessStream());
            return true;
        }

        public async Task<Uri> GetFolderThumbnailAsync(StorageFolder folder)
        {
            var itemId = GetStorageItemId(folder);
            if (await ApplicationData.Current.TemporaryFolder.FileExistsAsync(itemId))
            {
                var cachedFile = await ApplicationData.Current.TemporaryFolder.GetFileAsync(itemId);
                return new Uri(cachedFile.Path, UriKind.Absolute);
            }
            else
            {
#if WINDOWS_UWP
                var query = folder.CreateFileQueryWithOptions(new QueryOptions(CommonFileQuery.OrderByName, SupportedFileTypesHelper.SupportedImageFileExtensions) { FolderDepth = FolderDepth.Deep });
                var count = await query.GetItemCountAsync();
                if (count == 0) { return null; }
                var files = await query.GetFilesAsync(0, 1);
                var file = files[0];
                var tempFolder = await GetTempFolderAsync();
                var thumbnailFile = await tempFolder.CreateFileAsync(itemId, CreationCollisionOption.ReplaceExisting);
                await GenerateThumbnailImageAsync(file, thumbnailFile);
                return new Uri(thumbnailFile.Path);
#else
                return null;
#endif
            }
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
    }
}
