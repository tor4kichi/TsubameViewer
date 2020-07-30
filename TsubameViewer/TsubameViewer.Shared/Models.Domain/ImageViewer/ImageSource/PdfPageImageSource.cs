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
    public sealed class PdfPageImageSource : IImageSource, IDisposable
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

        public async Task<BitmapImage> GenerateBitmapImageAsync(CancellationToken ct = default)
        {
            using (var memoryStream = new MemoryStream())
            using (var streamWrite = new StreamWriter(memoryStream))
            {
                await _pdfPage.RenderToStreamAsync(memoryStream.AsRandomAccessStream()).AsTask(ct);

                ct.ThrowIfCancellationRequested();

                await memoryStream.FlushAsync();
                memoryStream.Seek(0, SeekOrigin.Begin);

                ct.ThrowIfCancellationRequested();

                var bitmapImage = new BitmapImage();
                bitmapImage.SetSource(memoryStream.AsRandomAccessStream());
                return bitmapImage;
            }
        }

        public void Dispose()
        {
            ((IDisposable)_pdfPage).Dispose();
        }
    }
}
