using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TsubameViewer.Core.Contracts.Maintenance;
using TsubameViewer.Core.Contracts.Services;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.SourceFolders;
using Windows.Storage;

namespace TsubameViewer.Core.Models.Maintenance;

/// <summary>
/// ソース管理に変更が加えられて、新規に管理するストレージアイテムが増えた・減った際に
/// ローカルDBや画像サムネイルの破棄などを行う
/// 単にソース管理が消されたからと破棄処理をしてしまうと包含関係のフォルダ追加を許容できなくなるので
/// 包含関係のフォルダに関するキャッシュの削除をスキップするような動作が含まれる
/// </summary>
public sealed class CacheDeletionWhenSourceStorageItemIgnored :
    ILaunchTimeMaintenance,
    IRecipient<SourceStorageItemIgnoringRequestMessage>,
    IRecipient<SourceStorageItemsRepository.SourceStorageItemMovedOrRenameMessage>
{
    private readonly IMessenger _messenger;
    private readonly SourceStorageItemsRepository _storageItemsRepository;
    private readonly RecentlyAccessRepository _recentlyAccessRepository;
    private readonly LocalBookmarkRepository _bookmarkManager;
    private readonly FolderContainerTypeManager _folderContainerTypeManager;
    private readonly IThumbnailImageMaintenanceService _thumbnailImageMaintenanceService;
    private readonly ISecondaryTileManager _secondaryTileManager;
    private readonly LastIntractItemRepository _folderLastIntractItemManager;
    private readonly DisplaySettingsByPathRepository _displaySettingsByPathRepository;
    private readonly ArchiveFileInnerStructureCache _archiveFileInnerStructureCache;

    public CacheDeletionWhenSourceStorageItemIgnored(
        IMessenger messenger,
        SourceStorageItemsRepository storageItemsRepository,
        RecentlyAccessRepository recentlyAccessRepository,
        LocalBookmarkRepository bookmarkManager,
        FolderContainerTypeManager folderContainerTypeManager,
        IThumbnailImageMaintenanceService thumbnailImageMaintenanceService,
        ISecondaryTileManager secondaryTileManager,
        LastIntractItemRepository folderLastIntractItemManager,
        DisplaySettingsByPathRepository displaySettingsByPathRepository,
        ArchiveFileInnerStructureCache archiveFileInnerStructureCache
        )
    {
        _messenger = messenger;
        _storageItemsRepository = storageItemsRepository;
        _recentlyAccessRepository = recentlyAccessRepository;
        _bookmarkManager = bookmarkManager;
        _folderContainerTypeManager = folderContainerTypeManager;
        _thumbnailImageMaintenanceService = thumbnailImageMaintenanceService;
        _secondaryTileManager = secondaryTileManager;
        _folderLastIntractItemManager = folderLastIntractItemManager;
        _displaySettingsByPathRepository = displaySettingsByPathRepository;
        _archiveFileInnerStructureCache = archiveFileInnerStructureCache;
        _messenger.RegisterAll(this);
    }

    public async void Receive(SourceStorageItemsRepository.SourceStorageItemMovedOrRenameMessage message)
    {
        var oldPath = message.Value.OldPath;
        var newPath = message.Value.NewPath;

        Debug.WriteLine($"Start folder change process.");
        Debug.WriteLine($"OldPath = {oldPath}");
        Debug.WriteLine($"NewPath = {newPath}");

        try
        {
            var tasks = new[] {
                _thumbnailImageMaintenanceService.FolderChangedAsync(oldPath, newPath),
                _secondaryTileManager.RemoveSecondaryTile(newPath)
            };

            _displaySettingsByPathRepository.FolderChanged(oldPath, newPath);
            _bookmarkManager.FolderChanged(oldPath, newPath);

            _recentlyAccessRepository.Delete(oldPath);
            _folderContainerTypeManager.Delete(oldPath);
            _folderLastIntractItemManager.Remove(oldPath);
            _archiveFileInnerStructureCache.DeleteUnderPath(oldPath);

            await Task.WhenAll(tasks);

            Debug.WriteLine($"Done folder change process.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed folder change process.");
            Debug.WriteLine(ex.ToString());
#if DEBUG
            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }
#endif
        }
    }


    void ILaunchTimeMaintenance.Maintenance()
    {
        Debug.WriteLine($"Restored CacheDeletionWhenSourceStorageItemIgnored.");
        TickNext();
    }

    void IRecipient<SourceStorageItemIgnoringRequestMessage>.Receive(SourceStorageItemIgnoringRequestMessage message)
    {
        Debug.WriteLine($"recive SourceStorageItemIgnoringRequestMessage.");
        _storageItemsRepository.AddIgnoreToken(message.Value);
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
                Debug.WriteLine($"Skip cache delete process. (Now Progress)");
                return; 
            }

            if (_storageItemsRepository.HasIgnorePath() is false) 
            {
                Debug.WriteLine($"Skip cache delete process. (No items)");
                return; 
            }

            Debug.WriteLine($"Start cache delete process.");
            _nowProgress = true;
        }

        try
        {
            while (_storageItemsRepository.TryPeek(out string path))
            {
                Debug.WriteLine($"Start cache deletion: {path}");
                if (Path.IsPathRooted(path))
                {
                    await DeleteCacheWithDescendantsAsync(path);
                    Debug.WriteLine($"Done cache deletion: {path}");
                }
                else
                {
                    Debug.WriteLine($"path is not rooted, skip removing process: {path}");
                }
                _storageItemsRepository.DeleteIgnorePath(path);
                Debug.WriteLine($"Remove ignored StorageItem from Db : {path}");
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
        try
        {
            var (token, item) = await _storageItemsRepository.GetSourceStorageItem(path);

            // pathを包摂する登録済みフォルダがあれば、キャッシュ削除はスキップする
            if (_storageItemsRepository.IsIgnoredPath(item.Path))
            {
                if (item is StorageFolder folder)
                {
                    await foreach (var deletePath in GetAllDeletionPathsAsync(folder))
                    {
                        Debug.WriteLine($"Delete cache: {deletePath}");
                        await DeleteCacheAllUnderPathAsync(deletePath);
                    }

                    await DeleteCachePathAsync(folder.Path);
                }
                else
                {
                    Debug.WriteLine($"Delete cache: {path}");
                    await DeleteCachePathAsync(path);
                }
            }
            else
            {
                Debug.WriteLine($"Skiped delete cache: {path}");
            }

            _storageItemsRepository.RemoveFolder(token);
        }
        catch { }
    }

    async Task DeleteCacheAllUnderPathAsync(string path)
    {
        var tasks = new[] {
            _thumbnailImageMaintenanceService.DeleteAllThumbnailUnderPathAsync(path),
            _secondaryTileManager.RemoveSecondaryTile(path)
        };

        _recentlyAccessRepository.DeleteAllUnderPath(path);
        _bookmarkManager.RemoveAllBookmarkUnderPath(path);
        _folderContainerTypeManager.DeleteAllUnderPath(path);
        _folderLastIntractItemManager.RemoveAllUnderPath(path);
        _displaySettingsByPathRepository.DeleteUnderPath(path);
        _archiveFileInnerStructureCache.DeleteUnderPath(path);

        await Task.WhenAll(tasks);
    }

    async Task DeleteCachePathAsync(string path)
    {
        var tasks = new[] {
            _thumbnailImageMaintenanceService.DeleteThumbnailFromPathAsync(path),
            _secondaryTileManager.RemoveSecondaryTile(path)
        };

        _recentlyAccessRepository.Delete(path);
        _bookmarkManager.RemoveBookmark(path);
        _folderContainerTypeManager.Delete(path);
        _folderLastIntractItemManager.Remove(path);
        _displaySettingsByPathRepository.Delete(path);
        _archiveFileInnerStructureCache.Delete(path);

        await Task.WhenAll(tasks);
    }


    async IAsyncEnumerable<string> GetAllDeletionPathsAsync(StorageFolder folder)
    {
        // 子孫フォルダ内のコンテンツを消さないように対象とするフォルダを列挙する
        var descendantPaths = await _storageItemsRepository.GetDescendantItemPathsAsync(folder.Path).ToListAsync();

        bool IsSkipPath(string path)
        {
            return folder.Path == path || descendantPaths.Any(x => path.StartsWith(x));
        }
        

        var query = folder.CreateFolderQueryWithOptions(new Windows.Storage.Search.QueryOptions() { FolderDepth = Windows.Storage.Search.FolderDepth.Deep });
        await foreach (var folderItem in query.ToAsyncEnumerable())
        {
            if (IsSkipPath(folderItem.Path) is false)
            {
                yield return folderItem.Path;
            }
        }

        // 対象フォルダ上（子孫フォルダ含まず）のファイルを列挙
        var fileQuery = folder.CreateFileQueryWithOptions(new Windows.Storage.Search.QueryOptions(Windows.Storage.Search.CommonFileQuery.DefaultQuery, SupportedFileTypesHelper.GetAllSupportedFileExtensions()));
        await foreach (var folderItem in fileQuery.ToAsyncEnumerable())
        {
            if (IsSkipPath(folderItem.Path) is false)
            {
                yield return folderItem.Path;
            }
        }
    }
}
