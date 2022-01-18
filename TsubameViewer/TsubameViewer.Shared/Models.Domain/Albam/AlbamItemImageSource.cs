﻿using System;
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
        private readonly IImageSource _imageSource;

        // 画像ソースの遅延解決
        public AlbamItemImageSource(AlbamItemEntry albamItem, IImageSource imageSource)
        {
            _albamItem = albamItem;
            _imageSource = imageSource;

            if (_imageSource.StorageItem is StorageFile file)
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

        public IStorageItem StorageItem => _imageSource.StorageItem;

        public string Name { get; }

        public string Path => _albamItem.Path;

        public DateTime DateCreated => _albamItem.AddedAt.LocalDateTime;

        public Task<IRandomAccessStream> GetImageStreamAsync(CancellationToken ct = default)
        {
            return _imageSource.GetImageStreamAsync(ct);
        }

        public Task<IRandomAccessStream> GetThumbnailImageStreamAsync(CancellationToken ct = default)
        {
            return _imageSource.GetThumbnailImageStreamAsync(ct);
        }

        public ThumbnailManager.ThumbnailSize? GetThumbnailSize()
        {
            return _imageSource.GetThumbnailSize();
        }
    }
}
