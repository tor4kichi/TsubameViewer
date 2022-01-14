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

        public Task<IRandomAccessStream> GetThumbnailImageStreamAsync(CancellationToken ct = default)
        {
            return null;
        }

        public ThumbnailManager.ThumbnailSize? GetThumbnailSize()
        {
            return null;
        }
    }
}
