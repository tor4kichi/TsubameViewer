using LiteDB;
using System;
using System.Collections.Generic;
using System.Text;

namespace TsubameViewer.Core.UseCases.Migrate;

public sealed class DropIgnoreStorageItemDbWhenIdNotString : IMigrater
{
    private readonly ILiteDatabase _liteDatabase;

    public DropIgnoreStorageItemDbWhenIdNotString(ILiteDatabase liteDatabase)
    {
        _liteDatabase = liteDatabase;
    }


    public Version TargetVersion { get; } = new Version(1, 4, 0);

    void IMigrater.Migrate()
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
    }
}
