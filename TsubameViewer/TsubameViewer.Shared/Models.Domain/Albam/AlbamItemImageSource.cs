using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain.Albam;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace TsubameViewer.Models.Domain.Albam
{
    public sealed class AlbamItemImageSource : IImageSource
    {
        private readonly AlbamItemEntry _albamItem;
        public IImageSource InnerImageSource { get; }

        // 画像ソースの遅延解決
        public AlbamItemImageSource(AlbamItemEntry albamItem, IImageSource imageSource)
        {
            _albamItem = albamItem;
            InnerImageSource = imageSource;

            if (InnerImageSource.StorageItem is StorageFile file)
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
        }



        public Guid AlbamId => _albamItem.AlbamId;

        public IStorageItem StorageItem => InnerImageSource.StorageItem;

        public string Name { get; }

        public string Path => _albamItem.Path;

        public DateTime DateCreated => _albamItem.AddedAt.LocalDateTime;

        public Task<IRandomAccessStream> GetImageStreamAsync(CancellationToken ct = default)
        {
            return InnerImageSource.GetImageStreamAsync(ct);
        }

        public Task<IRandomAccessStream> GetThumbnailImageStreamAsync(CancellationToken ct = default)
        {
            return InnerImageSource.GetThumbnailImageStreamAsync(ct);
        }

        public ThumbnailManager.ThumbnailSize? GetThumbnailSize()
        {
            return InnerImageSource.GetThumbnailSize();
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
