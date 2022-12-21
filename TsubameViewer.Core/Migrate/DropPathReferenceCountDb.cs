using LiteDB;
using Microsoft.Toolkit.Uwp.Helpers;
using System;
using System.Collections.Generic;
using System.Text;

namespace TsubameViewer.Core.UseCases.Migrate
{
    public sealed class DropPathReferenceCountDb : IMigrater
    {
        private readonly ILiteDatabase _liteDatabase;

        const string RemovedSearchIndexCollectionName = "PathReferenceCountEntry";

        public DropPathReferenceCountDb(ILiteDatabase liteDatabase)
        {
            _liteDatabase = liteDatabase;
        }
        public bool IsRequireMigrate => _liteDatabase.CollectionExists(RemovedSearchIndexCollectionName);

        public void Migrate()
        {
            _liteDatabase.DropCollection(RemovedSearchIndexCollectionName);
        }
    }
}
