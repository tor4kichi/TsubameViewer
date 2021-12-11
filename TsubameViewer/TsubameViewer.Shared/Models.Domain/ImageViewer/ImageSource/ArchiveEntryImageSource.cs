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
        private readonly ThumbnailManager _thumbnailManager;

        public ArchiveEntryImageSource(IArchiveEntry entry, ArchiveDirectoryToken archiveDirectoryToken, ArchiveImageCollection archiveImageCollection, ThumbnailManager thumbnailManager)
        {
            _entry = entry;
            _archiveDirectoryToken = archiveDirectoryToken;
            _archiveImageCollection = archiveImageCollection;
            StorageItem = _archiveImageCollection.File;
            _thumbnailManager = thumbnailManager;
            DateCreated = entry.CreatedTime ?? entry.LastModifiedTime ?? entry.ArchivedTime ?? DateTime.Now;
        }

        public StorageFile StorageItem { get; }

        IStorageItem IImageSource.StorageItem => StorageItem;


        // Note: NameをImageViewerで表示時のページ名として扱っている
        // フォルダ名をスキップしてしまうとアーカイブ内に別フォルダに同名ファイルがある場合に
        // 常に最初のフォルダの同名ファイルが選ばれてしまう問題が起きる
        private string _name;
        //public string Name => _name ??= System.IO.Path.GetFileName(_entry.Key);
        public string Name => _name ??= _entry.Key;

        public string Path => _entry.Key;

        public DateTime DateCreated { get; }

        public async Task<IRandomAccessStream> GetImageStreamAsync(CancellationToken ct)
        {
            using var mylock = await ArchiveEntryAccessLock.LockAsync(ct);

            var memoryStream = new InMemoryRandomAccessStream();
            using (var entryStream = _entry.OpenEntryStream())
            {
                // Note: コメントアウトした書き方だと稀にコピーできないケースが発生する
                // entryStream.CopyTo(memoryStream.AsStream());
                await RandomAccessStream.CopyAsync(entryStream.AsInputStream(), memoryStream);
                memoryStream.Seek(0);

                ct.ThrowIfCancellationRequested();
            }

            return memoryStream;
        }


        internal static readonly Models.Infrastructure.AsyncLock ArchiveEntryAccessLock = new ();

        public async Task<IRandomAccessStream> GetThumbnailImageStreamAsync(CancellationToken ct)
        {
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
