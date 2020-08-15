using LiteDB;
using Microsoft.Toolkit.Uwp.Helpers;
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


        public const string SecondaryTileThumbnailSaveFolderName = "SecondaryTile";
        static StorageFolder _SecondaryTileThumbnailFolder;
        public static async ValueTask<StorageFolder> GetSecondaryTileThumbnailFolderAsync()
        {
            return _SecondaryTileThumbnailFolder ??= await ApplicationData.Current.LocalFolder.CreateFolderAsync(SecondaryTileThumbnailSaveFolderName, CreationCollisionOption.OpenIfExists);
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

        public async Task DeleteFromPath(string path)
        {
            _thumbnailImageInfoRepository.DeleteItem(path);
            var tempFolder = await GetTempFolderAsync();
            var itemId = GetStorageItemId(path);
            if (await tempFolder.FileExistsAsync(itemId))
            {
                var file = await ApplicationData.Current.TemporaryFolder.GetFileAsync(itemId);
                await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
            }
        }


        Dictionary<string, string> _FilePathToHashCodeStringMap = new Dictionary<string, string>();


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

        public async Task<StorageFile> GetFileThumbnailImageAsync(StorageFile file, CancellationToken ct = default)
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
                            SupportedFileTypesHelper.CbzFileType => ZipFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite()),
                            SupportedFileTypesHelper.CbrFileType => RarFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite()),
                            SupportedFileTypesHelper.SevenZipFileType => SevenZipFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite()),
                            SupportedFileTypesHelper.Cb7FileType => SevenZipFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite()),
                            SupportedFileTypesHelper.TarFileType => TarFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite()),


                            SupportedFileTypesHelper.JpgFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite()),
                            SupportedFileTypesHelper.JpegFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite()),
                            SupportedFileTypesHelper.PngFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite()),
                            SupportedFileTypesHelper.BmpFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite()),
                            SupportedFileTypesHelper.GifFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite()),
                            SupportedFileTypesHelper.TifFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite()),
                            SupportedFileTypesHelper.TiffFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite()),
                            SupportedFileTypesHelper.SvgFileType => ImageFileThumbnailImageWriteToStreamAsync(file, stream.AsStreamForWrite()),

                            SupportedFileTypesHelper.EPubFileType => EPubFileThubnailImageWriteToStreamAsync(file, stream.AsStreamForWrite()),
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
                var archiveImageItem = zipArchive.Entries.OrderBy(x=> x.Name).FirstOrDefault(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Name));
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
                var archiveImageItem = rarArchive.Entries.OrderBy(x => x.Key).FirstOrDefault(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Key));                
                if (archiveImageItem == null) { return false; }

                using (var inputStream = archiveImageItem.OpenEntryStream())
                {
                    await inputStream.CopyToAsync(outputStream);
                    await outputStream.FlushAsync();
                }

                return true;
            }
        }

        private static async Task<bool> SevenZipFileThumbnailImageWriteToStreamAsync(StorageFile file, Stream outputStream)
        {
            using (var archiveStream = await file.OpenStreamForReadAsync())
            using (var zipArchive = SevenZipArchive.Open(archiveStream))
            {
                var archiveImageItem = zipArchive.Entries.OrderBy(x => x.Key).FirstOrDefault(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Key));
                if (archiveImageItem == null) { return false; }

                using (var inputStream = archiveImageItem.OpenEntryStream())
                {
                    await inputStream.CopyToAsync(outputStream);
                    await outputStream.FlushAsync();
                }

                return true;
            }
        }

        private static async Task<bool> TarFileThumbnailImageWriteToStreamAsync(StorageFile file, Stream outputStream)
        {
            using (var archiveStream = await file.OpenStreamForReadAsync())
            using (var zipArchive = TarArchive.Open(archiveStream))
            {
                var archiveImageItem = zipArchive.Entries.OrderBy(x => x.Key).FirstOrDefault(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Key));
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

        public async Task<Uri> GetThumbnailAsync(IStorageItem storageItem)
        {
            if (storageItem is StorageFolder folder)
            {
                return await GetFolderThumbnailAsync(folder);
            }
            else if (storageItem is StorageFile file)
            {
                var thumb = await GetFileThumbnailImageAsync(file);
                return new Uri(thumb.Path);
            }
            else
            {
                throw new NotSupportedException();
            }
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
                var query = folder.CreateFileQueryWithOptions(new QueryOptions(CommonFileQuery.OrderByName, SupportedFileTypesHelper.GetAllSupportedFileExtensions()) { FolderDepth = FolderDepth.Deep });
                var count = await query.GetItemCountAsync();

                if (count == 0) { return null; }

                var tempFolder = await GetTempFolderAsync();

                var thumbnailFile = await tempFolder.CreateFileAsync(itemId);
                var files = await query.GetFilesAsync(0, 1);
                var outputFile = await GenerateThumbnailImageAsync(files[0], thumbnailFile);
                return new Uri(outputFile.Path);
#else
                return null;
#endif
            }
        }

        private async Task<bool> EPubFileThubnailImageWriteToStreamAsync(StorageFile file, Stream outputStream)
        {
            using var fileStream = await file.OpenStreamForReadAsync();

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

        public async Task<GenerateSecondaryTileThumbnailResult> GenerateSecondaryThumbnailImageAsync(StorageFolder folder)
        {
            var itemId = GetStorageItemId(folder);

#if WINDOWS_UWP
            var query = folder.CreateFileQueryWithOptions(new QueryOptions(CommonFileQuery.OrderByName, SupportedFileTypesHelper.GetAllSupportedFileExtensions()) { FolderDepth = FolderDepth.Deep });
            var count = await query.GetItemCountAsync();

            if (count == 0) { return null; }

            var files = await query.GetFilesAsync(0, 1);
            return await GenerateSecondaryThumbnailImageAsync(files[0], itemId);
#endif
        }

        public Task<GenerateSecondaryTileThumbnailResult> GenerateSecondaryThumbnailImageAsync(StorageFile file)
        {
            var itemId = GetStorageItemId(file);
            return GenerateSecondaryThumbnailImageAsync(file, itemId);
        }

        private  Task<GenerateSecondaryTileThumbnailResult> GenerateSecondaryThumbnailImageAsync(StorageFile file, string itemId)
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
                            SupportedFileTypesHelper.EPubFileType => EPubFileThubnailImageWriteToStreamAsync(file, stream.AsStreamForWrite()),
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
            });
        }

        #endregion
    }
}
