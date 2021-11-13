using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;

namespace TsubameViewer.Models.Domain.ImageViewer.ImageSource
{
    public sealed class ArchiveDirectoryImageSource : IImageSource
    {
        public IStorageItem StorageItem => throw new NotImplementedException();

        public string Name => throw new NotImplementedException();

        public DateTime DateCreated => throw new NotImplementedException();

        public Task<IRandomAccessStream> GetImageStreamAsync(CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<IRandomAccessStream> GetThumbnailImageStreamAsync(CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }
}
