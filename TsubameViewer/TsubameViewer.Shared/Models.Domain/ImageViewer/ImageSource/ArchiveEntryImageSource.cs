using Prism.Mvvm;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Uno.Extensions;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace TsubameViewer.Models.Domain.ImageViewer.ImageSource
{
    public sealed class ArchiveEntryImageSource : IImageSource, IDisposable
    {
        private readonly IArchiveEntry _entry;

        public ArchiveEntryImageSource(IArchiveEntry entry, IStorageItem storageItem)
        {
            _entry = entry;
            StorageItem = storageItem;
            DateCreated = entry.CreatedTime ?? entry.LastModifiedTime ?? entry.ArchivedTime ?? DateTime.Now;
        }

        public IStorageItem StorageItem { get; }


        public string Name => _entry.Key;

        public DateTime DateCreated { get; }

        CancellationTokenSource _cts = new CancellationTokenSource();
        
        public async Task<BitmapImage> GenerateBitmapImageAsync(CancellationToken ct)
        {
            using (var entryStream = _entry.OpenEntryStream())
            using (var memoryStream = entryStream.ToMemoryStream())
            {
                ct.ThrowIfCancellationRequested();

                var bitmapImage = new BitmapImage();
                await bitmapImage.SetSourceAsync(memoryStream.AsRandomAccessStream()).AsTask(ct);

                ct.ThrowIfCancellationRequested();
                
                return bitmapImage;
            }
        }

        public void CancelLoading()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        public void Dispose()
        {
            ((IDisposable)_cts).Dispose();
        }


        public async Task<BitmapImage> GenerateThumbnailBitmapImageAsync(CancellationToken ct = default)
        {
            using (var entryStream = _entry.OpenEntryStream())
            using (var memoryStream = entryStream.ToMemoryStream())
            {
                ct.ThrowIfCancellationRequested();

                var bitmapImage = new BitmapImage();
                await bitmapImage.SetSourceAsync(memoryStream.AsRandomAccessStream()).AsTask(ct);

                ct.ThrowIfCancellationRequested();

                bitmapImage.DecodePixelWidth = FolderItemListing.ListingImageConstants.MidiumFileThumbnailImageWidth;

                return bitmapImage;
            }
        }
    }
}
