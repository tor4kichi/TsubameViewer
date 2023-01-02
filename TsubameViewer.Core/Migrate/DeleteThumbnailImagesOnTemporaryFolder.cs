using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace TsubameViewer.Core.Models.Migrate;

public class DeleteThumbnailImagesOnTemporaryFolder : IAsyncMigrater
{
    public Version? TargetVersion { get; } = new Version(1, 5, 1);

    public async ValueTask MigrateAsync()
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
