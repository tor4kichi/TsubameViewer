using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Contracts.Services;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace TsubameViewer.Core.Models.ImageViewer.ImageSource;

public sealed class PdfPageImageSource : IImageSource, IDisposable
{
    private readonly PdfPage _pdfPage;
    private readonly ImageViewerSettings _imageViewerSettings;

    public PdfPageImageSource(
        PdfPage pdfPage, 
        StorageFile storageItem, 
        ImageViewerSettings imageViewerSettings
        )
    {
        _pdfPage = pdfPage;
        Name = (_pdfPage.Index + 1).ToString();
        DateCreated = storageItem.DateCreated.DateTime;
        StorageItem = storageItem;
        _imageViewerSettings = imageViewerSettings;        

        Path = PageNavigationConstants.MakeStorageItemIdWithPage(storageItem.Path, _pdfPage.Index.ToString());
    }

    public string Name { get; }

    public string Path { get; }
    public DateTime DateCreated { get; }
    public StorageFile StorageItem { get; }

    IStorageItem IImageSource.StorageItem => StorageItem;

    public async Task<IRandomAccessStream> GetImageStreamAsync(CancellationToken ct)
    {
        var memoryStream = new InMemoryRandomAccessStream();
        {
            if (_pdfPage.Size.Height < _imageViewerSettings.PdfImageThresholdHeight)
            {
                await _pdfPage.RenderToStreamAsync(memoryStream, new PdfPageRenderOptions() { DestinationHeight = _imageViewerSettings.PdfImageAlternateHeight }).AsTask(ct);
            }
            else if (_pdfPage.Size.Width < _imageViewerSettings.PdfImageThresholdWidth)
            {
                await _pdfPage.RenderToStreamAsync(memoryStream, new PdfPageRenderOptions() {  DestinationWidth = _imageViewerSettings.PdfImageThresholdWidth }).AsTask(ct);
            }
            else
            {
                await _pdfPage.RenderToStreamAsync(memoryStream).AsTask(ct);
            }
            

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
