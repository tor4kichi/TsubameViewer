using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Core.Contracts.Services;
using TsubameViewer.Core.Models;

namespace TsubameViewer.Core.UseCases.Migrate
{
    public sealed class MigrateLocalStorageHelperToApplicationDataStorageHelper : IAsyncMigrater
    {
        public MigrateLocalStorageHelperToApplicationDataStorageHelper(IStorageHelper storageHelper)
        {
            _storageHelper = storageHelper;
        }

        public  Version? TargetVersion { get; } = new Version(1, 3, 5);
        private readonly IStorageHelper _storageHelper;

        public async ValueTask MigrateAsync()
        {
            _storageHelper.Clear();

            await _storageHelper.TryDeleteItemAsync(NavigationStackRepository.NavigationStackRepository_Internal.BackNavigationEntriesName);
            await _storageHelper.TryDeleteItemAsync(NavigationStackRepository.NavigationStackRepository_Internal.ForwardNavigationEntriesName);
        }
    }
}
