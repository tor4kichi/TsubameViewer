using Microsoft.Toolkit.Uwp.Helpers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Core.Models.SourceFolders;
using Windows.ApplicationModel;

namespace TsubameViewer.Core.UseCases.Migrate
{
    public interface IAsyncMigrater
    {
        bool IsRequireMigrate { get; }
        Task MigrateAsync();
    }

    public class MigrateAsyncStorageApplicationPermissionToDb : IAsyncMigrater
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
