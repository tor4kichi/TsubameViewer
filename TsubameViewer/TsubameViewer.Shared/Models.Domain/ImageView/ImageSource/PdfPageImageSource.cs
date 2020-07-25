using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Uno.Disposables;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace TsubameViewer.Models.Domain.ImageView.ImageSource
{
    public sealed class PdfPageImageSource : IImageSource, IDisposable
    {
        public static async Task<ImageCollectionManager.GetImagesFromArchiveResult> GetImagesFromPdfFileAsync(StorageFile file)
        {
            var pdfDocument = await PdfDocument.LoadFromFileAsync(file);

            var supportedEntries = Enumerable.Range(0, (int)pdfDocument.PageCount)
                .Select(x => pdfDocument.GetPage((uint)x))
                .Select(x => (IImageSource)new PdfPageImageSource(x))
                .OrderBy(x => x.Name)
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


        public async Task<BitmapImage> GenerateBitmapImageAsync()
        {
            var ct = _cts.Token;
            {
                if (_image != null) { return _image; }

                using (var memoryStream = new InMemoryRandomAccessStream())
                {
                    await _pdfPage.RenderToStreamAsync(memoryStream);
                    await memoryStream.FlushAsync();
                    memoryStream.Seek(0);
                    var bitmapImage = new BitmapImage();
                    bitmapImage.SetSource(memoryStream);
                    return _image = bitmapImage;
                }
            }
        }

        public void Dispose()
        {
            ((IDisposable)_pdfPage).Dispose();
        }
    }
}
