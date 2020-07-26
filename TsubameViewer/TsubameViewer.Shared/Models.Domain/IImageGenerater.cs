using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media.Imaging;

namespace TsubameViewer.Models.Domain
{
    public interface IImageGenerater : INotifyPropertyChanged
    {
        BitmapImage Image { get; }
    }
}
