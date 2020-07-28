using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Uno.Disposables;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace TsubameViewer.Models.Domain.ImageViewer.ImageSource
{
    public sealed class PdfPageImageSource : BindableBase, IImageSource, IDisposable
    {
        public static async Task<ImageCollectionManager.GetImagesFromArchiveResult> GetImagesFromPdfFileAsync(StorageFile file)
        {
            var pdfDocument = await PdfDocument.LoadFromFileAsync(file);

            var supportedEntries = Enumerable.Range(0, (int)pdfDocument.PageCount)
                .Select(x => pdfDocument.GetPage((uint)x))
                .Select(x => (IImageSource)new PdfPageImageSource(x))
                .ToArray();

            return new ImageCollectionManager.GetImagesFromArchiveResult()
            {
                ItemsCount = pdfDocument.PageCount,
                Disposer = Disposable.Empty,
                Images = supportedEntries,
            };
        }

        

        private readonly PdfPage _pdfPage;

        public PdfPageImageSource(PdfPage pdfPage)
        {
            _pdfPage = pdfPage;
            Name = (_pdfPage.Index + 1).ToString();
        }

        public string Name { get; }
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
                using (var memoryStream = new MemoryStream())
                using (var streamWrite = new StreamWriter(memoryStream))
                {
                    await _pdfPage.RenderToStreamAsync(memoryStream.AsRandomAccessStream());
                    await memoryStream.FlushAsync();
                    memoryStream.Seek(0, SeekOrigin.Begin);
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

        public void Dispose()
        {
            ((IDisposable)_pdfPage).Dispose();
        }
    }
}
