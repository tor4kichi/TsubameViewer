using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Core.Contracts.Services;

using static TsubameViewer.Core.Services.RestoreNavigationService;

namespace TsubameViewer.Core.UseCases.Migrate
{
    public sealed class MigrateLocalStorageHelperToApplicationDataStorageHelper : IAsyncMigrater
    {
        public MigrateLocalStorageHelperToApplicationDataStorageHelper(IStorageHelper storageHelper)
        {
            _storageHelper = storageHelper;
        }

        public  Version TargetVersion { get; } = new Version(1, 3, 5);
        private readonly IStorageHelper _storageHelper;

        async Task IAsyncMigrater.MigrateAsync()
        {
            _storageHelper.Clear();

            await _storageHelper.TryDeleteItemAsync(NavigationStackRepository.BackNavigationEntriesName);
            await _storageHelper.TryDeleteItemAsync(NavigationStackRepository.ForwardNavigationEntriesName);
        }
    }
}
