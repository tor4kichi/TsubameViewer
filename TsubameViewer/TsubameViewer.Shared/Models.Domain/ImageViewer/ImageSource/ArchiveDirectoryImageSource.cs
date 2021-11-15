using Microsoft.IO;
using Microsoft.Toolkit.Diagnostics;
using SharpCompress.Archives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain.FolderItemListing;
using Windows.Storage;
using Windows.Storage.Streams;

namespace TsubameViewer.Models.Domain.ImageViewer.ImageSource
{
    public sealed class ArchiveDirectoryImageSource : IImageSource
    {
        private readonly ArchiveImageCollection _imageCollection;
        private readonly ArchiveDirectoryToken _directoryToken;
        private readonly ThumbnailManager _thumbnailManager;

        public ArchiveDirectoryImageSource(ArchiveImageCollection archiveImageCollection, ArchiveDirectoryToken directoryToken, ThumbnailManager thumbnailManager)
        {
            Guard.IsNotNull(directoryToken.Key, nameof(directoryToken.Key));
            _imageCollection = archiveImageCollection;
            _directoryToken = directoryToken;
            _thumbnailManager = thumbnailManager;
            Name = _directoryToken.Key is not null ? new string(_directoryToken.Key.Reverse().TakeWhile(c => c != System.IO.Path.DirectorySeparatorChar).Reverse().ToArray()) : _imageCollection.Name;
        }

        public IArchiveEntry ArchiveEntry => _directoryToken?.Entry;

        public StorageFile StorageItem => _imageCollection.File;
        
        IStorageItem IImageSource.StorageItem => _imageCollection.File;

        public string Name { get; }

        public string Path => _directoryToken?.Key;

        public DateTime DateCreated => _imageCollection.File.DateCreated.DateTime;

        public async Task<IRandomAccessStream> GetImageStreamAsync(CancellationToken ct = default)
        {
            using var mylock = await ArchiveEntryImageSource.ArchiveEntryAccessLock.LockAsync(ct);

            var imageSource = GetNearestImageFromDirectory(_directoryToken);
            if (imageSource == null) { return null; }

            return await imageSource.GetImageStreamAsync(ct);
        }

        public async Task<IRandomAccessStream> GetThumbnailImageStreamAsync(CancellationToken ct = default)
        {
            using var mylock = await ArchiveEntryImageSource.ArchiveEntryAccessLock.LockAsync(ct);

            var file = await _thumbnailManager.GetArchiveEntryThumbnailImageAsync(StorageItem, ArchiveEntry, ct);
            if (file is null)
            {
                var imageSource = GetNearestImageFromDirectory(_directoryToken);
                if (imageSource == null) { return null; }

                var stream = await imageSource.GetThumbnailImageStreamAsync(ct);
                {
                    file = await _thumbnailManager.SetArchiveEntryThumbnailAsync(StorageItem, ArchiveEntry, stream, ct);
                }

                stream.Seek(0);
                return stream;
            }

            if (file == null) { return null; }

            var fileStream = await file.OpenStreamForReadAsync();
            return fileStream.AsRandomAccessStream();
        }

        private IImageSource GetNearestImageFromDirectory(ArchiveDirectoryToken firstToken)
        {
            Stack<ArchiveDirectoryToken> archiveDirectoryTokens = new Stack<ArchiveDirectoryToken>();
            archiveDirectoryTokens.Push(firstToken);
            IImageSource imageSource = null;
            while (archiveDirectoryTokens.TryPop(out var token))
            {
                imageSource = _imageCollection.GetThumbnailImageFromDirectory(token);
                if (imageSource != null) { break; }
                else
                {
                    foreach (var subDir in _imageCollection.GetSubDirectories(token).Reverse())
                    {
                        archiveDirectoryTokens.Push(subDir);
                    }
                }
            }

            return imageSource;
        }

        public bool IsContainsImage()
        {
            return _imageCollection.GetImagesFromDirectory(_directoryToken).Any();
        }

        public bool IsContainsSubDirectory()
        {
            return _imageCollection.GetSubDirectories(_directoryToken).Any();
        }

        public IArchiveEntry GetParentDirectoryEntry()
        {            
            var entry = _imageCollection.GetDirectoryTokenFromPath(System.IO.Path.GetDirectoryName(_directoryToken?.Key))?.Entry;
            if (entry == null
                || !(entry.Key.Contains(System.IO.Path.DirectorySeparatorChar) || entry.Key.Contains(System.IO.Path.AltDirectorySeparatorChar))
                )
            {
                return null;
            }
            else { return entry; }
        }
    }
}
