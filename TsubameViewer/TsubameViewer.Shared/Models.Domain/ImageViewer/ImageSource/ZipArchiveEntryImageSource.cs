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
        public static async Task<ImageCollectionManager.GetImagesFromArchiveResult>
            GetImagesFromZipFileAsync(StorageFile file)
        {
            CompositeDisposable disposables = new CompositeDisposable();
            var stream = await file.OpenStreamForReadAsync()
                .AddTo(disposables);
            var zipArchive = new ZipArchive(stream)
                .AddTo(disposables);

            var supportedEntries = zipArchive.Entries
                .OrderBy(x => x.FullName)
                .Where(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Name))
                .Select(x => (IImageSource)new ZipArchiveEntryImageSource(x))
                .ToArray();

            return new ImageCollectionManager.GetImagesFromArchiveResult()
            {
                ItemsCount = (uint)supportedEntries.Length,
                Disposer = disposables,
                Images = supportedEntries,
            };
        }

        private readonly ZipArchiveEntry _entry;
        public ZipArchiveEntryImageSource(ZipArchiveEntry entry)
        {
            _entry = entry;
        }

        void IDisposable.Dispose()
        {
            
        }

        public string Name => _entry.FullName;

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
    }
}
