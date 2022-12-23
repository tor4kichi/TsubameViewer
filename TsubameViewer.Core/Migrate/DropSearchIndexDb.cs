using LiteDB;
using System;
using System.Collections.Generic;
using System.Text;

namespace TsubameViewer.Core.UseCases.Migrate;

public sealed class DropSearchIndexDb : IMigrater
{
    private readonly ILiteDatabase _liteDatabase;

    const string RemovedSearchIndexCollectionName = "StorageItemSearchEntry";

    public DropSearchIndexDb(ILiteDatabase liteDatabase)
    {
        _liteDatabase = liteDatabase;
    }

    public Version TargetVersion { get; } = new Version(1, 5, 0);
    
    public void Migrate()
    {
        if (_liteDatabase.CollectionExists(RemovedSearchIndexCollectionName))
        {
            _liteDatabase.DropCollection(RemovedSearchIndexCollectionName);
        }
    }
}
