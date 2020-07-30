using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media.Imaging;

namespace TsubameViewer.Models.Domain.ImageViewer
{
    public interface IImageSource
    {
        string Name { get; }

        Task<BitmapImage> GenerateBitmapImageAsync(CancellationToken ct = default);
    }
}
