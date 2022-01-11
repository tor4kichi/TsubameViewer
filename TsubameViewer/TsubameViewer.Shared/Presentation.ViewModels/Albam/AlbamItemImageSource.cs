using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain.Albam;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace TsubameViewer.Presentation.ViewModels.Albam
{
    public sealed class AlbamItemImageSource : IImageSource
    {
        private readonly AlbamItemEntry _albamItem;

        // 画像ソースの遅延解決
        public AlbamItemImageSource(AlbamItemEntry albamItem)
        {
            _albamItem = albamItem;
        }

        public IStorageItem StorageItem => null;

        public string Name => _albamItem.Path;

        public string Path => _albamItem.Path;

        public DateTime DateCreated => _albamItem.AddedAt.LocalDateTime;

        public Task<IRandomAccessStream> GetImageStreamAsync(CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<IRandomAccessStream> GetThumbnailImageStreamAsync(CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public ThumbnailManager.ThumbnailSize? GetThumbnailSize()
        {
            throw new NotImplementedException();
        }
    }
}
