using LiteDB;
using Microsoft.Toolkit.Uwp.Helpers;
using System;
using System.Collections.Generic;
using System.Text;

namespace TsubameViewer.Models.UseCase.Migrate
{
    internal sealed class DropSearchIndexDb : IMigrater
    {
        private readonly ILiteDatabase _liteDatabase;

        const string RemovedSearchIndexCollectionName = "StorageItemSearchEntry";

        public DropSearchIndexDb(ILiteDatabase liteDatabase)
        {
            _liteDatabase = liteDatabase;
        }
        public bool IsRequireMigrate => (SystemInformation.Instance.IsFirstRun || SystemInformation.Instance.IsAppUpdated) 
            && _liteDatabase.CollectionExists(RemovedSearchIndexCollectionName);

        public void Migrate()
        {
            _liteDatabase.DropCollection(RemovedSearchIndexCollectionName);
        }
    }
}
