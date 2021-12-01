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
        private readonly ThumbnailManager _thumbnailManager;

        public PdfPageImageSource(PdfPage pdfPage, StorageFile storageItem, ThumbnailManager thumbnailManager)
        {
            _pdfPage = pdfPage;
            Name = (_pdfPage.Index + 1).ToString();
            DateCreated = storageItem.DateCreated.DateTime;
            StorageItem = storageItem;
            _thumbnailManager = thumbnailManager;
        }

        public string Name { get; }

        public string Path => Name;
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
            var memoryStream = new InMemoryRandomAccessStream();
            {
                await _pdfPage.RenderToStreamAsync(memoryStream).AsTask(ct);

                ct.ThrowIfCancellationRequested();

                await memoryStream.FlushAsync();
                memoryStream.Seek(0);

                ct.ThrowIfCancellationRequested();
            }

            return memoryStream;
        }

        public void Dispose()
        {
            ((IDisposable)_pdfPage).Dispose();
        }

        public ThumbnailManager.ThumbnailSize? GetThumbnailSize()
        {
            return _thumbnailManager.GetThubmnailOriginalSize(_thumbnailManager.GetArchiveEntryPath(StorageItem, _pdfPage));
        }
    }
}
