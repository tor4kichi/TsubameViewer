using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace TsubameViewer.Models.Domain.ImageViewer
{
    public interface IImageSource
    {
        IStorageItem StorageItem { get; }
        string Name { get; }
        DateTime DateCreated { get; }
        Task<BitmapImage> GenerateBitmapImageAsync(CancellationToken ct = default);
        Task<BitmapImage> GenerateThumbnailBitmapImageAsync(CancellationToken ct = default);
    }
}
