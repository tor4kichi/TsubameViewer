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
using Windows.UI.Xaml.Media.Imaging;

namespace TsubameViewer.Models.Domain.ImageViewer.ImageSource
{
    public sealed class StorageItemImageSource : IImageSource, IDisposable
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

        public void Dispose()
        {
        }

        public async Task<BitmapImage> GenerateBitmapImageAsync(CancellationToken ct)
        {
            if (StorageItem is StorageFile file
                && SupportedFileTypesHelper.IsSupportedImageFileExtension(file.FileType))
            {
                using (var stream = await file.OpenReadAsync().AsTask(ct))
                {
                    var bitmapImage = new BitmapImage();
                    await bitmapImage.SetSourceAsync(stream).AsTask(ct);

                    ct.ThrowIfCancellationRequested();

                    return bitmapImage;
                }
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        public async Task<BitmapImage> GenerateThumbnailBitmapImageAsync(CancellationToken ct)
        {
            if (StorageItem is StorageFile file)
            {
                if (SupportedFileTypesHelper.IsSupportedImageFileExtension(file.FileType))
                {
                    //var image = new BitmapImage(new Uri(file.Path, UriKind.Absolute));
                    using (var thumbImage = await file.GetScaledImageAsThumbnailAsync(Windows.Storage.FileProperties.ThumbnailMode.SingleItem, (uint)ListingImageConstants.LargeFileThumbnailImageHeight))
                    {
                        var image = new BitmapImage();
                        image.SetSource(thumbImage);
                        return image;
                    }
                }
                else if (SupportedFileTypesHelper.IsSupportedArchiveFileExtension(file.FileType))
                {

                    var thumbnailFile = await _thumbnailManager.GetArchiveThumbnailAsync(file);
                    if (thumbnailFile == null) { return null; }
                    var image = new BitmapImage();

                    using (var stream = await thumbnailFile.OpenStreamForReadAsync())
                    {
                        image.SetSource(stream.AsRandomAccessStream());
                    }

                    return image;
                }
            }
            else if (StorageItem is StorageFolder folder)
            {

                var uri = await _thumbnailManager.GetFolderThumbnailAsync(folder);
                if (uri == null) { return null; }
                var image = new BitmapImage(uri);
                return image;
            }

            return new BitmapImage();
        }

        public void CancelLoading()
        {

        }

    }
}
