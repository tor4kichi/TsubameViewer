using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace TsubameViewer.Core.Models.Albam;

public sealed class AlbamImageSource : IImageSource
{
    public AlbamEntry AlbamEntry { get; }
    private readonly AlbamImageCollectionContext _albamImageCollectionContext;

    public AlbamImageSource(AlbamEntry albamEntry, AlbamImageCollectionContext albamImageCollectionContext)
    {
        AlbamEntry = albamEntry;
        _albamImageCollectionContext = albamImageCollectionContext;
    }

    public Guid AlbamId => AlbamEntry._id;

    public IStorageItem StorageItem => null;

    public string Name => AlbamEntry.Name;

    public string Path => AlbamEntry.Name;

    public DateTime DateCreated => AlbamEntry.CreatedAt.LocalDateTime;        

    public Task<IRandomAccessStream> GetImageStreamAsync(CancellationToken ct = default)
    {
        return null;
    }

    private IImageSource _sampleImageSource;
    public async Task<IRandomAccessStream> GetThumbnailImageStreamAsync(CancellationToken ct = default)
    {
        var sampleImageSource = await GetSampleImageSourceAsync(ct);
        if (sampleImageSource is not null)
        {
            return await sampleImageSource.GetThumbnailImageStreamAsync(ct);
        }
        else
        {
            return null;
        }
    }

    private async ValueTask<IImageSource> GetSampleImageSourceAsync(CancellationToken ct)
    {
        if (_sampleImageSource is null && await _albamImageCollectionContext.IsExistImageFileAsync(ct))
        {
            _sampleImageSource = await _albamImageCollectionContext.GetImageFileAtAsync(0, FileSortType.None, ct);
        }
        else if (_sampleImageSource is null && await _albamImageCollectionContext.IsExistFolderOrArchiveFileAsync(ct))
        {
            _sampleImageSource = await _albamImageCollectionContext.GetFolderOrArchiveFilesAsync(ct).FirstAsync(ct);
        }

        return _sampleImageSource;
    }

    public ThumbnailManager.ThumbnailSize? GetThumbnailSize()
    {
        return null;
    }

    public bool Equals(IImageSource other)
    {
        if (other == null) { return false; }
        return this.AlbamId == (other as AlbamImageSource)?.AlbamId;
    }

    public override string ToString()
    {
        return Path;
    }

    public ValueTask<bool> IsExistImageFileAsync()
    {
        return _albamImageCollectionContext.IsExistImageFileAsync(CancellationToken.None);
    }

    public ValueTask<bool> IsExistFolderOrArchiveFileAsync()
    {
        return _albamImageCollectionContext.IsExistFolderOrArchiveFileAsync(CancellationToken.None);
    }
}
