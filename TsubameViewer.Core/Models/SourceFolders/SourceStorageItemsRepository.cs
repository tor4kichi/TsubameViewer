﻿using LiteDB;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Search;

#nullable enable
namespace TsubameViewer.Core.Models.SourceFolders;

// StorageApplicationPermissionsについて
// https://docs.microsoft.com/ja-jp/windows/uwp/files/how-to-track-recently-used-files-and-folders#use-a-token-to-retrieve-an-item-from-the-mru
//
// MostRecentlyUsedList と FutureAccessList のアイテムに対するtokenとパスの組に対する検索が出来るようにした    

public sealed class SourceStorageItemsRepository
{
    internal async Task RefreshTokenToPathDbAsync()
    {
        _tokenToPathRepository.Clear();

        foreach (var entry in StorageApplicationPermissions.FutureAccessList.Entries)
        {
            try
            {
                var item = await StorageApplicationPermissions.FutureAccessList.GetItemAsync(entry.Token, AccessCacheOptions.FastLocationsOnly);
                _tokenToPathRepository.Add(TokenListType.FutureAccessList, entry.Token, item.Path);
            }
            catch
            {

            }
        }

        foreach (var entry in StorageApplicationPermissions.MostRecentlyUsedList.Entries)
        {
            try
            {
                var item = await StorageApplicationPermissions.MostRecentlyUsedList.GetItemAsync(entry.Token, AccessCacheOptions.FastLocationsOnly);
                _tokenToPathRepository.Add(TokenListType.MostRecentlyUsedList, entry.Token, item.Path);                    
            }
            catch
            {

            }
        }
    }


    public sealed class SourceStorageItemAddedMessageData
    {
        internal SourceStorageItemAddedMessageData() { }

        public string Token { get; set; }
        public IStorageItem StorageItem { get; set; }
        public string Metadata { get; set; }
        public TokenListType ListType { get; set; }
    }

    public sealed class SourceStorageItemAddedMessage : ValueChangedMessage<SourceStorageItemAddedMessageData>
    {
        public SourceStorageItemAddedMessage(SourceStorageItemAddedMessageData value) : base(value)
        {
        }
    }



    public sealed class SourceStorageItemRemovedMessageData
    {
        internal SourceStorageItemRemovedMessageData() { }

        public string Token { get; set; }

        public string Path { get; set; }
    }

    public sealed class SourceStorageItemRemovedMessage : ValueChangedMessage<SourceStorageItemRemovedMessageData>
    {
        public SourceStorageItemRemovedMessage(SourceStorageItemRemovedMessageData value) : base(value)
        {
        }
    }


    public sealed class SourceStorageItemMovedOrRenameMessageData
    {
        public string Token { get; set; }

        public string OldPath { get; set; }

        public string NewPath { get; set; }

        public bool IsRename => Path.GetDirectoryName(OldPath) == Path.GetDirectoryName(NewPath);
    }

    public sealed class SourceStorageItemMovedOrRenameMessage : ValueChangedMessage<SourceStorageItemMovedOrRenameMessageData>
    {
        public SourceStorageItemMovedOrRenameMessage(SourceStorageItemMovedOrRenameMessageData value) : base(value)
        {
        }
    }

    private sealed class TokenToPathEntry
    {
        [BsonId]
        public string Token { get; set; }

        public string Path { get; set; }

        public TokenListType TokenListType { get; set; }
    }

    public enum TokenListType
    {
        MostRecentlyUsedList,
        FutureAccessList
    }

    private sealed class TokenToPathRepository : Infrastructure.LiteDBServiceBase<TokenToPathEntry>
    {
        public TokenToPathRepository(LiteDB.ILiteDatabase liteDatabase) : base(liteDatabase)
        {
            _collection.EnsureIndex(x => x.Path);
            _collection.EnsureIndex(x => x.TokenListType);
        }

        public void Add(TokenListType tokenListType, string token, string path)
        {
            // 古いトークンは捨てるように
            _collection.DeleteMany(x => x.Path == path && x.TokenListType == tokenListType);

            _collection.Upsert(new TokenToPathEntry() 
            {
                TokenListType = tokenListType,
                Path = path,
                Token = token,
            });
        }

        internal TokenToPathEntry GetPathFromToken(string token)
        {
            return _collection.FindOne(x => x.Token == token);
        }

        internal TokenToPathEntry GetTokenFromPathExact(string exactPath)
        {
            return _collection.Find(x => x.Path == exactPath).FirstOrDefault();
        }

        internal TokenToPathEntry GetTokenFromPath(string path)
        {
            return _collection.Find(x => x.Path == path).FirstOrDefault()
                ?? _collection.Find(x => path.StartsWith(x.Path)).OrderByDescending(x => x.Path.Length).FirstOrDefault();
        }

        internal IEnumerable<TokenToPathEntry> GetAllTokenFromPath(string path)
        {
            return _collection.Find(x => path.StartsWith(x.Path));
        }

        internal TokenToPathEntry FindTokenToPathFromRoot(string path)
        {
            TokenToPathEntry token = null;
            while (Path.GetDirectoryName(path) is not null and var dir && string.IsNullOrEmpty(dir))
            {
                token = _collection.Find(x => dir == x.Path).FirstOrDefault();
                if (token is not null)
                {
                    break;
                }
            }

            return token;
        }

        internal IEnumerable<TokenToPathEntry> FindTokenToPathAll(string path)
        {
            return _collection.Find(x => path.StartsWith(x.Path));
        }

        public bool IsExistPath(string path)
        {
            return _collection.Exists(x => x.Path == path);
        }

        public bool IsAvairableAccessPath(string path)
        {
            return _collection.Exists(x => path.StartsWith(x.Path));
        }

        internal void Clear()
        {
            _collection.DeleteAll();
        }
    }

    private readonly TokenToPathRepository _tokenToPathRepository;
    private readonly IgnoreStorageItemRepository _ignoreStorageItemRepository;
    private readonly IMessenger _messenger;

    public SourceStorageItemsRepository(
        IMessenger messenger,
        LiteDB.ILiteDatabase liteDatabase
        )
    {
        _tokenToPathRepository = new TokenToPathRepository(liteDatabase);
        _ignoreStorageItemRepository = new IgnoreStorageItemRepository(liteDatabase);

        StorageApplicationPermissions.MostRecentlyUsedList.ItemRemoved += MostRecentlyUsedList_ItemRemoved;
        _messenger = messenger;
    }

    private void MostRecentlyUsedList_ItemRemoved(StorageItemMostRecentlyUsedList sender, ItemRemovedEventArgs args)
    {
        Debug.WriteLine($"システムにより最近使ったファイルからアイテムが削除されました: token {args.RemovedEntry.Token}");
        var token = _tokenToPathRepository.FindById(args.RemovedEntry.Token);
        _tokenToPathRepository.DeleteItem(args.RemovedEntry.Token);
        try
        {
            if (token is not null)
            {
                Debug.WriteLine($"システムにより最近使ったファイルからアイテムが削除されました: path {token.Path}");
                _messenger.Send(new SourceStorageItemRemovedMessage(new() { Token = token.Token, Path = token.Path }));
            }
        }
        catch { }
    }


    public bool IsSourceStorageItem(string path)
    {
        if (string.IsNullOrEmpty(path)) { return false; }

        return _tokenToPathRepository.IsExistPath(path);
    }

    public async Task<(string Token, IStorageItem Item)> GetSourceStorageItem(string path)
    {
        var token = _tokenToPathRepository.GetTokenFromPathExact(path);
        return (token.Token, await GetItemAsync(token.Token));
    }

    public bool PathIsAccessAvailable(string path)
    {
        return _tokenToPathRepository.IsAvairableAccessPath(path);
    }

    /// <summary>
    /// 指定パスにアクセス可能な登録フォルダへのアクセス権をアプリが保有しているかを検査します。<br />
    /// FutureAccessList と MostRecentlyUsedList のどちらかでアクセス出来ればアクセス可能とします。
    /// </summary>
    /// <param name="path"></param>
    /// <exception cref="UnauthorizedAccessException"></exception>
    public void ThrowIfPathIsUnauthorizedAccess(string path)
    {
        if (PathIsAccessAvailable(path) is false) 
        {
            throw new UnauthorizedAccessException(path);
        }
    }

    public async IAsyncEnumerable<string> GetDescendantItemPathsAsync(string parentPath, bool withMostRecentlyUsed = false)
    {
        foreach (var entry in StorageApplicationPermissions.FutureAccessList.Entries)
        {
            var item = await StorageApplicationPermissions.FutureAccessList.GetItemAsync(entry.Token, AccessCacheOptions.FastLocationsOnly);
            if (parentPath != item.Path && item.Path.StartsWith(parentPath))
            {
                yield return item.Path;
            }
        }
        
        if (withMostRecentlyUsed)
        {
            foreach (var entry in StorageApplicationPermissions.MostRecentlyUsedList.Entries)
            {
                var item = await StorageApplicationPermissions.MostRecentlyUsedList.GetItemAsync(entry.Token, AccessCacheOptions.FastLocationsOnly);
                if (parentPath != item.Path && item.Path.StartsWith(parentPath))
                {
                    yield return item.Path;
                }
            }
        }
    }

    public async Task<string> AddFileTemporaryAsync(IStorageItem storageItem, string metadata)
    {
#if WINDOWS_UWP
        var list = StorageApplicationPermissions.MostRecentlyUsedList;
        string token = null;

        try
        {
            foreach (var entry in list.Entries)
            {
                var item = await list.GetItemAsync(entry.Token, AccessCacheOptions.FastLocationsOnly);
                if (item.Path == storageItem.Path)
                {
                    token = entry.Token;
                    break;
                }
            }
        }
        catch { }

        token ??= Guid.NewGuid().ToString();

        if (metadata != null)
        {
            list.AddOrReplace(token, storageItem, metadata);
        }
        else
        {
            list.AddOrReplace(token, storageItem);
        }
#else
        throw new NotImplementedException();
#endif
        if (string.IsNullOrEmpty(storageItem.Path) is false)
        {
            _tokenToPathRepository.Add(TokenListType.MostRecentlyUsedList, token, storageItem.Path);
        }

        _messenger.Send(new SourceStorageItemAddedMessage(new () 
        {
            Token = token,
            StorageItem = storageItem,
            Metadata = metadata,
            ListType = TokenListType.MostRecentlyUsedList,
        }));

        return token;
    }


    public async Task<string> AddItemPersistantAsync(IStorageItem storageItem, string metadata)
    {

#if WINDOWS_UWP
        string token = null;

        foreach (var entry in StorageApplicationPermissions.FutureAccessList.Entries)
        {
            var item = await StorageApplicationPermissions.FutureAccessList.GetItemAsync(entry.Token, AccessCacheOptions.FastLocationsOnly);
            if (item.Path == storageItem.Path)
            {
                token = entry.Token;
                break;
            }
        }

        token ??= Guid.NewGuid().ToString();

        if (metadata != null)
        {
            StorageApplicationPermissions.FutureAccessList.AddOrReplace(token, storageItem, metadata);
        }
        else
        {
            StorageApplicationPermissions.FutureAccessList.AddOrReplace(token, storageItem);
        }
#else
        throw new NotImplementedException();
#endif
        _tokenToPathRepository.Add(TokenListType.FutureAccessList, token, storageItem.Path);

        _messenger.Send(new SourceStorageItemAddedMessage(new()
        {
            Token = token,
            StorageItem = storageItem,
            Metadata = metadata,
            ListType = TokenListType.FutureAccessList,
        }));

        return token;
    }

    ConcurrentDictionary<string, IStorageItem> _cached = new ();
    
    private async Task<IStorageItem> GetItemAsync(string token)
    {
        if (_cached.TryGetValue(token, out var item)) { return item; }

        if (StorageApplicationPermissions.MostRecentlyUsedList.ContainsItem(token))
        {
            item = await StorageApplicationPermissions.MostRecentlyUsedList.GetItemAsync(token);
        }

        if (StorageApplicationPermissions.FutureAccessList.ContainsItem(token))
        {
            try
            {
                item = await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(token);
            }
            catch { }

            try
            {
                item ??= await StorageApplicationPermissions.FutureAccessList.GetFileAsync(token);
            }
            catch { }
        }

        if (item is not null)
        {
            _cached.TryAdd(token, item);
        }

        return item;
    }
    
    private async Task<TokenToPathEntry> CheckAndGetTokenToPathAfterRefreshDb(string targetPath)
    {
        // 一旦全てのトークンとパスの組み合わせをバッファ
        var oldTokenToPathMap = _tokenToPathRepository.ReadAllItems().ToDictionary(x => x.Token, x => x.Path);

        // TokenToPathRepositoryを再構築する
        await RefreshTokenToPathDbAsync();

        // 改めてtargetPathと組になっているトークンを取り出す
        // ここで取れなかった場合や、意図しないフォルダが取れてしまった場合はユーザーにフォルダの再登録を要求する必要がある
        var token = _tokenToPathRepository.GetTokenFromPath(targetPath);
        if (token == null) { return null; }

        // トークンと一致するバッファした組におけるパスが旧パスである
        var oldPath = oldTokenToPathMap[token.Token];

        // トークンに対応するアイテムを取得する（targetPathとトークンが示すアイテムのPathが異なる可能性がある）
        var storageItem = await GetItemAsync(token.Token);

        // トークン、パス、旧パスを元にフォルダ変更イベントをトリガーする
        _messenger.Send(new SourceStorageItemMovedOrRenameMessage(new() { Token = token.Token, OldPath = oldPath, NewPath = storageItem.Path }));

        return token;
    }

    public async Task<IStorageItem> TryGetStorageItemFromPath(string path)
    {            
        // .Where() は 例えば「_A_B」と「_A」というフォルダ名を分別するために必要
        var tokenEntries = _tokenToPathRepository.GetAllTokenFromPath(path).ToArray();

        // 登録アイテムがリネーム等されていた場合に内部DBを再構築する
        // 理想的には変更部分だけを差分更新するべき
        if (tokenEntries.Any() is false)
        {
            var token = await CheckAndGetTokenToPathAfterRefreshDb(path);
            if (token == null)
            {
                return null;
            }
            tokenEntries = new[] { token };
        }

        async Task<IStorageItem> FindStorageItem(TokenToPathEntry tokenEntry)
        {
            try
            {
                var tokenStorageItem = await GetItemAsync(tokenEntry.Token);
                if (tokenStorageItem?.Path == path)
                {
                    return tokenStorageItem;
                }
                else if (tokenStorageItem is StorageFolder folder)
                {
                    var subtractPath = path.Substring(tokenStorageItem.Path.Length);
                    return await FolderHelper.GetFolderItemFromPath(folder, subtractPath);
                }
                else
                {
                    if (tokenEntry.TokenListType == TokenListType.MostRecentlyUsedList)
                    {
                        _tokenToPathRepository.DeleteItem(tokenEntry.Token);
                    }
                }
            }
            catch (FileNotFoundException) { }

            return null;
        }

        foreach (var tokenEntry in tokenEntries)
        {
            if (await FindStorageItem(tokenEntry) is not null and IStorageItem result) { return result; }
        }

        return null;
    }

    public void RemoveFolder(string token)
    {
        var entry = _tokenToPathRepository.GetPathFromToken(token);
        bool isRemoved = false;
#if WINDOWS_UWP
        if (StorageApplicationPermissions.MostRecentlyUsedList.ContainsItem(token))
        {
            StorageApplicationPermissions.MostRecentlyUsedList.Remove(token);
            isRemoved = true;
        }
        else if (StorageApplicationPermissions.FutureAccessList.ContainsItem(token))
        {
            StorageApplicationPermissions.FutureAccessList.Remove(token);
            isRemoved = true;
        }
#else
        throw new NotImplementedException();
#endif
        if (isRemoved)
        {
            _tokenToPathRepository.DeleteItem(token);
            _messenger.Send(new SourceStorageItemRemovedMessage(new () { Token = token, Path = entry.Path }));
        }
    }

    public string? GetPathFromToken(string token)
    {
        return _tokenToPathRepository.FindById(token)?.Path;
    }

    public async IAsyncEnumerable<(IStorageItem? item, string token, string metadata)> GetParsistantItems([EnumeratorCancellation] CancellationToken ct = default)
    {
#if WINDOWS_UWP
        var myItems = StorageApplicationPermissions.FutureAccessList.Entries;
        foreach (var item in myItems)
        {
            ct.ThrowIfCancellationRequested();
            IStorageItem? storageItem = null;
            try
            {
                storageItem = await StorageApplicationPermissions.FutureAccessList.GetItemAsync(item.Token);
            }
            catch (FileNotFoundException) { }

            yield return (storageItem, item.Token, item.Metadata);
        }
#else
        // TODO: GetSourceFolders() UWP以外での対応
        throw new NotImplementedException();
#endif
    }

    public async IAsyncEnumerable<(IStorageItem item, string token, string metadata)> GetTemporaryItems([EnumeratorCancellation] CancellationToken ct = default)
    {
#if WINDOWS_UWP
        var myItems = StorageApplicationPermissions.MostRecentlyUsedList.Entries;
        
        foreach (var item in myItems)
        {
            ct.ThrowIfCancellationRequested();
            IStorageItem storageItem = null;
            try
            {
                storageItem = await StorageApplicationPermissions.MostRecentlyUsedList.GetItemAsync(item.Token);
            }
            catch (FileNotFoundException) 
            {
                StorageApplicationPermissions.MostRecentlyUsedList.Remove(item.Token);
            }

            if (storageItem is not null)
            {
                yield return (storageItem, item.Token, item.Metadata);
            }
        }
#else
        // TODO: GetSourceFolders() UWP以外での対応
        throw new NotImplementedException();
#endif
    }


    public IEnumerable<string> GetParsistantItemsFromCache()
    {
        return _tokenToPathRepository.ReadAllItems()
            .Where(x => x.TokenListType == TokenListType.FutureAccessList).Select(x => x.Path);
    }

    public async IAsyncEnumerable<IStorageItem> SearchAsync(string keyword, [EnumeratorCancellation] CancellationToken ct)
    {
        static IAsyncEnumerable<IStorageItem> SearchInFolder(StorageFolder folder, QueryOptions queryOptions, CancellationToken ct)
        {
            var query = folder.CreateItemQueryWithOptions(queryOptions);
            return query.ToAsyncEnumerable(ct);
        }

        QueryOptions queryOptions = new QueryOptions(CommonFileQuery.DefaultQuery, SupportedFileTypesHelper.GetAllSupportedFileExtensions()) 
        {
            ApplicationSearchFilter = $"System.FileName:\"*{keyword}*\"",
            FolderDepth = FolderDepth.Deep,
        };

        await foreach (var (item, token, metadata) in GetParsistantItems(ct).WithCancellation(ct))
        {
            if (item?.Name.Contains(keyword) ?? false)
            {
                yield return item;
            }

            if (item is StorageFolder folder)
            {                    
                await foreach (var folderItem in SearchInFolder(folder, queryOptions, ct).WithCancellation(ct))
                {                    
                    yield return folderItem;
                }
            }
        }

        await foreach (var (item, token, metadata) in GetTemporaryItems(ct).WithCancellation(ct))
        {
            if (item.Name.Contains(keyword))
            {
                yield return item;
            }

            if (item is StorageFolder folder)
            {
                await foreach (var folderItem in SearchInFolder(folder, queryOptions, ct).WithCancellation(ct))
                {
                    yield return folderItem;
                }
            }
        }
    }

    
}
