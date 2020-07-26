using LiteDB;
using Microsoft.Toolkit.Uwp.Helpers;
using MonkeyCache;
using SharpCompress.Archives.Rar;
using System;
using System.Collections.Generic;
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
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Search;
using Windows.Storage.Streams;

namespace TsubameViewer.Models.Domain.FolderItemListing
{
    public sealed class ThumbnailManager
    {
        public static async Task DeleteAllThumbnailCacheAsync()
        {
            var cacheFolder = await GetTempFolderAsync();
            var files = await cacheFolder.GetFilesAsync();
            foreach (var file in files)
            {
                await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
            }
        }


        public ThumbnailManager(
            )
        {
        }

        private static async ValueTask<StorageFolder> GetTempFolderAsync()
        {
            return ApplicationData.Current.TemporaryFolder;
        }

        SemaphoreSlim _writeerLock = new SemaphoreSlim(2, 2);

        Dictionary<string, string> _FilePathToHashCodeStringMap = new Dictionary<string, string>();

        private string GetStorageItemId(StorageFile item)
        {
            return _FilePathToHashCodeStringMap.TryGetValue(item.Path, out var code)
                ? code
                : _FilePathToHashCodeStringMap[item.Path] = item.Path.GetHashCode().ToString()
                ;
        }

        private string GetStorageItemId(StorageFolder item)
        {
            return _FilePathToHashCodeStringMap.TryGetValue(item.Path, out var code)
                ? code
                : _FilePathToHashCodeStringMap[item.Path] = item.Path.GetHashCode().ToString()
                ;
        }

        public async Task<Uri> GetArchiveThumbnailAsync(StorageFile file)
        {
            await _writeerLock.WaitAsync();

            try
            {
                var itemId = GetStorageItemId(file);
                if (await ApplicationData.Current.TemporaryFolder.FileExistsAsync(itemId))
                {
                    var cachedFile = await ApplicationData.Current.TemporaryFolder.GetFileAsync(itemId);
                    return new Uri(cachedFile.Path, UriKind.Absolute);
                }
                else
                {
                    var tempFolder = await GetTempFolderAsync();
                    var thumbnailFile = await tempFolder.CreateFileAsync(itemId, CreationCollisionOption.ReplaceExisting);
                    try
                    {
                        using (var thumbnailWriteStream = await thumbnailFile.OpenStreamForWriteAsync())
                        {
                            var result = await (file.FileType switch
                            {
                                SupportedFileTypesHelper.ZipFileType => ZipFileThumbnailImageWriteToStreamAsync(file, thumbnailWriteStream),
                                SupportedFileTypesHelper.RarFileType => RarFileThumbnailImageWriteToStreamAsync(file, thumbnailWriteStream),
                                SupportedFileTypesHelper.PdfFileType => PdfFileThumbnailImageWriteToStreamAsync(file, thumbnailWriteStream),
                                _ => throw new NotSupportedException(),
                            });
                            if (!result) { return null; }
                        }
                        return new Uri(thumbnailFile.Path);
                    }
                    catch
                    {
                        await thumbnailFile.DeleteAsync();
                        return null;
                    }
                }
            }
            finally
            {
                _writeerLock.Release();
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
                using (var readStream = await file.OpenStreamForReadAsync())
                using (var outputStream = await thumbnailFile.OpenStreamForWriteAsync())
                {
                    await readStream.CopyToAsync(outputStream);
                    await outputStream.FlushAsync();
                }

                return new Uri(thumbnailFile.Path);
#else
                return null;
#endif
            }
        }
    }
}
