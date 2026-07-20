using CommunityToolkit.Mvvm.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Models.Navigation;
using Windows.Storage;
using Windows.Storage.Streams;

namespace TsubameViewer.Core.Models.ImageViewer.ImageSource;

public sealed class PdfPageImageSource : IImageSource
{
    private readonly ImageViewerSettings _imageViewerSettings;

    public PdfPageImageSource(
        int pageIndex,
        SizeF size,
        StorageFile storageItem, 
        ImageViewerSettings imageViewerSettings
        )
    {
        Name = (pageIndex + 1).ToString();
        DateCreated = storageItem.DateCreated.DateTime;
        PageIndex = pageIndex;
        Size = size;
        StorageItem = storageItem;
        _imageViewerSettings = imageViewerSettings;        

        Path = PageNavigationConstants.MakeStorageItemIdWithPage(storageItem.Path, (pageIndex + 1).ToString());
    }

    public string Name { get; }

    public string Path { get; }
    public DateTime DateCreated { get; }
    public int PageIndex { get; }
    public SizeF Size { get; }
    public StorageFile StorageItem { get; }

    public SizeF? PreCulcuratedSize => Size;

    IStorageItem IImageSource.StorageItem => StorageItem;

    public async ValueTask<SizeF?> TryGetSizedImageStreamAsync(int requestedSize, Stream imageStream, CancellationToken ct = default)
    {
        using (var pdfStream = await StorageItem.OpenStreamForReadAsync())
        {
            // Note: Jpegだとリリースビルド時のタブレット端末でクラッシュする
            PDFtoImage.Conversion.SavePng(imageStream, pdfStream, page: PageIndex, 
                options: new PDFtoImage.RenderOptions(Dpi: 96, Width: requestedSize, WithAspectRatio: true));

            ct.ThrowIfCancellationRequested();

            await imageStream.FlushAsync();
            imageStream.Seek(0, SeekOrigin.Begin);

            var ratio = requestedSize / Size.Width;

            ct.ThrowIfCancellationRequested();
            return new SizeF(requestedSize, Size.Height * ratio);
        }
    }

    public async ValueTask<Stream> GetImageStreamAsync(CancellationToken ct)
    {
        var memoryStream = new MemoryStream();
        await Task.Run(async () => 
        {
            using (var pdfStream = await StorageItem.OpenStreamForReadAsync())
            {
                // Note: Jpegだとリリースビルド時のタブレット端末でクラッシュする
                PDFtoImage.Conversion.SavePng(memoryStream, pdfStream, page: PageIndex);
            }
        }, ct);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return memoryStream;
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
