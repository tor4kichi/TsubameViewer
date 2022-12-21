using LiteDB;
using Microsoft.Toolkit.Uwp.Helpers;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Core.Models.SourceFolders;
using Windows.ApplicationModel;

namespace TsubameViewer.Core.UseCases.Migrate
{
    public sealed class DropIgnoreStorageItemDbWhenIdNotString : IMigrater
    {
        private readonly ILiteDatabase _liteDatabase;

        public DropIgnoreStorageItemDbWhenIdNotString(ILiteDatabase liteDatabase)
        {
            _liteDatabase = liteDatabase;
        }

        PackageVersion _targetVersion = new PackageVersion() { Major = 1, Minor = 4, Build = 0 };

        public bool IsRequireMigrate => SystemInformation.Instance.PreviousVersionInstalled.IsSmallerThen(_targetVersion)
            && _liteDatabase.CollectionExists(nameof(IgnoreStorageItemEntry))            
            ;

        void IMigrater.Migrate()
        {
            try
            {
                _liteDatabase.DropCollection(nameof(IgnoreStorageItemEntry));
            }
            catch
            {
            }
        }
    }
}
