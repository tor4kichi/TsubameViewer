using System;
using System.Collections.Generic;
using System.Drawing;
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

    public SizeF? PreCulcuratedSize => null;

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

    public ValueTask<SizeF?> TryGetSizedImageStreamAsync(int requestedSize, Stream imageStream, CancellationToken ct = default)
    {
        return new(default(SizeF?));
    }

    public ValueTask<Stream> GetImageStreamAsync(CancellationToken ct)
    {
        if (StorageItem is StorageFile file)
        {
            if (file.IsSupportedImageFile())
            {
                var fileHandle = file.CreateSafeFileHandle(FileAccess.Read);
                return new (new FileStream(fileHandle, FileAccess.Read));
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
