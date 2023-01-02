using LiteDB;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TsubameViewer.Core.Models.Migrate;

public sealed class DropSearchIndexDb : IAsyncMigrater
{
    private readonly ILiteDatabase _liteDatabase;

    const string RemovedSearchIndexCollectionName = "StorageItemSearchEntry";

    public DropSearchIndexDb(ILiteDatabase liteDatabase)
    {
        _liteDatabase = liteDatabase;
    }

    public Version TargetVersion { get; } = new Version(1, 5, 0);
    
    public ValueTask MigrateAsync()
    {
        if (_liteDatabase.CollectionExists(RemovedSearchIndexCollectionName))
        {
            _liteDatabase.DropCollection(RemovedSearchIndexCollectionName);
        }

        return new ();
    }
}
