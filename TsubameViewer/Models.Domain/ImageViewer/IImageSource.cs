using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;
using static TsubameViewer.Models.Domain.FolderItemListing.ThumbnailManager;

namespace TsubameViewer.Models.Domain.ImageViewer
{
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
}
