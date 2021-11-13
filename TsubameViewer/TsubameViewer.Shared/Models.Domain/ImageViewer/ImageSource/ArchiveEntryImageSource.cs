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
using Uno.Extensions;
using Uno.Threading;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace TsubameViewer.Models.Domain.ImageViewer.ImageSource
{
    public sealed class ArchiveEntryImageSource : IImageSource, IDisposable
    {
        private readonly IArchiveEntry _entry;
        private readonly RecyclableMemoryStreamManager _recyclableMemoryStreamManager;

        public ArchiveEntryImageSource(IArchiveEntry entry, IStorageItem storageItem, RecyclableMemoryStreamManager recyclableMemoryStreamManager)
        {
            _entry = entry;
            StorageItem = storageItem;
            _recyclableMemoryStreamManager = recyclableMemoryStreamManager;
            DateCreated = entry.CreatedTime ?? entry.LastModifiedTime ?? entry.ArchivedTime ?? DateTime.Now;
        }

        public IStorageItem StorageItem { get; }


        public string Name => _entry.Key;

        public DateTime DateCreated { get; }

        CancellationTokenSource _cts = new CancellationTokenSource();

        public async Task<IRandomAccessStream> GetImageStreamAsync(CancellationToken ct)
        {
            using var mylock = await _fastAsyncLock.LockAsync(ct);

            var memoryStream = _recyclableMemoryStreamManager.GetStream();
            using (var entryStream = _entry.OpenEntryStream())
            {
                entryStream.CopyTo(memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin);

                ct.ThrowIfCancellationRequested();
            }

            return memoryStream.AsRandomAccessStream();
        }

        public void CancelLoading()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        public void Dispose()
        {
            ((IDisposable)_cts).Dispose();
        }

        static FastAsyncLock _fastAsyncLock = new FastAsyncLock();

        public async Task<IRandomAccessStream> GetThumbnailImageStreamAsync(CancellationToken ct)
        {
            using var mylock = await _fastAsyncLock.LockAsync(ct);

            var memoryStream = _recyclableMemoryStreamManager.GetStream();
            using (var entryStream = _entry.OpenEntryStream())
            {
                entryStream.CopyTo(memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin);

                ct.ThrowIfCancellationRequested();
            }

            return memoryStream.AsRandomAccessStream();
        }
    }
}
