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
        private readonly ThumbnailManager _thumbnailManager;

        public IImageSource InnerImageSource { get; }

        // 画像ソースの遅延解決
        public AlbamItemImageSource(AlbamItemEntry albamItem, IImageSource imageSource, ThumbnailManager thumbnailManager)
        {
            _albamItem = albamItem;
            InnerImageSource = imageSource;
            _thumbnailManager = thumbnailManager;
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

        public async Task<IRandomAccessStream> GetThumbnailImageStreamAsync(CancellationToken ct = default)
        {
            if (InnerImageSource != null)
            {
                return await InnerImageSource.GetThumbnailImageStreamAsync(ct);
            }
            else
            {
                return await _thumbnailManager.GetThumbnailImageFromPathAsync(_albamItem.Path, ct);
            }            
        }

        public ThumbnailManager.ThumbnailSize? GetThumbnailSize()
        {
            return InnerImageSource?.GetThumbnailSize() ?? _thumbnailManager.GetThumbnailOriginalSize(_albamItem.Path);
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
