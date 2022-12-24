using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Contracts.Services;
using TsubameViewer.Core.Models.ImageViewer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace TsubameViewer.Core.Models.Albam;

public sealed class AlbamItemImageSource : IImageSource
{
    private readonly AlbamItemEntry _albamItem;
    public IImageSource InnerImageSource { get; }

    public AlbamItemImageSource(AlbamItemEntry albamItem, IImageSource imageSource)
    {
        _albamItem = albamItem;
        InnerImageSource = imageSource;
        if (InnerImageSource == null)
        {
            Name = albamItem.Name;
        }
        else if (InnerImageSource.StorageItem is StorageFile file)
        {
            if (file.FileType == SupportedFileTypesHelper.PdfFileType)
            {
                Name = $"{file.Name}#{imageSource.Name}";
            }
            else if (file.IsSupportedMangaFile())
            {
                Name = imageSource.Name;
            }
            else
            {
                Name = imageSource.Name;
            }
        }
        else if (InnerImageSource.StorageItem is StorageFolder folder)
        {
            Name = folder.Name;
        }
    }



    public Guid AlbamId => _albamItem.AlbamId;

    public IStorageItem StorageItem => InnerImageSource?.StorageItem;

    public string Name { get; }

    public string Path => _albamItem.Path;

    public DateTime DateCreated => _albamItem.AddedAt.LocalDateTime;

    public async Task<IRandomAccessStream> GetImageStreamAsync(CancellationToken ct = default)
    {
        return await InnerImageSource?.GetImageStreamAsync(ct);
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
