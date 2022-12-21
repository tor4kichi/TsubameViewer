using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;
using static TsubameViewer.Core.Models.FolderItemListing.ThumbnailManager;

namespace TsubameViewer.Core.Models.ImageViewer;

public interface IImageSource : IEquatable<IImageSource>
{
    IStorageItem StorageItem { get; }
    string Name { get; }

    string Path { get; }
    DateTime DateCreated { get; }

    ThumbnailSize? GetThumbnailSize();

    Task<IRandomAccessStream> GetThumbnailImageStreamAsync(CancellationToken ct = default);

    Task<IRandomAccessStream> GetImageStreamAsync(CancellationToken ct = default);                 
}

public static class ImageSourceExtensions
{
    public static bool IsStorageItemNotFound(this IImageSource imageSource)
    {
        return imageSource.StorageItem is null;
    }
}
