using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.ImageViewer;
using Windows.UI.Xaml.Media.Imaging;
#nullable enable
namespace TsubameViewer.ViewModels;
public interface IStorageItemViewModel : INotifyPropertyChanged
{
    DateTimeOffset DateCreated { get; }
    float? ImageAspectRatioWH { get; set; }
    bool IsFavorite { get; set; }
    bool IsSelected { get; set; }
    bool IsSourceStorageItem { get; }
    IImageSource Item { get; }
    string Name { get; }
    string Path { get; }
    double ReadParcentage { get; set; }
    SelectionContext? Selection { get; }
    StorageItemTypes Type { get; }

    string? Duration { get; }

    bool IsRequestImageLoading { get; }
    bool IsInitialized { get; }
    ValueTask InitializeAsync(BitmapImage targetBitmap, CancellationToken ct);
    ValueTask EnsureImageSizeRatioAsync(CancellationToken ct);
    void RestoreThumbnailLoadingTask(BitmapImage targetBitmap, CancellationToken ct);
    void StopImageLoading();
    void ThumbnailChanged();
    void UpdateLastReadPosition();
}