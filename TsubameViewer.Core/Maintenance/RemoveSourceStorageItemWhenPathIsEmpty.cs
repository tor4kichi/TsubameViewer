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
using ZLinq;

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

        // 整理しなくても勝手に消えるし、MostRecentlyUsedListを使う場面でファイルが無いなりの処理を選択すれば十分
        //await StorageApplicationPermissions.MostRecentlyUsedList.Entries.ToAwaitableParallelTaskAsync(async entry =>
        //{
        //    try
        //    {
        //        var item = await StorageApplicationPermissions.MostRecentlyUsedList.GetItemAsync(entry.Token);
        //        if (string.IsNullOrEmpty(item.Path))
        //        {
        //            StorageApplicationPermissions.MostRecentlyUsedList.Remove(entry.Token);
        //        }
        //    }
        //    catch (FileNotFoundException)
        //    {
        //        StorageApplicationPermissions.MostRecentlyUsedList.Remove(entry.Token);
        //    }
        //});
    }
}
