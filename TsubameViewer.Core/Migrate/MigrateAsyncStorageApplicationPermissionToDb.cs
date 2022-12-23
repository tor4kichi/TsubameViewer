using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Core.Models.SourceFolders;

namespace TsubameViewer.Core.UseCases.Migrate;


public class MigrateAsyncStorageApplicationPermissionToDb : IAsyncMigrater
{
    private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;

    public MigrateAsyncStorageApplicationPermissionToDb(SourceStorageItemsRepository sourceStorageItemsRepository)
    {
        _sourceStorageItemsRepository = sourceStorageItemsRepository;
    }

    public Version TargetVersion { get; } = new Version(1, 2, 5);

    public Task MigrateAsync()
    {
        return _sourceStorageItemsRepository.RefreshTokenToPathDbAsync();
    }
}
