using Hnx8.ReadJEnc;
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
    public sealed class RarArchiveEntryImageSource : IImageSource, IDisposable
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
                .OrderBy(x => x.Key)
                .Select(x => (IImageSource)new RarArchiveEntryImageSource(x))
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
    }
}
