using Prism.Events;
using System;
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

namespace TsubameViewer.Models.Domain.SourceFolders
{
    public sealed class SourceStorageItemsRepository
    {
        public sealed class AddedEventArgs
        {
            internal AddedEventArgs() { }

            public string Token { get; set; }
            public IStorageItem StorageItem { get; set; }
            public string Metadata { get; set; }
        }

        public sealed class AddedEvent : PubSubEvent<AddedEventArgs> { }



        public sealed class RemovedEventArgs
        {
            internal RemovedEventArgs() { }

            public string Token { get; set; }
        }

        public sealed class RemovedEvent : PubSubEvent<RemovedEventArgs> { }





        private readonly IEventAggregator _eventAggregator;

        public SourceStorageItemsRepository(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
        }

        public async Task<string> AddFileTemporaryAsync(StorageFile storageItem, string metadata)
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
            _eventAggregator.GetEvent<AddedEvent>().Publish(new AddedEventArgs()
            {
                Token = token,
                StorageItem = storageItem,
                Metadata = metadata
            });

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
            _eventAggregator.GetEvent<AddedEvent>().Publish(new AddedEventArgs() 
            {
                Token = token,
                StorageItem = storageItem,
                Metadata = metadata
            });

            return token;
        }

        Dictionary<string, IStorageItem> _cached = new Dictionary<string, IStorageItem>();

        public async Task<IStorageItem> GetItemAsync(string token)
        {
            if (_cached.TryGetValue(token, out var item)) { return item; }

            if (StorageApplicationPermissions.MostRecentlyUsedList.ContainsItem(token))
            {
                item = await StorageApplicationPermissions.MostRecentlyUsedList.GetFileAsync(token);
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
                _cached.Add(token, item);
            }

            return item;
        }


        public async Task<StorageFolder> GetFolderAsync(string token)
        {
            return await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(token);
        }

        public async Task<(IStorageItem item, string metadata)> GetParsistantItemAsync(string token)
        {
            var entry = StorageApplicationPermissions.FutureAccessList.Entries.FirstOrDefault(x => x.Token == token);
            if (entry.Token == null) { return default; }

            var storageItem = await StorageApplicationPermissions.FutureAccessList.GetItemAsync(token);

            return (storageItem, entry.Metadata);
        }

        public async Task<(IStorageItem item, string metadata)> GetTemporaryItemAsync(string token)
        {
            var entry = StorageApplicationPermissions.MostRecentlyUsedList.Entries.FirstOrDefault(x => x.Token == token);
            if (entry.Token == null) { return default; }

            var storageItem = await StorageApplicationPermissions.MostRecentlyUsedList.GetItemAsync(token);

            return (storageItem, entry.Metadata);
        }


        public async Task<IStorageItem> GetStorageItemFromPath(string token, string path)
        {
            var tokenStorageItem = await GetItemAsync(token);

            if (tokenStorageItem?.Path == path)
            {
                return tokenStorageItem;
            }
            else
            {
                // tokenからファイルやフォルダが取れなかった場合はpathをヒントにStorageFolderの取得を試みる
                await foreach (var item in GetParsistantItems())
                {
                    var folderPath = item.item.Path;
                    string dirName = Path.GetDirectoryName(path);
                    do
                    {
                        if (folderPath.Equals(dirName))
                        {
                            tokenStorageItem = item.item;
                            break;
                        }
                        dirName = Path.GetDirectoryName(dirName);
                    }
                    while (!string.IsNullOrEmpty(dirName));

                    if (tokenStorageItem != null) { break; }
                }
            }

            if (tokenStorageItem is StorageFolder folder)
            {
                var subtractPath = path.Substring(tokenStorageItem.Path.Length);
                return await FolderHelper.GetFolderItemFromPath(folder, subtractPath);
            }
            else
            {
                throw new Exception();
            }
        }



        public void RemoveFolder(string token)
        {
            bool isRemoved = false;
#if WINDOWS_UWP
            if (StorageApplicationPermissions.MostRecentlyUsedList.ContainsItem(token))
            {
                StorageApplicationPermissions.MostRecentlyUsedList.Remove(token);
                isRemoved = true;
            }
            if (StorageApplicationPermissions.FutureAccessList.ContainsItem(token))
            {
                StorageApplicationPermissions.FutureAccessList.Remove(token);
                isRemoved = true;
            }
#else
            throw new NotImplementedException();
#endif
            if (isRemoved)
            {
                _eventAggregator.GetEvent<RemovedEvent>().Publish(new RemovedEventArgs() { Token = token });
            }
        }

        public async IAsyncEnumerable<(IStorageItem item, string token, string metadata)> GetParsistantItems([EnumeratorCancellation] CancellationToken ct = default)
        {
#if WINDOWS_UWP
            var myItems = StorageApplicationPermissions.FutureAccessList.Entries;
            foreach (var item in myItems)
            {
                ct.ThrowIfCancellationRequested();
                var storageItem = await StorageApplicationPermissions.FutureAccessList.GetItemAsync(item.Token);
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
                catch (FileNotFoundException) { }

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


        public bool CanAddItem()
        {
#if WINDOWS_UWP
            return StorageApplicationPermissions.FutureAccessList.Entries.Count < StorageApplicationPermissions.FutureAccessList.MaximumItemsAllowed;
#else
            return false;
#endif
        }        
    }
}
