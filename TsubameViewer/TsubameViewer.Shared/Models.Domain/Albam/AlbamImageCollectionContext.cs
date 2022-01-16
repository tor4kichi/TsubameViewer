using Microsoft.Toolkit.Mvvm.Messaging;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using TsubameViewer.Models.Domain.SourceFolders;
using Windows.Storage;

namespace TsubameViewer.Models.Domain.Albam
{
    
    public sealed class AlbamItemRemovedObservable : IObservable<(Guid AlbamId, string Path)>
    {
        private readonly IMessenger _messenger;

        public AlbamItemRemovedObservable(IMessenger messenger)
        {
            _messenger = messenger;
        }

        public IDisposable Subscribe(IObserver<(Guid AlbamId, string Path)> observer)
        {
            return new AlbamItemRemovedObserver(_messenger, observer);
        }


        public sealed class AlbamItemRemovedObserver : IDisposable
        {
            private readonly IMessenger _messenger;
            private readonly IObserver<(Guid AlbamId, string Path)> _observer;

            public AlbamItemRemovedObserver(IMessenger messenger, IObserver<(Guid AlbamId, string Path)> observer)
            {
                _messenger = messenger;
                _observer = observer;

                _messenger.Register<AlbamItemRemovedMessage>(this, (r, m) =>
                {
                    _observer.OnNext(m.Value);
                });
            }

            public void Dispose()
            {
                _messenger.Unregister<AlbamItemRemovedMessage>(this);
                _observer.OnCompleted();
                (_observer as IDisposable)?.Dispose();
            }
        }
    }


    public sealed class AlbamItemAddedObservable : IObservable<(Guid AlbamId, string Path)>
    {
        private readonly IMessenger _messenger;

        public AlbamItemAddedObservable(IMessenger messenger)
        {
            _messenger = messenger;
        }

        public IDisposable Subscribe(IObserver<(Guid AlbamId, string Path)> observer)
        {
            return new AlbamItemAddedObserver(_messenger, observer);
        }


        public sealed class AlbamItemAddedObserver : IDisposable
        {
            private readonly IMessenger _messenger;
            private readonly IObserver<(Guid AlbamId, string Path)> _observer;

            public AlbamItemAddedObserver(IMessenger messenger, IObserver<(Guid AlbamId, string Path)> observer)
            {
                _messenger = messenger;
                _observer = observer;

                _messenger.Register<AlbamItemAddedMessage>(this, (r, m) =>
                {
                    _observer.OnNext(m.Value);
                });
            }

            public void Dispose()
            {
                _messenger.Unregister<AlbamItemAddedMessage>(this);
                _observer.OnCompleted();
                (_observer as IDisposable)?.Dispose();
            }
        }
    }


    public sealed class AlbamImageCollectionContext : IImageCollectionContext
    {
        private readonly AlbamEntry _albam;
        private readonly AlbamRepository _albamRepository;
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly ImageCollectionManager _imageCollectionManager;

        private readonly FolderListingSettings _folderListingSettings;
        private readonly ThumbnailManager _thumbnailManager;
        private readonly IMessenger _messenger;

        public AlbamImageCollectionContext(
            AlbamEntry albam, 
            AlbamRepository albamRepository,
            SourceStorageItemsRepository sourceStorageItemsRepository,
            ImageCollectionManager imageCollectionManager,

            FolderListingSettings folderListingSettings,
            ThumbnailManager thumbnailManager,

            IMessenger messenger
            )
        {
            _albam = albam;
            _albamRepository = albamRepository;
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
            _imageCollectionManager = imageCollectionManager;
            _folderListingSettings = folderListingSettings;
            _thumbnailManager = thumbnailManager;
            _messenger = messenger;
        }


        private async Task<IImageSource> GetAlbamItemImageSourceAsync(AlbamItemEntry entry, CancellationToken ct)
        {
            var (itemPath, pageName, _) = PageNavigationConstants.ParseStorageItemId(entry.Path);

            var storageItem = await _sourceStorageItemsRepository.GetStorageItemFromPath(itemPath);
            ct.ThrowIfCancellationRequested();

            IImageSource imageSource = null;
            if (storageItem is StorageFile file)
            {
                if (SupportedFileTypesHelper.IsSupportedImageFile(file))
                {
                    imageSource = new StorageItemImageSource(file, _folderListingSettings, _thumbnailManager);
                }
                else if (SupportedFileTypesHelper.IsSupportedMangaFile(file))
                {
                    var collection = await _imageCollectionManager.GetArchiveImageCollectionContextAsync(file, archiveDirectoryPath: null, ct);
                    var index = await collection.GetIndexFromKeyAsync(pageName, FileSortType.None, ct);
                    imageSource = await collection.GetImageFileAtAsync(index, FileSortType.None, ct);
                }
            }
            
            if (imageSource == null)
            {
                throw new NotSupportedException(entry.Path);
            }

            return new AlbamItemImageSource(entry, imageSource);
        }        


        public string Name => _albam.Name;

        public bool IsSupportedFolderContentsChanged => true;


        public ValueTask<bool> IsExistFolderOrArchiveFileAsync(CancellationToken ct)
        {
            return new(false);
        }

        public ValueTask<bool> IsExistImageFileAsync(CancellationToken ct)
        {
            return new(_albamRepository.IsExistAlbamItem(_albam._id));
        }


        public IObservable<Unit> CreateFolderAndArchiveFileChangedObserver()
        {
            return Observable.Empty<Unit>();
        }

        public IObservable<Unit> CreateImageFileChangedObserver()
        {
            return Observable.Merge(
                new AlbamItemRemovedObservable(_messenger).ToUnit(),
                new AlbamItemAddedObservable(_messenger).ToUnit()
                );
        }

        public async IAsyncEnumerable<IImageSource> GetAllImageFilesAsync([EnumeratorCancellation] CancellationToken ct)
        {
            var items = _albamRepository.GetAlbamItems(_albam._id);
            foreach (var item in items)
            {
                yield return await GetAlbamItemImageSourceAsync(item, ct);
            }
        }

        public IAsyncEnumerable<IImageSource> GetFolderOrArchiveFilesAsync(CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public async ValueTask<IImageSource> GetImageFileAtAsync(int index, FileSortType sort, CancellationToken ct)
        {
            var albamItem = _albamRepository.GetAlbamItems(_albam._id, index, 1).FirstOrDefault();
            if (albamItem == null)
            {
                throw new InvalidOperationException($"not found albam item from index [{index}] in {_albam.Name}");
            }

            return await GetAlbamItemImageSourceAsync(albamItem, ct);
        }

        public ValueTask<int> GetImageFileCountAsync(CancellationToken ct)
        {
            return new (_albamRepository.GetAlbamItemsCount(_albam._id));
        }

        public IAsyncEnumerable<IImageSource> GetImageFilesAsync(CancellationToken ct)
        {
            return GetAllImageFilesAsync(ct);
        }

        public ValueTask<int> GetIndexFromKeyAsync(string key, FileSortType sort, CancellationToken ct)
        {
            var items = _albamRepository.GetAlbamItems(_albam._id);
            int index = 0;
            foreach (var item in items)
            {
                if (item.Path == key || item.Path.EndsWith(key))
                {
                    return new (index);
                }
                index++;
            }

            throw new InvalidOperationException();
        }

        public IAsyncEnumerable<IImageSource> GetLeafFoldersAsync(CancellationToken ct)
        {
            throw new NotSupportedException();
        }
    }
}
