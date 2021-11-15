using Microsoft.IO;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain.FolderItemListing;
using Uno.Disposables;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace TsubameViewer.Models.Domain.ImageViewer.ImageSource
{
    public sealed class PdfPageImageSource : IImageSource, IDisposable
    {
        private readonly PdfPage _pdfPage;
        private readonly RecyclableMemoryStreamManager _recyclableMemoryStreamManager;
        private readonly ThumbnailManager _thumbnailManager;

        public PdfPageImageSource(PdfPage pdfPage, StorageFile storageItem, RecyclableMemoryStreamManager recyclableMemoryStreamManager, ThumbnailManager thumbnailManager)
        {
            _pdfPage = pdfPage;
            Name = (_pdfPage.Index + 1).ToString();
            DateCreated = storageItem.DateCreated.DateTime;
            StorageItem = storageItem;
            _recyclableMemoryStreamManager = recyclableMemoryStreamManager;
            _thumbnailManager = thumbnailManager;
        }

        public string Name { get; }
        public DateTime DateCreated { get; }
        public StorageFile StorageItem { get; }

        IStorageItem IImageSource.StorageItem => StorageItem;

        public async Task<IRandomAccessStream> GetThumbnailImageStreamAsync(CancellationToken ct)
        {
            var thumbnailFile = await _thumbnailManager.GetPdfPageThumbnailImageAsync(StorageItem, _pdfPage, ct);
            var stream = await thumbnailFile.OpenStreamForReadAsync();
            return stream.AsRandomAccessStream();
        }

        public async Task<IRandomAccessStream> GetImageStreamAsync(CancellationToken ct)
        {
            var memoryStream = _recyclableMemoryStreamManager.GetStream();
            var stream = memoryStream.AsRandomAccessStream();
            {
                await _pdfPage.RenderToStreamAsync(stream).AsTask(ct);

                ct.ThrowIfCancellationRequested();

                await memoryStream.FlushAsync();
                memoryStream.Seek(0, SeekOrigin.Begin);

                ct.ThrowIfCancellationRequested();
            }

            return stream;
        }

        public void Dispose()
        {
            ((IDisposable)_pdfPage).Dispose();
        }

    }
}
