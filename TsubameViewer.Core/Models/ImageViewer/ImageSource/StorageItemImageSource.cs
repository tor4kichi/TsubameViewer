using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Models.FolderItemListing;
using Windows.Storage;
using Windows.Storage.Streams;

namespace TsubameViewer.Core.Models.ImageViewer.ImageSource;

public sealed class StorageItemImageSource : IImageSource
{
    public IStorageItem StorageItem { get; }

    public StorageItemTypes ItemTypes { get; }

    public string Name => StorageItem.Name;

    public string Path => StorageItem.Path;

    public DateTime DateCreated => StorageItem?.DateCreated.DateTime ?? DateTime.MinValue;

    /// <summary>
    /// Tokenで取得されたファイルやフォルダ
    /// </summary>
    public StorageItemImageSource(
        IStorageItem storageItem
        )
    {
        StorageItem = storageItem;
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
        }
        else
        {
            throw new NotSupportedException();
        }
    }

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
