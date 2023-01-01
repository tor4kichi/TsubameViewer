using LiteDB;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TsubameViewer.Core.UseCases.Migrate;

public sealed class DropIgnoreStorageItemDbWhenIdNotString : IAsyncMigrater
{
    private readonly ILiteDatabase _liteDatabase;

    public DropIgnoreStorageItemDbWhenIdNotString(ILiteDatabase liteDatabase)
    {
        _liteDatabase = liteDatabase;
    }


    public Version? TargetVersion { get; } = new Version(1, 4, 0);

    public ValueTask MigrateAsync()
    {
        try
        {
            if (_liteDatabase.CollectionExists("IgnoreStorageItemEntry"))
            {
                _liteDatabase.DropCollection("IgnoreStorageItemEntry");
            }            
        }
        catch
        {
        }

        return new();
    }
}
