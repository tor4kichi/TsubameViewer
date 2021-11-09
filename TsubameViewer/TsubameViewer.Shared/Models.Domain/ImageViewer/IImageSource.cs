using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace TsubameViewer.Models.Domain.ImageViewer
{
    public interface IImageSource
    {
        IStorageItem StorageItem { get; }
        string Name { get; }
        DateTime DateCreated { get; }
        Task<IRandomAccessStream> GetThumbnailImageStreamAsync(CancellationToken ct = default);

        Task<IRandomAccessStream> GetImageStreamAsync(CancellationToken ct = default);
    }
}
