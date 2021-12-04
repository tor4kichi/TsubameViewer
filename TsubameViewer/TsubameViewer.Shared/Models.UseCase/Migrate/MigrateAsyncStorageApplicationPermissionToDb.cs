using Microsoft.Toolkit.Uwp.Helpers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain.SourceFolders;
using Windows.ApplicationModel;

namespace TsubameViewer.Models.UseCase.Migrate
{
    internal interface IAsyncMigrater
    {
        bool IsRequireMigrate { get; }
        Task MigrateAsync();
    }

    internal class MigrateAsyncStorageApplicationPermissionToDb : IAsyncMigrater
    {
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;

        public MigrateAsyncStorageApplicationPermissionToDb(SourceStorageItemsRepository sourceStorageItemsRepository)
        {
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
        }

        PackageVersion _targetVersion = new PackageVersion() { Major = 1, Minor = 2, Build = 5 };

        public bool IsRequireMigrate
        {
            get
            {
                if (SystemInformation.Instance.IsAppUpdated is false) { return false; }

                var prevVersion = SystemInformation.Instance.PreviousVersionInstalled;
                if (_targetVersion.Major > prevVersion.Major)
                {
                    return true;
                }
                if (_targetVersion.Major == prevVersion.Major 
                    && _targetVersion.Minor > prevVersion.Minor)
                {
                    return true;
                }
                if (_targetVersion.Major == prevVersion.Major
                    && _targetVersion.Minor== prevVersion.Minor
                    && _targetVersion.Build >= prevVersion.Build)
                {
                    return true;
                }

                return false;
            }
        }

        public Task MigrateAsync()
        {
            return _sourceStorageItemsRepository.RefreshTokenToPathDbAsync();
        }
    }
}
