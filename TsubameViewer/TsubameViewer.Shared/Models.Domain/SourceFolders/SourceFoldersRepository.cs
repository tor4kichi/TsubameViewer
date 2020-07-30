using Prism.Events;
using System;
using System.Collections.Generic;
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

        public string AddFolder(IStorageItem storageItem, string metadata = null)
        {
            var token = Guid.NewGuid().ToString();
#if WINDOWS_UWP
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

        public async Task<StorageFolder> GetFolderAsync(string token)
        {
            return await StorageApplicationPermissions.FutureAccessList.GetFolderAsync(token);
        }

        public async Task<(IStorageItem item, string metadata)> GetStorageItemAsync(string token)
        {
            var entry = StorageApplicationPermissions.FutureAccessList.Entries.FirstOrDefault(x => x.Token == token);
            if (entry.Token == null) { return default; }

            var storageItem = await StorageApplicationPermissions.FutureAccessList.GetItemAsync(token);

            return (storageItem, entry.Metadata);
        }


        public void RemoveFolder(string token)
        {
#if WINDOWS_UWP
            StorageApplicationPermissions.FutureAccessList.Remove(token);
#else
            throw new NotImplementedException();
#endif
            _eventAggregator.GetEvent<RemovedEvent>().Publish(new RemovedEventArgs() { Token = token });
        }

        public async IAsyncEnumerable<(IStorageItem item, string token, string metadata)> GetSourceFolders([EnumeratorCancellation] CancellationToken ct = default)
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
