using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media.Imaging;

namespace TsubameViewer.Models.Domain
{
    public interface IImageGenerater
    {
        bool IsImageGenerated { get; }

        Task<BitmapImage> GenerateBitmapImageAsync();
    }
}
