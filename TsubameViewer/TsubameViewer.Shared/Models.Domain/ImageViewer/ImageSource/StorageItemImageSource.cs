using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain.FolderItemListing;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace TsubameViewer.Models.Domain.ImageViewer.ImageSource
{
    public sealed class StorageItemImageSource : IImageSource
    {
        private readonly ThumbnailManager _thumbnailManager;

        public IStorageItem StorageItem { get; }

        public StorageItemTypes ItemTypes { get; }

        public string Name => StorageItem.Name;

        public string Path => StorageItem.Path;

        public DateTime DateCreated => StorageItem.DateCreated.DateTime;


        /// <summary>
        /// Tokenで取得されたファイルやフォルダ
        /// </summary>
        /// <param name="storageItem"></param>
        /// <param name="thumbnailManager"></param>
        public StorageItemImageSource(IStorageItem storageItem, ThumbnailManager thumbnailManager)
        {
            StorageItem = storageItem;
            _thumbnailManager = thumbnailManager;
            ItemTypes = SupportedFileTypesHelper.StorageItemToStorageItemTypes(StorageItem);
        }

        public async Task<IRandomAccessStream> GetImageStreamAsync(CancellationToken ct)
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

        public async Task<IRandomAccessStream> GetThumbnailImageStreamAsync(CancellationToken ct)
        {
            if (StorageItem is StorageFile file)
            {
                if (SupportedFileTypesHelper.IsSupportedImageFileExtension(file.FileType))
                {
                    var thumbnailFile = await _thumbnailManager.GetFileThumbnailImageAsync(file, ct);
                    if (thumbnailFile == null) { return null; }
                    return await thumbnailFile.OpenReadAsync().AsTask(ct);
                }
                else if (SupportedFileTypesHelper.IsSupportedArchiveFileExtension(file.FileType)
                    || SupportedFileTypesHelper.IsSupportedEBookFileExtension(file.FileType)
                    )
                {
                    var thumbnailFile = await _thumbnailManager.GetFileThumbnailImageAsync(file, ct);
                    if (thumbnailFile == null) { return null; }
                    return await thumbnailFile.OpenReadAsync().AsTask(ct);
                }
                else
                {
                    throw new NotSupportedException();
                }
            }
            else if (StorageItem is StorageFolder folder)
            {
                var thumbnailFile = await _thumbnailManager.GetFolderThumbnailAsync(folder, ct);
                if (thumbnailFile == null) { return null; }
                return await thumbnailFile.OpenReadAsync().AsTask(ct);
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        public ThumbnailManager.ThumbnailSize? GetThumbnailSize()
        {
            return _thumbnailManager.GetThubmnailOriginalSize(StorageItem);
        }
    }
}
