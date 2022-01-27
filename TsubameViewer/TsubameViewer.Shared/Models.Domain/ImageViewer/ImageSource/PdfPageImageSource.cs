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
        private readonly FolderListingSettings _folderListingSettings;
        private readonly ThumbnailManager _thumbnailManager;

        public PdfPageImageSource(PdfPage pdfPage, StorageFile storageItem, FolderListingSettings folderListingSettings, ThumbnailManager thumbnailManager)
        {
            _pdfPage = pdfPage;
            Name = (_pdfPage.Index + 1).ToString();
            DateCreated = storageItem.DateCreated.DateTime;
            StorageItem = storageItem;
            _folderListingSettings = folderListingSettings;
            _thumbnailManager = thumbnailManager;

            Path = PageNavigationConstants.MakeStorageItemIdWithPage(storageItem.Path, _pdfPage.Index.ToString());
        }

        public string Name { get; }

        public string Path { get; }
        public DateTime DateCreated { get; }
        public StorageFile StorageItem { get; }

        IStorageItem IImageSource.StorageItem => StorageItem;

        public async Task<IRandomAccessStream> GetThumbnailImageStreamAsync(CancellationToken ct)
        {
            if (_folderListingSettings.IsArchiveEntryGenerateThumbnailEnabled)
            {
                return await _thumbnailManager.GetPdfPageThumbnailImageFileAsync(StorageItem, _pdfPage, ct);
            }
            else
            {
                return await _thumbnailManager.GetPdfPageThumbnailImageStreamAsync(StorageItem, _pdfPage, ct);
            }
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
            return _thumbnailManager.GetThumbnailOriginalSize(StorageItem, _pdfPage);
        }

        public bool Equals(IImageSource other)
        {
            if (other == null) { return false; }
            return this.Path == other.Path;
        }

        public override string ToString()
        {
            return Path;
        }
    }
}
