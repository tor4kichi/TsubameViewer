using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TsubameViewer.Core.Contracts.Maintenance;
using TsubameViewer.Core.Contracts.Services;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.SourceFolders;
using Windows.Devices.Geolocation;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Shapes;

namespace TsubameViewer.Core.Models.Maintenance;

public sealed class StroageItemAccessRemovedMessage : ValueChangedMessage<string>
{
    public StroageItemAccessRemovedMessage(string value) : base(value)
    {
    }
}

public sealed class StroageItemMovedOrRenamedMessage : ValueChangedMessage<(string OldPath, string NewPath)>
{
    public StroageItemMovedOrRenamedMessage(string OldPath, string NewPath) : base((OldPath, NewPath))
    {
    }
}


/// <summary>
/// ソース管理に変更が加えられて、新規に管理するストレージアイテムが増えた・減った際に
/// ローカルDBや画像サムネイルの破棄などを行う
/// 単にソース管理が消されたからと破棄処理をしてしまうと包含関係のフォルダ追加を許容できなくなるので
/// 包含関係のフォルダに関するキャッシュの削除をスキップするような動作が含まれる
/// </summary>
public sealed class SyncStorageCacheService :
    ILaunchTimeMaintenanceAsync,
    IRecipient<SourceStorageItemIgnoringRequestMessage>,
    IRecipient<SourceStorageItemsRepository.SourceStorageItemMovedOrRenameMessage>,
    IRecipient<StroageItemMovedOrRenamedMessage>
    
{
    private readonly IMessenger _messenger;
    private readonly SourceStorageItemsRepository _storageItemsRepository;
    private readonly IgnoreStorageItemRepository _ignoreStorageItemRepository;
    private readonly RecentlyAccessRepository _recentlyAccessRepository;
    private readonly LocalBookmarkRepository _bookmarkManager;
    private readonly FolderContainerTypeManager _folderContainerTypeManager;
    private readonly IThumbnailImageMaintenanceService _thumbnailImageMaintenanceService;
    private readonly ISecondaryTileManager _secondaryTileManager;
    private readonly LastIntractItemRepository _folderLastIntractItemManager;
    private readonly DisplaySettingsByPathRepository _displaySettingsByPathRepository;
    private readonly ArchiveFileInnerStructureCache _archiveFileInnerStructureCache;
    private readonly FolderStructureFilesRepository _folderStructureFilesRepository;
    private readonly AlbamRepository _albamRepository;

    public SyncStorageCacheService(
        IMessenger messenger,        
        SourceStorageItemsRepository storageItemsRepository,
        IgnoreStorageItemRepository ignoreStorageItemRepository,
        RecentlyAccessRepository recentlyAccessRepository,
        LocalBookmarkRepository bookmarkManager,
        FolderContainerTypeManager folderContainerTypeManager,
        IThumbnailImageMaintenanceService thumbnailImageMaintenanceService,
        ISecondaryTileManager secondaryTileManager,
        LastIntractItemRepository folderLastIntractItemManager,
        DisplaySettingsByPathRepository displaySettingsByPathRepository,
        ArchiveFileInnerStructureCache archiveFileInnerStructureCache,
        FolderStructureFilesRepository folderStructureFilesRepository,
        Albam.AlbamRepository albamRepository
        )
    {
        _messenger = messenger;
        _storageItemsRepository = storageItemsRepository;
        _ignoreStorageItemRepository = ignoreStorageItemRepository;
        _recentlyAccessRepository = recentlyAccessRepository;
        _bookmarkManager = bookmarkManager;
        _folderContainerTypeManager = folderContainerTypeManager;
        _thumbnailImageMaintenanceService = thumbnailImageMaintenanceService;
        _secondaryTileManager = secondaryTileManager;
        _folderLastIntractItemManager = folderLastIntractItemManager;
        _displaySettingsByPathRepository = displaySettingsByPathRepository;
        _archiveFileInnerStructureCache = archiveFileInnerStructureCache;
        _folderStructureFilesRepository = folderStructureFilesRepository;
        _albamRepository = albamRepository;
        _messenger.Register<SourceStorageItemIgnoringRequestMessage>(this);
        _messenger.Register<SourceStorageItemsRepository.SourceStorageItemMovedOrRenameMessage>(this);
        _messenger.Register<StroageItemMovedOrRenamedMessage>(this);
    }

    public void Receive(SourceStorageItemsRepository.SourceStorageItemMovedOrRenameMessage message)
    {
        var oldPath = message.Value.OldPath;
        var newPath = message.Value.NewPath;

        Debug.WriteLine($"Start folder change process.");
        Debug.WriteLine($"OldPath = {oldPath}");
        Debug.WriteLine($"NewPath = {newPath}");

        ProcessStorageItemMovedAsync(oldPath, newPath).FireAndForgetSafe();
    }

    public void Receive(StroageItemMovedOrRenamedMessage m)
    {
        ProcessStorageItemMovedAsync(m.Value.OldPath, m.Value.NewPath).FireAndForgetSafe();
    }

    private async Task ProcessStorageItemMovedAsync(string oldPath, string newPath)
    {

        // Note: ドライブレターに含まれる : コロン が含まれる文字列を
        // 直接FindByIdするとLiteDatabaseがエラーを起こす（BsonExpression変換中のエラー）
        try
        {
            var tasks = new[] {
                _thumbnailImageMaintenanceService.FolderChangedAsync(oldPath, newPath),
                _secondaryTileManager.RemoveSecondaryTile(newPath)
            };

            _displaySettingsByPathRepository.PathChanged(oldPath, newPath);
            _bookmarkManager.PathChanged(oldPath, newPath);
            _recentlyAccessRepository.PathChanged(oldPath, newPath);
            _folderContainerTypeManager.PathChanged(oldPath, newPath);
            _archiveFileInnerStructureCache.PathChanged(oldPath, newPath);
            _folderStructureFilesRepository.PathChanged(oldPath, newPath);
            _albamRepository.PathChanged(oldPath, newPath);

            _folderLastIntractItemManager.Remove(oldPath);

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


    async Task ILaunchTimeMaintenanceAsync.MaintenanceAsync()
    {
        Debug.WriteLine($"Restored CacheDeletionWhenSourceStorageItemIgnored.");
        while (TryPeekIgnoredPath(out string path))
        {
            try
            {
                await DeleteCacheWithDescendantsAsync(path);
            }
            finally
            {
                DeleteIgnorePath(path);
            }
        }
    }

    void IRecipient<SourceStorageItemIgnoringRequestMessage>.Receive(SourceStorageItemIgnoringRequestMessage message)
    {
        message.Reply(
            Task.Run(async () => 
            {                
                var path = message.Path;
                AddIgnoreToken(path);

                try
                {
                    await DeleteCacheWithDescendantsAsync(path);                    
                    return new StorageItemDeletionResult();
                }
                catch
                {
                    throw;
                }
                finally
                {
                    DeleteIgnorePath(path);
                }
            })
        );
    }


    #region Ignore Process

    public void AddIgnoreToken(string path)
    {
        if (_ignoreStorageItemRepository.IsIgnoredPath(path) is false)
        {
            Debug.WriteLine($"除去対象パスに追加 {path}");
            _ignoreStorageItemRepository.CreateItem(new() { Path = path });
        }
    }

    public bool IsIgnoredPathExact(string path)
    {
        return IsIgnoredPathExact(path);
    }

    public bool HasIgnorePath()
    {
        return _ignoreStorageItemRepository.Any();
    }

    public bool TryPeekIgnoredPath(out string path)
    {
        if (_ignoreStorageItemRepository.TryPeek(out var entry))
        {
            path = entry.Path;
            return true;
        }
        else
        {
            path = null;
            return false;
        }
    }

    public void DeleteIgnorePath(string path)
    {
        Debug.WriteLine($"除去対象パスから削除 {path}");
        _ignoreStorageItemRepository.DeleteItem(path);

    }

    #endregion


    async Task DeleteCacheWithDescendantsAsync(string path)
    {
        Debug.WriteLine($"除去作業を開始 {path}");

        try
        {
            var (token, item) = await _storageItemsRepository.GetSourceStorageItem(path);

            Debug.WriteLine($"除去対象のトークン {token}");
            // pathを包摂する登録済みフォルダがあれば、キャッシュ削除はスキップする
            if (item is StorageFolder folder)
            {
                Debug.WriteLine($"除去対象はフォルダです。");
                // 子孫フォルダ内のコンテンツを消さないように対象とするフォルダを列挙する
                var descendantPaths = await _storageItemsRepository.GetDescendantItemPathsAsync(folder.Path).ToListAsync();

                bool IsDeleteTargetPath(string path)
                {
                    if (descendantPaths.Any(path.StartsWith))
                    {
                        Debug.WriteLine($"保持されるフォルダのためスキップ: {path}");
                        return false;
                    }
                    else { return true; }
                }

                await GetAllDeletionPathsAsync(folder, (storageItem) => IsDeleteTargetPath(storageItem.Path), async (storageItem) => await DeleteCacheAllUnderPathAsync(storageItem.Path));
            }
            else
            {
                Debug.WriteLine($"除去対象はファイルです。");
                await DeleteCachePathAsync(path);
            }

            Debug.WriteLine($"StorageSourceから当該トークンを削除 {token}");
            _storageItemsRepository.RemoveFolder(token);
            Debug.WriteLine($"全ての除去処理を完了しました {path}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"除去処理に失敗しました");
            Debug.WriteLine(ex.ToString());
            throw;
        }
    }

    async Task DeleteCacheAllUnderPathAsync(string path)
    {
        Debug.WriteLine($"対象パス以下の全てのデータを除去します {path}");

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
        _folderStructureFilesRepository.FolderRemoved(path);
        _albamRepository.DeleteAlbamItemsUnderPath(path);

        await Task.WhenAll(tasks);
        Debug.WriteLine($"対象パス以下の全てのデータを除去完了 {path}");
        _messenger.Send(new StroageItemAccessRemovedMessage(path));
    }


    async Task DeleteCachePathAsync(string path)
    {
        Debug.WriteLine($"対象パスのデータを除去します {path}");

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
        _folderStructureFilesRepository.FileRemoved(path);
        _albamRepository.DeleteAlbamItemsUnderPath(path);

        await Task.WhenAll(tasks);
        Debug.WriteLine($"対象パスのデータを除去完了 {path}");
        _messenger.Send(new StroageItemAccessRemovedMessage(path));
    }


    async Task GetAllDeletionPathsAsync(StorageFolder folder, Predicate<IStorageItem> targetPredicate, Func<IStorageItem, Task> actionTask)
    {        
        var query = folder.CreateFolderQueryWithOptions(new Windows.Storage.Search.QueryOptions() { FolderDepth = Windows.Storage.Search.FolderDepth.Deep});

        List<Task> deleteTasks = [];
        //await foreach (var folderItem in query.ToAsyncEnumerable())
        //{
        //    if (targetPredicate(folderItem))
        //    {
        //        Debug.WriteLine($"除去する {folderItem.Path}");
        //        deleteTasks.Add(actionTask(folderItem));
        //    }
        //    else
        //    {
        //        Debug.WriteLine($"除去しない {folderItem.Path}");
        //    }
        //}

        //// 対象フォルダ上（子孫フォルダ含まず）のファイルを列挙
        //var fileQuery = folder.CreateFileQueryWithOptions(new Windows.Storage.Search.QueryOptions(Windows.Storage.Search.CommonFileQuery.DefaultQuery, SupportedFileTypesHelper.GetAllSupportedFileExtensions()));
        //await foreach (var folderItem in fileQuery.ToAsyncEnumerable())
        //{
        //    if (targetPredicate(folderItem))
        //    {
        //        Debug.WriteLine($"除去する {folderItem.Path}");
        //        deleteTasks.Add(actionTask(folderItem));
        //    }
        //    else
        //    {
        //        Debug.WriteLine($"除去しない {folderItem.Path}");
        //    }
        //}

        // 最後に渡されたフォルダも処理させる
        if (targetPredicate(folder))
        {
            Debug.WriteLine($"除去する {folder.Path}");
            deleteTasks.Add(actionTask(folder));
        }
        else
        {
            Debug.WriteLine($"除去しない {folder.Path}");
        }

        await Task.WhenAll(deleteTasks);
    }
}
