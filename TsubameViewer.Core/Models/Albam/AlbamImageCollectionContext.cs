using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.Core.Contracts.Services;
using Windows.Storage;

namespace TsubameViewer.Core.Models.Albam;


public sealed class AlbamImageCollectionContext : IImageCollectionContext, IDisposable
{
    private readonly AlbamEntry _albam;
    private readonly AlbamRepository _albamRepository;
    private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
    private readonly ImageCollectionManager _imageCollectionManager;

    private readonly FolderListingSettings _folderListingSettings;
    private readonly IThumbnailImageService _thumbnailManager;
    private readonly IMessenger _messenger;

    public AlbamImageCollectionContext(
        AlbamEntry albam, 
        AlbamRepository albamRepository,
        SourceStorageItemsRepository sourceStorageItemsRepository,
        ImageCollectionManager imageCollectionManager,

        FolderListingSettings folderListingSettings,
        IThumbnailImageService thumbnailManager,

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


    public void Dispose()
    {
        foreach (var context in _archiveImageCollectionContextCache.Values)
        {
            (context as IDisposable)?.Dispose();
        }
    }


    private readonly Dictionary<Guid, IImageSource> _imagesCache = new();
    private readonly Dictionary<string, IImageCollectionContext> _archiveImageCollectionContextCache = new();

    private async Task<IImageSource> GetAlbamItemImageSourceAsync(AlbamItemEntry entry, CancellationToken ct)
    {
        if (_imagesCache.TryGetValue(entry._id, out var image)) { return image; }

        var (itemPath, pageName) = PageNavigationConstants.ParseStorageItemId(entry.Path);

        var storageItem = await _sourceStorageItemsRepository.TryGetStorageItemFromPath(itemPath);
        ct.ThrowIfCancellationRequested();

        IImageSource imageSource = null;
        if (storageItem is StorageFile file)
        {
            if (SupportedFileTypesHelper.IsSupportedImageFile(file))
            {
                imageSource = new StorageItemImageSource(file, _folderListingSettings);
            }
            else if (SupportedFileTypesHelper.IsSupportedMangaFile(file))
            {
                if (string.IsNullOrEmpty(pageName) is false)
                {
                    if (_archiveImageCollectionContextCache.TryGetValue(file.Path, out IImageCollectionContext collection) is false)
                    {
                        collection = await _imageCollectionManager.GetArchiveImageCollectionContextAsync(file, archiveDirectoryPath: null, ct);
                        _archiveImageCollectionContextCache.Add(file.Path, collection);
                    }

                    var index = await collection.GetIndexFromKeyAsync(pageName, FileSortType.None, ct);
                    imageSource = await collection.GetImageFileAtAsync(index, FileSortType.None, ct);
                }
                else
                {
                    imageSource = new StorageItemImageSource(file, _folderListingSettings);
                }
            }
            else if (SupportedFileTypesHelper.IsSupportedEBookFile(file))
            {
                imageSource = new StorageItemImageSource(file, _folderListingSettings);
            }
        }
        else if (storageItem is StorageFolder folder)
        {
            imageSource = new StorageItemImageSource(folder, _folderListingSettings);
        }
        
        var albamImage = new AlbamItemImageSource(entry, imageSource);
        if (imageSource != null)
        {
            _imagesCache.Add(entry._id, albamImage);
        }

        return albamImage;
    }        


    public string Name => _albam.Name;

    public bool IsSupportedFolderContentsChanged => true;


    public ValueTask<bool> IsExistFolderOrArchiveFileAsync(CancellationToken ct)
    {
        return new(_albamRepository.IsExistAlbamItem(_albam._id, AlbamItemType.FolderOrArchive));
    }

    public ValueTask<bool> IsExistImageFileAsync(CancellationToken ct)
    {
        return new(_albamRepository.IsExistAlbamItem(_albam._id, AlbamItemType.Image));
    }


    public IObservable<Unit> CreateFolderAndArchiveFileChangedObserver()
    {
        return Observable.Merge(
            _messenger.CreateObservable<AlbamItemAddedMessage, AlbamItemChangedMessageValue>(),
            _messenger.CreateObservable<AlbamItemRemovedMessage, AlbamItemChangedMessageValue>()
            )
            .Where(x => x.ItemType == AlbamItemType.FolderOrArchive)
            .ToUnit()
            ;
    }

    public IObservable<Unit> CreateImageFileChangedObserver()
    {
        return Observable.Merge(
            _messenger.CreateObservable<AlbamItemAddedMessage, AlbamItemChangedMessageValue>(),
            _messenger.CreateObservable<AlbamItemRemovedMessage, AlbamItemChangedMessageValue>()
            )
            .Where(x => x.ItemType == AlbamItemType.Image)
            .ToUnit()
            ;
    }

    public async IAsyncEnumerable<IImageSource> GetAllImageFilesAsync([EnumeratorCancellation] CancellationToken ct)
    {
        var items = _albamRepository.GetAlbamImageItems(_albam._id);
        foreach (var item in items.ToList())
        {
            yield return await GetAlbamItemImageSourceAsync(item, ct);
        }
    }

    public async IAsyncEnumerable<IImageSource> GetFolderOrArchiveFilesAsync([EnumeratorCancellation] CancellationToken ct)
    {
        var items = _albamRepository.GetAlbamFolderOrArchiveItems(_albam._id).ToArray();
        foreach (var item in items)
        {
            yield return await GetAlbamItemImageSourceAsync(item, ct);
        }
    }

    public async ValueTask<IImageSource> GetImageFileAtAsync(int index, FileSortType sort, CancellationToken ct)
    {
        var albamItem = _albamRepository.GetAlbamImageItems(_albam._id, sort, index, 1).FirstOrDefault();
        if (albamItem == null)
        {
            throw new InvalidOperationException($"not found albam item from index [{index}] in {_albam.Name}");
        }

        return await GetAlbamItemImageSourceAsync(albamItem, ct);
    }

    public ValueTask<int> GetImageFileCountAsync(CancellationToken ct)
    {
        return new (_albamRepository.GetAlbamItemsCount(_albam._id, AlbamItemType.Image));
    }

    public IAsyncEnumerable<IImageSource> GetImageFilesAsync(CancellationToken ct)
    {
        return GetAllImageFilesAsync(ct);
    }

    public ValueTask<int> GetIndexFromKeyAsync(string key, FileSortType sort, CancellationToken ct)
    {
        var items = _albamRepository.GetAlbamImageItems(_albam._id, sort);
        int index = 0;
        foreach (var item in items)
        {
            // item.Path.EndsWithは並べ替え後に必要
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
        return AsyncEnumerable.Empty<IImageSource>();
    }
}
