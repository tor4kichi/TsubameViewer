using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Models.FolderItemListing;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace TsubameViewer.Core.Models.ImageViewer.ImageSource;

public sealed class StorageItemImageSource : IImageSource
{
    private readonly FolderListingSettings _folderListingSettings;

    public IStorageItem StorageItem { get; }

    public StorageItemTypes ItemTypes { get; }

    public string Name => StorageItem.Name;

    public string Path => StorageItem.Path;

    public DateTime DateCreated => StorageItem?.DateCreated.DateTime ?? DateTime.MinValue;

    /// <summary>
    /// Tokenで取得されたファイルやフォルダ
    /// </summary>
    /// <param name="storageItem"></param>
    /// <param name="thumbnailManager"></param>
    public StorageItemImageSource(
        IStorageItem storageItem, 
        FolderListingSettings folderListingSettings
        )
    {
        StorageItem = storageItem;
        _folderListingSettings = folderListingSettings;
        ItemTypes = SupportedFileTypesHelper.StorageItemToStorageItemTypes(StorageItem);
    }

    public async Task<IRandomAccessStream> GetImageStreamAsync(CancellationToken ct)
    {
        if (StorageItem is StorageFile file)
        {
            if (file.IsSupportedImageFile())
            {
                return await file.OpenReadAsync().AsTask(ct);
            }
            else
            {
                throw new NotSupportedException();
            }
        }
        else if (StorageItem is StorageFolder folder)
        {
            throw new NotSupportedException("StorageFolder not present GetImageStreamAsync().");
            //return await _thumbnailManager.GetThumbnailAsync(folder, ct);
        }
        else
        {
            throw new NotSupportedException();
        }
    }

    //public async Task<IRandomAccessStream> GetThumbnailImageStreamAsync(CancellationToken ct)
    //{
    //    if (StorageItem is StorageFile file)
    //    {
    //        if (SupportedFileTypesHelper.IsSupportedImageFileExtension(file.FileType))
    //        {
    //            if (_folderListingSettings.IsImageFileGenerateThumbnailEnabled)
    //            {
    //                return await _thumbnailManager.GetFileThumbnailImageFileAsync(file, ct);
    //            }
    //            else
    //            {
    //                return await _thumbnailManager.GetFileThumbnailImageStreamAsync(file, ct);
    //            }
    //        }
    //        else if (SupportedFileTypesHelper.IsSupportedArchiveFileExtension(file.FileType)
    //            || SupportedFileTypesHelper.IsSupportedEBookFileExtension(file.FileType)
    //            )
    //        {
    //            if (_folderListingSettings.IsArchiveFileGenerateThumbnailEnabled)
    //            {
    //                return await _thumbnailManager.GetFileThumbnailImageFileAsync(file, ct);
    //            }
    //            else
    //            {
    //                return await _thumbnailManager.GetFileThumbnailImageStreamAsync(file, ct);
    //            }
    //        }
    //        else
    //        {
    //            throw new NotSupportedException();
    //        }
    //    }
    //    else if (StorageItem is StorageFolder folder)
    //    {
    //        if (_folderListingSettings.IsFolderGenerateThumbnailEnabled)
    //        {
    //            return await _thumbnailManager.GetFolderThumbnailImageFileAsync(folder, ct);
    //        }
    //        else
    //        {
    //            return await _thumbnailManager.GetFolderThumbnailImageStreamAsync(folder, ct);
    //        }
    //    }
    //    else
    //    {
    //        throw new NotSupportedException();
    //    }
    //}

    //public ThumbnailSize? GetThumbnailSize()
    //{
    //    return _thumbnailManager.GetThumbnailOriginalSize(StorageItem);
    //}

    public bool Equals(IImageSource other)
    {
        if (other == null) { return false; }
        return this.Path == other.Path;
    }

    public override string ToString()
    {
        return Path;
    }
}
