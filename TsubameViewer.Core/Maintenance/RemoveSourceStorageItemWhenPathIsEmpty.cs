using LiteDB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Core.Contracts.Maintenance;
using TsubameViewer.Core.Models.SourceFolders;
using Windows.Storage.AccessCache;

namespace TsubameViewer.Core.Maintenance;

public sealed class RemoveSourceStorageItemWhenPathIsEmpty : ILaunchTimeMaintenanceAsync
{
    private readonly ILiteDatabase _liteDatabase;

    public RemoveSourceStorageItemWhenPathIsEmpty(ILiteDatabase liteDatabase)
    {
        _liteDatabase = liteDatabase;
    }

    async Task ILaunchTimeMaintenanceAsync.MaintenanceAsync()
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
            catch (FileNotFoundException)
            {
                StorageApplicationPermissions.MostRecentlyUsedList.Remove(entry.Token);
            }
        }
    }
}
