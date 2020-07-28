using Prism.Mvvm;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
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
    public sealed class RarArchiveEntryImageSource : BindableBase, IImageSource, IDisposable
    {
        public static async Task<ImageCollectionManager.GetImagesFromArchiveResult> 
            GetImagesFromRarFileAsync(StorageFile file)
        {
            CompositeDisposable disposables = new CompositeDisposable();
            var stream = await file.OpenStreamForReadAsync()
                .AddTo(disposables);
            var rarArchive = RarArchive.Open(stream)
                .AddTo(disposables);

            var supportedEntries = rarArchive.Entries
                .Where(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Key))
                .Select(x => (IImageSource)new RarArchiveEntryImageSource(x))
                .OrderBy(x => x.Name)
                .ToArray();

            return new ImageCollectionManager.GetImagesFromArchiveResult() 
            {
                ItemsCount = (uint)supportedEntries.Length,
                Disposer = disposables,
                Images = supportedEntries,
            };
        }


        private readonly RarArchiveEntry _entry;

        public RarArchiveEntryImageSource(RarArchiveEntry entry)
        {
            _entry = entry;
        }

        public string Name => _entry.Key;
        public bool IsImageGenerated => _image != null;

        CancellationTokenSource _cts = new CancellationTokenSource();

        private BitmapImage _image;
        public BitmapImage Image
        {
            get { return _image; }
            private set { SetProperty(ref _image, value); }
        }

        public void ClearImage()
        {
            Image = null;
        }
        
        public async Task<BitmapImage> GenerateBitmapImageAsync(int canvasWidth, int canvasHeight)
        {
            var ct = _cts.Token;
            {
                using (var entryStream = _entry.OpenEntryStream())
                using (var memoryStream = entryStream.ToMemoryStream())
                {
                    var bitmapImage = new BitmapImage();
                    bitmapImage.SetSource(memoryStream.AsRandomAccessStream());
                    if (bitmapImage.PixelHeight > bitmapImage.PixelWidth)
                    {
                        if (bitmapImage.PixelHeight > canvasHeight)
                        {
                            bitmapImage.DecodePixelHeight = canvasHeight;
                        }
                    }
                    else
                    {
                        if (bitmapImage.PixelWidth > canvasWidth)
                        {
                            bitmapImage.DecodePixelWidth = canvasWidth;
                        }
                    }
                    return Image = bitmapImage;
                }
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
    }
}
