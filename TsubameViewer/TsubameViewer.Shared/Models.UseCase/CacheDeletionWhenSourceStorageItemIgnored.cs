using Prism.Events;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Text;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.ReadingFeature;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.RestoreNavigation;
using TsubameViewer.Models.Domain.Search;
using TsubameViewer.Models.Domain.SourceFolders;
using TsubameViewer.Presentation.Services.UWP;
using System.Threading.Tasks;
using Microsoft.Toolkit.Mvvm.Messaging;
using System.Diagnostics;
using Windows.Storage;
using System.Linq;

namespace TsubameViewer.Models.UseCase
{
    public interface IRestorable
    {
        void Restore();
    }

    public sealed class CacheDeletionWhenSourceStorageItemIgnored :
        IRecipient<SourceStorageItemIgnoringRequestMessage>,
        IRestorable
    {
        private readonly IMessenger _messenger;
        private readonly SourceStorageItemsRepository _storageItemsRepository;
        private readonly IgnoreStorageItemRepository _ignoreStorageItemRepository;
        private readonly RecentlyAccessManager _recentlyAccessManager;
        private readonly BookmarkManager _bookmarkManager;
        private readonly StorageItemSearchManager _storageItemSearchManager;
        private readonly FolderContainerTypeManager _folderContainerTypeManager;
        private readonly ThumbnailManager _thumbnailManager;
        private readonly SecondaryTileManager _secondaryTileManager;
        private readonly FolderLastIntractItemManager _folderLastIntractItemManager;
        private readonly DisplaySettingsByPathRepository _displaySettingsByPathRepository;
        
        public CacheDeletionWhenSourceStorageItemIgnored(
            IMessenger messenger,
            SourceStorageItemsRepository storageItemsRepository,
            IgnoreStorageItemRepository ignoreStorageItemRepository,
            RecentlyAccessManager recentlyAccessManager,
            BookmarkManager bookmarkManager,
            StorageItemSearchManager storageItemSearchManager,
            FolderContainerTypeManager folderContainerTypeManager,
            ThumbnailManager thumbnailManager,
            SecondaryTileManager secondaryTileManager,
            FolderLastIntractItemManager folderLastIntractItemManager,
            DisplaySettingsByPathRepository displaySettingsByPathRepository
            )
        {
            _messenger = messenger;
            _storageItemsRepository = storageItemsRepository;
            _ignoreStorageItemRepository = ignoreStorageItemRepository;
            _recentlyAccessManager = recentlyAccessManager;
            _bookmarkManager = bookmarkManager;
            _storageItemSearchManager = storageItemSearchManager;
            _folderContainerTypeManager = folderContainerTypeManager;
            _thumbnailManager = thumbnailManager;
            _secondaryTileManager = secondaryTileManager;
            _folderLastIntractItemManager = folderLastIntractItemManager;
            _displaySettingsByPathRepository = displaySettingsByPathRepository;

        }


        void IRestorable.Restore()
        {
            Debug.WriteLine($"Restored CacheDeletionWhenSourceStorageItemIgnored.");
            TickNext();
        }

        void IRecipient<SourceStorageItemIgnoringRequestMessage>.Receive(SourceStorageItemIgnoringRequestMessage message)
        {
            Debug.WriteLine($"recive SourceStorageItemIgnoringRequestMessage.");
            _ignoreStorageItemRepository.CreateItem(new IgnoreStorageItemEntry() { Path = message.Value });
            Debug.WriteLine($"add ignored StorageItem to Db : {message.Value}");
            TickNext();
        }


        bool _nowProgress;
        object _lock = new object();

        async void TickNext()
        {
            lock (_lock)
            {
                if (_nowProgress) 
                {
                    Debug.WriteLine($"Skip cache delete process.");
                    return; 
                }

                Debug.WriteLine($"Start cache delete process.");
                _nowProgress = true;
            }

            try
            {
                while (_ignoreStorageItemRepository.TryPeek(out IgnoreStorageItemEntry entry))
                {
                    Debug.WriteLine($"start cache deletion: {entry.Path}");
                    await DeleteCacheWithDescendantsAsync(entry.Path);
                    Debug.WriteLine($"done cache deletion: {entry.Path}");
                    _ignoreStorageItemRepository.Delete(entry);
                    Debug.WriteLine($"remove ignored StorageItem from Db : {entry.Path}");
                }
            }
            finally
            {
                lock (_lock)
                {
                    _nowProgress = false;
                }
            }

            Debug.WriteLine($"End cache delete process.");
        }

        async Task DeleteCacheWithDescendantsAsync(string path)
        {
            var (_, item) = await _storageItemsRepository.GetSourceStorageItem(path);
            if (item is StorageFolder folder)
            {
                await foreach(var deletePath in GetAllDeletionPathsAsync(folder))
                {
                    await DeleteCacheAsync(deletePath);
                }
            }
            else
            {
                await DeleteCacheAsync(path);
            }
        }

        async Task DeleteCacheAsync(string path)
        {
            var tasks = new[] {
                _thumbnailManager.DeleteThumbnailFromPathAsync(path),
                _secondaryTileManager.RemoveSecondaryTile(path)
            };

            _recentlyAccessManager.Delete(path);
            _bookmarkManager.RemoveBookmark(path);
            _storageItemSearchManager.Remove(path);
            _folderContainerTypeManager.Delete(path);
            _folderLastIntractItemManager.Remove(path);
            _displaySettingsByPathRepository.DeleteUnderPath(path);

            await Task.WhenAll(tasks);
        }


        async IAsyncEnumerable<string> GetAllDeletionPathsAsync(StorageFolder folder)
        {
            var descendantPaths = await _storageItemsRepository.GetDescendantItemPathsAsync(folder.Path).ToListAsync();

            bool IsSkipPath(string path)
            {
                return descendantPaths.Any(x => path.StartsWith(x));
            }

            var query = folder.CreateItemQueryWithOptions(new Windows.Storage.Search.QueryOptions(Windows.Storage.Search.CommonFileQuery.DefaultQuery, SupportedFileTypesHelper.GetAllSupportedFileExtensions()) { FolderDepth = Windows.Storage.Search.FolderDepth.Deep });
            var count = await query.GetItemCountAsync();
            uint processed = 0;
            List<string> paths = new List<string>();
            while (count < processed)
            {
                var items = await query.GetItemsAsync(processed, 100);
                processed += (uint)items.Count;

                foreach (var folderItem in items)
                {
                    if (IsSkipPath(folderItem.Path) is false)
                    {
                        yield return folderItem.Path;
                    }
                }
            }
        }

    }
}
