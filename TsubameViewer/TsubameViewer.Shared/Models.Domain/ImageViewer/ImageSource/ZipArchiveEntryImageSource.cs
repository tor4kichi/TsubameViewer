using Hnx8.ReadJEnc;
using Prism.Mvvm;
using Reactive.Bindings.Extensions;
using SharpCompress.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Uno.Extensions;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;


namespace TsubameViewer.Models.Domain.ImageViewer.ImageSource
{
    public sealed class ZipArchiveEntryImageSource : IImageSource, IDisposable
    {
        private readonly ZipArchiveEntry _entry;
        public ZipArchiveEntryImageSource(ZipArchiveEntry entry, IStorageItem storageItem)
        {
            _entry = entry;
            StorageItem = storageItem;
        }

        void IDisposable.Dispose()
        {
            
        }

        public string Name => _entry.FullName;
        public DateTime DateCreated => _entry.LastWriteTime.DateTime;

        public IStorageItem StorageItem { get; }

        public async Task<BitmapImage> GenerateBitmapImageAsync(CancellationToken ct)
        {
            using (var entryStream = _entry.Open())
            using (var memoryStream = entryStream.ToMemoryStream())
            {
                ct.ThrowIfCancellationRequested();

                var bitmapImage = new BitmapImage();
                await bitmapImage.SetSourceAsync(memoryStream.AsRandomAccessStream()).AsTask(ct);

                return bitmapImage;
            }
        }

        public async Task<BitmapImage> GenerateThumbnailBitmapImageAsync(CancellationToken ct = default)
        {
            using (var entryStream = _entry.Open())
            using (var memoryStream = entryStream.ToMemoryStream())
            {
                ct.ThrowIfCancellationRequested();

                var bitmapImage = new BitmapImage();
                await bitmapImage.SetSourceAsync(memoryStream.AsRandomAccessStream()).AsTask(ct);

                bitmapImage.DecodePixelWidth = FolderItemListing.ListingImageConstants.MidiumFileThumbnailImageWidth;

                return bitmapImage;
            }
        }
    }
}
