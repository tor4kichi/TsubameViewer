using LiteDB;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
#nullable enable
namespace TsubameViewer.Core.Models.Migrate;

public sealed class DropPathReferenceCountDb : IAsyncMigrater
{
    private readonly ILiteDatabase _liteDatabase;

    const string _removedSearchIndexCollectionName = "PathReferenceCountEntry";

    public DropPathReferenceCountDb(ILiteDatabase liteDatabase)
    {
        _liteDatabase = liteDatabase;
    }

    public Version? TargetVersion { get; } = new Version(1, 5, 0);

    public ValueTask MigrateAsync()
    {        
        if (_liteDatabase.CollectionExists(_removedSearchIndexCollectionName))
        {
            _liteDatabase.DropCollection(_removedSearchIndexCollectionName);
        }

        return new();
    }
}
