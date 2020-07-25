﻿using Prism.Events;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Windows.Storage;
using Windows.Storage.AccessCache;

namespace TsubameViewer.Models.Domain.SourceManagement
{
    public sealed class StoredFoldersRepository
    {
        public sealed class AddedEventArgs
        {
            internal AddedEventArgs() { }

            public string Token { get; set; }
            public IStorageItem StorageItem { get; set; }
        }

        public sealed class AddedEvent : PubSubEvent<AddedEventArgs> { }


        private readonly IEventAggregator _eventAggregator;

        public StoredFoldersRepository(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
        }

        public string AddFolder(IStorageItem storageItem)
        {
            var token = Guid.NewGuid().ToString();
            StorageApplicationPermissions.FutureAccessList.AddOrReplace(token, storageItem);
            
            _eventAggregator.GetEvent<AddedEvent>().Publish(new AddedEventArgs() 
            {
                Token = token,
                StorageItem = storageItem,
            });

            return token;
        }

        public async IAsyncEnumerable<(IStorageItem item, string token)> GetStoredFolderItems([EnumeratorCancellation] CancellationToken ct = default)
        {
#if WINDOWS_UWP
            var myItems = StorageApplicationPermissions.FutureAccessList.Entries;
            foreach (var item in myItems)
            {
                ct.ThrowIfCancellationRequested();
                yield return (await StorageApplicationPermissions.FutureAccessList.GetItemAsync(item.Token), item.Token);
            }
#else
            // TODO: GetStoredFolderItems() UWP以外での対応
#endif
        }

    }
}