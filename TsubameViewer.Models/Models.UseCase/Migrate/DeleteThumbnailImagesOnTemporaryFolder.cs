using Microsoft.Toolkit.Uwp.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;

namespace TsubameViewer.Models.UseCase.Migrate
{
    public class DeleteThumbnailImagesOnTemporaryFolder : IAsyncMigrater
    {
        PackageVersion _targetVersion = new PackageVersion() { Major = 1, Minor = 5, Build = 1 };

        public bool IsRequireMigrate => SystemInformation.Instance.PreviousVersionInstalled.IsSmallerThen(_targetVersion);


        public async Task MigrateAsync()
        {
            var query = ApplicationData.Current.TemporaryFolder.CreateFileQuery();

            await Task.Run(async () => 
            {
                while (await query.GetFilesAsync() is not null and var files && files.Any())
                {
                    foreach (var file in files)
                    {
                        try
                        {
                            await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                        }
                        catch { }
                    }
                }
            });
        }
    }
}
