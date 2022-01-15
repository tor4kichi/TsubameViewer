using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace TsubameViewer.Models.Domain.Albam
{
    public sealed class AlbamImageSource : IImageSource
    {
        private readonly AlbamEntry _albamEntry;
        private readonly AlbamImageCollectionContext _albamImageCollectionContext;

        public AlbamImageSource(AlbamEntry albamEntry, AlbamImageCollectionContext albamImageCollectionContext)
        {
            _albamEntry = albamEntry;
            _albamImageCollectionContext = albamImageCollectionContext;
        }

        public Guid AlbamId => _albamEntry._id;

        public IStorageItem StorageItem => null;

        public string Name => _albamEntry.Name;

        public string Path => _albamEntry.Name;

        public DateTime DateCreated => _albamEntry.CreatedAt.LocalDateTime;

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

            return _sampleImageSource;
        }

        public ThumbnailManager.ThumbnailSize? GetThumbnailSize()
        {
            return null;
        }
    }
}
