using Microsoft.Toolkit.Uwp.Helpers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Core.Infrastructure;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using static TsubameViewer.Core.Services.RestoreNavigationService;

namespace TsubameViewer.Core.UseCases.Migrate
{
    public interface IMigrater 
    {
        bool IsRequireMigrate { get; }
        void Migrate();
    }
    public sealed class MigrateLocalStorageHelperToApplicationDataStorageHelper : IAsyncMigrater
    {
        private readonly PackageVersion _targetVersion = new PackageVersion() { Major = 1, Minor = 3, Build = 5 };

        bool IAsyncMigrater.IsRequireMigrate =>
            SystemInformation.Instance.IsAppUpdated
            && SystemInformation.Instance.PreviousVersionInstalled.IsSmallerThen(_targetVersion)    
            ;
        async Task IAsyncMigrater.MigrateAsync()
        {
            var strorageHelper = BytesApplicationDataStorageHelper.GetCurrent(objectSerializer: new BinaryJsonObjectSerializer());
            strorageHelper.Clear();

            await strorageHelper.TryDeleteItemAsync(NavigationStackRepository.BackNavigationEntriesName);
            await strorageHelper.TryDeleteItemAsync(NavigationStackRepository.ForwardNavigationEntriesName);
        }
    }
}
