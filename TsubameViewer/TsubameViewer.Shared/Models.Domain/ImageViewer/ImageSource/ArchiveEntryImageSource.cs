using Microsoft.IO;
using Prism.Mvvm;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain.FolderItemListing;
using Uno.Extensions;
using Uno.Threading;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace TsubameViewer.Models.Domain.ImageViewer.ImageSource
{
    public sealed class ArchiveEntryImageSource : IImageSource
    {
        private readonly IArchiveEntry _entry;
        private readonly ArchiveDirectoryToken _archiveDirectoryToken;
        private readonly ArchiveImageCollection _archiveImageCollection;
        private readonly RecyclableMemoryStreamManager _recyclableMemoryStreamManager;
        private readonly ThumbnailManager _thumbnailManager;

        public ArchiveEntryImageSource(IArchiveEntry entry, ArchiveDirectoryToken archiveDirectoryToken, ArchiveImageCollection archiveImageCollection, RecyclableMemoryStreamManager recyclableMemoryStreamManager, ThumbnailManager thumbnailManager)
        {
            _entry = entry;
            _archiveDirectoryToken = archiveDirectoryToken;
            _archiveImageCollection = archiveImageCollection;
            StorageItem = _archiveImageCollection.File;
            _recyclableMemoryStreamManager = recyclableMemoryStreamManager;
            _thumbnailManager = thumbnailManager;
            DateCreated = entry.CreatedTime ?? entry.LastModifiedTime ?? entry.ArchivedTime ?? DateTime.Now;
        }

        public StorageFile StorageItem { get; }

        IStorageItem IImageSource.StorageItem => StorageItem;

        private string _name;
        public string Name => _name ??= System.IO.Path.GetFileName(_entry.Key);

        public string Path => _entry.Key;

        public DateTime DateCreated { get; }

        public async Task<IRandomAccessStream> GetImageStreamAsync(CancellationToken ct)
        {
            using var mylock = await ArchiveEntryAccessLock.LockAsync(ct);

            var memoryStream = _recyclableMemoryStreamManager.GetStream();
            using (var entryStream = _entry.OpenEntryStream())
            {
                entryStream.CopyTo(memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin);

                ct.ThrowIfCancellationRequested();
            }

            return memoryStream.AsRandomAccessStream();
        }


        internal static FastAsyncLock ArchiveEntryAccessLock = new FastAsyncLock();

        public async Task<IRandomAccessStream> GetThumbnailImageStreamAsync(CancellationToken ct)
        {
            using var mylock = await ArchiveEntryAccessLock.LockAsync(ct);

            var thumbnailFile = await _thumbnailManager.GetArchiveEntryThumbnailImageAsync(StorageItem, _entry, ct);
            var stream = await thumbnailFile.OpenStreamForReadAsync();
            return stream.AsRandomAccessStream();
        }


        public IArchiveEntry GetParentDirectoryEntry()
        {
            if (_archiveDirectoryToken.Key == null
                || !(_archiveDirectoryToken.Key.Contains(System.IO.Path.DirectorySeparatorChar) || _archiveDirectoryToken.Entry.Key.Contains(System.IO.Path.AltDirectorySeparatorChar))                
                )
            {
                return null;
            }

            return _archiveDirectoryToken.Entry;
        }

        public ThumbnailManager.ThumbnailSize? GetThumbnailSize()
        {
            return _thumbnailManager.GetThubmnailOriginalSize(_thumbnailManager.GetArchiveEntryPath(StorageItem, _entry));
        }
    }
}
