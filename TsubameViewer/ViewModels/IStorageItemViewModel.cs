using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.ImageViewer;
using Windows.UI.Xaml.Media.Imaging;

namespace TsubameViewer.ViewModels;
public interface IStorageItemViewModel : INotifyPropertyChanged
{
    DateTimeOffset DateCreated { get; }
    BitmapImage Image { get; set; }
    float? ImageAspectRatioWH { get; set; }
    bool IsFavorite { get; set; }
    bool IsSelected { get; set; }
    bool IsSourceStorageItem { get; }
    IImageSource Item { get; }
    string Name { get; }
    string Path { get; }
    double ReadParcentage { get; set; }
    SelectionContext Selection { get; }
    StorageItemTypes Type { get; }

    void Dispose();
    ValueTask InitializeAsync(CancellationToken ct);
    void RestoreThumbnailLoadingTask(CancellationToken ct);
    void StopImageLoading();
    void ThumbnailChanged();
    void UpdateLastReadPosition();
}