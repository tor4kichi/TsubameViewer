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

        public bool IsRequireMigrate => SystemInformation.Instance.PreviousVersionInstalled.IsSmallerThen(_targetVersion);

        public Task MigrateAsync()
        {
            return _sourceStorageItemsRepository.RefreshTokenToPathDbAsync();
        }
    }
}
