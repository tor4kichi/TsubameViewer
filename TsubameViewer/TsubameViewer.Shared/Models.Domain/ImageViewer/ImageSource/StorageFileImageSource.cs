using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.UI.Xaml.Media.Imaging;

namespace TsubameViewer.Models.Domain.ImageViewer.ImageSource
{
    public sealed class StorageFileImageSource : IImageSource, IDisposable
    {
        public static async Task<(uint ItemsCount, IAsyncEnumerable<IImageSource> Images)> GetImagesFromFolderAsync(StorageFolder storageFolder, CancellationToken ct)
        {
#if WINDOWS_UWP
            var query = storageFolder.CreateFileQuery();
            var itemsCount = await query.GetItemCountAsync();
            return (itemsCount, AsyncEnumerableImages(itemsCount, query, ct));
#else
            return (itemsCount, AsyncEnumerableImages(
#endif
        }
#if WINDOWS_UWP
        private static async IAsyncEnumerable<IImageSource> AsyncEnumerableImages(uint count, StorageFileQueryResult queryResult, [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var item in FolderHelper.GetEnumerator(queryResult, count, ct))
            {
                yield return new StorageFileImageSource(item as StorageFile);
            }
        }
#else
                
#endif



        private readonly StorageFile _file;

        public StorageFileImageSource(StorageFile file)
        {
            _file = file;
        }

        public void Dispose()
        {
        }

        public string Name => _file.Name;
        
        public async Task<BitmapImage> GenerateBitmapImageAsync(CancellationToken ct)
        {
            using (var stream = await _file.OpenReadAsync().AsTask(ct))
            {
                var bitmapImage = new BitmapImage();
                await bitmapImage.SetSourceAsync(stream).AsTask(ct);

                ct.ThrowIfCancellationRequested();

                return bitmapImage;
            }
        }

        public void CancelLoading()
        {

        }

    }
}
