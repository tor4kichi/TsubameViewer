using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Contracts.Services;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace TsubameViewer.Core.Models.ImageViewer;

public interface IImageSource : IEquatable<IImageSource>
{
    IStorageItem StorageItem { get; }
    string Name { get; }

    string Path { get; }
    DateTime DateCreated { get; }

    ValueTask<IRandomAccessStream> GetImageStreamAsync(CancellationToken ct = default);                 
}

public static class ImageSourceExtensions
{
    public static bool IsStorageItemNotFound(this IImageSource imageSource)
    {
        return imageSource.StorageItem is null;
    }
}
