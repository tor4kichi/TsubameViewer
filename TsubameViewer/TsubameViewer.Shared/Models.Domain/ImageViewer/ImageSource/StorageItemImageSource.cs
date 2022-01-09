using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Infrastructure;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace TsubameViewer.Models.Domain.ImageViewer.ImageSource
{
    public sealed class StorageItemImageSource : IImageSource
    {
        private readonly FolderListingSettings _folderListingSettings;
        private readonly ThumbnailManager _thumbnailManager;

        public IStorageItem StorageItem { get; }

        public StorageItemTypes ItemTypes { get; }

        public string Name => StorageItem.Name;

        public string Path => StorageItem.Path;

        public DateTime DateCreated => StorageItem.DateCreated.DateTime;

        private readonly AsyncLock _fileLock = new AsyncLock();

        /// <summary>
        /// Tokenで取得されたファイルやフォルダ
        /// </summary>
        /// <param name="storageItem"></param>
        /// <param name="thumbnailManager"></param>
        public StorageItemImageSource(IStorageItem storageItem, FolderListingSettings folderListingSettings, ThumbnailManager thumbnailManager)
        {
            StorageItem = storageItem;
            _folderListingSettings = folderListingSettings;
            _thumbnailManager = thumbnailManager;
            ItemTypes = SupportedFileTypesHelper.StorageItemToStorageItemTypes(StorageItem);
        }

        public async Task<IRandomAccessStream> GetImageStreamAsync(CancellationToken ct)
        {
            using (await _fileLock.LockAsync(ct))
            {
                if (StorageItem is StorageFile file
                    && SupportedFileTypesHelper.IsSupportedImageFileExtension(file.FileType))
                {
                    return await file.OpenReadAsync().AsTask(ct);
                }
                else
                {
                    throw new NotSupportedException();
                }
            }
        }

        public async Task<IRandomAccessStream> GetThumbnailImageStreamAsync(CancellationToken ct)
        {
            using (await _fileLock.LockAsync(ct))
            {
                if (StorageItem is StorageFile file)
                {
                    if (SupportedFileTypesHelper.IsSupportedImageFileExtension(file.FileType))
                    {
                        if (_folderListingSettings.IsImageFileGenerateThumbnailEnabled)
                        {
                            return await _thumbnailManager.GetFileThumbnailImageFileAsync(file, ct);
                        }
                        else
                        {
                            return await _thumbnailManager.GetFileThumbnailImageStreamAsync(file, ct);
                        }
                    }
                    else if (SupportedFileTypesHelper.IsSupportedArchiveFileExtension(file.FileType)
                        || SupportedFileTypesHelper.IsSupportedEBookFileExtension(file.FileType)
                        )
                    {
                        if (_folderListingSettings.IsArchiveFileGenerateThumbnailEnabled)
                        {
                            return await _thumbnailManager.GetFileThumbnailImageFileAsync(file, ct);
                        }
                        else
                        {
                            return await _thumbnailManager.GetFileThumbnailImageStreamAsync(file, ct);
                        }
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }
                else if (StorageItem is StorageFolder folder)
                {
                    if (_folderListingSettings.IsFolderGenerateThumbnailEnabled)
                    {
                        return await _thumbnailManager.GetFolderThumbnailImageFileAsync(folder, ct);
                    }
                    else
                    {
                        return await _thumbnailManager.GetFolderThumbnailImageStreamAsync(folder, ct);
                    }
                }
                else
                {
                    throw new NotSupportedException();
                }
            }
        }

        public ThumbnailManager.ThumbnailSize? GetThumbnailSize()
        {
            return _thumbnailManager.GetThumbnailOriginalSize(StorageItem);
        }
    }
}
