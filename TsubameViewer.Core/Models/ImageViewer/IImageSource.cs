using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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

    SizeF? PreCulcuratedSize { get; }
    ValueTask<SizeF?> TryGetSizedImageStreamAsync(int requestedSize, Stream imageStream, CancellationToken ct = default);
    ValueTask<Stream> GetImageStreamAsync(CancellationToken ct = default);                 
}

public static class ImageSourceExtensions
{
    public static bool IsStorageItemNotFound(this IImageSource imageSource)
    {
        return imageSource.StorageItem is null;
    }
}

public sealed class IImageSourceEqualityComparer : EqualityComparer<IImageSource>
{
    public static readonly IImageSourceEqualityComparer Default = new IImageSourceEqualityComparer();
    public override bool Equals(IImageSource x, IImageSource y)
    {
        return x.Equals(y);
    }

    public override int GetHashCode(IImageSource obj)
    {
        return obj.GetHashCode();
    }
}
