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
    public sealed class StorageFileImageSource : BindableBase, IImageSource, IDisposable
    {
        public static async Task<(uint ItemsCount, IAsyncEnumerable<IImageSource> Images)> GetImagesFromFolderAsync(StorageFolder storageFolder, CancellationToken ct)
        {
#if WINDOWS_UWP
            var query = storageFolder.CreateItemQuery();
            var itemsCount = await query.GetItemCountAsync();
            return (itemsCount, AsyncEnumerableImages(itemsCount, query, ct));
#else
            return (itemsCount, AsyncEnumerableImages(
#endif
        }
#if WINDOWS_UWP
        private static async IAsyncEnumerable<IImageSource> AsyncEnumerableImages(uint count, StorageItemQueryResult queryResult, [EnumeratorCancellation] CancellationToken ct = default)
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
        public bool IsImageGenerated => _image != null;

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
            using (var stream = await _file.OpenReadAsync())
            {
                var bitmapImage = new BitmapImage();
                bitmapImage.SetSource(stream);
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

        public void CancelLoading()
        {

        }

    }
}
