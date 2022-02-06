using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain.SourceFolders;
using Windows.Storage.AccessCache;

namespace TsubameViewer.Models.UseCase.Maintenance
{    
    public sealed class RemoveSourceStorageItemWhenPathIsEmpty : ILaunchTimeMaintenanceAsync
    {
        private readonly ILiteDatabase _liteDatabase;
        private readonly IScheduler _scheduler;

        public RemoveSourceStorageItemWhenPathIsEmpty(
            ILiteDatabase liteDatabase,
            IScheduler scheduler
            )
        {
            _liteDatabase = liteDatabase;
            _scheduler = scheduler;
        }

        Task ILaunchTimeMaintenanceAsync.MaintenanceAsync()
        {
            TaskCompletionSource tcs = new TaskCompletionSource();
            _scheduler.Schedule(async () => 
            {
                try
                {
                    if (_liteDatabase.CollectionExists(nameof(IgnoreStorageItemEntry)))
                    {
                        var removeCount = _liteDatabase.GetCollection<IgnoreStorageItemEntry>().DeleteMany(x => x.Path == String.Empty);
                    }
                }
                catch
                {
                    _liteDatabase.DropCollection(nameof(IgnoreStorageItemEntry));
                }

                foreach (var entry in StorageApplicationPermissions.MostRecentlyUsedList.Entries)
                {
                    try
                    {
                        var item = await StorageApplicationPermissions.MostRecentlyUsedList.GetItemAsync(entry.Token, AccessCacheOptions.FastLocationsOnly);
                        if (string.IsNullOrEmpty(item.Path))
                        {
                            StorageApplicationPermissions.MostRecentlyUsedList.Remove(entry.Token);
                        }
                    }
                    catch
                    {

                    }
                }
                tcs.SetResult();
            });

            return tcs.Task;
        }
    }
}
