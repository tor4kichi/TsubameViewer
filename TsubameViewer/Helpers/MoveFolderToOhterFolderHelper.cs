using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace TsubameViewer.Helpers;

internal static class MoveFolderToOhterFolderHelper
{
    // this code is copy from
    // https://stackoverflow.com/questions/54942686/fastest-way-to-move-folder-to-another-place-in-uwp
    public static async Task MoveAsync(
        this StorageFolder sourceFolder,
        StorageFolder destFolder,
        CreationCollisionOption repDirOpt,
        NameCollisionOption repFilesOpt)
    {
        try
        {
            if (sourceFolder == null)
                return;

            List<Task> copies = new List<Task>();
            var files = await sourceFolder.GetFilesAsync();
            if (files == null || files.Count == 0)
                await destFolder.CreateFolderAsync(sourceFolder.Name, CreationCollisionOption.OpenIfExists);
            else
            {
                await destFolder.CreateFolderAsync(sourceFolder.Name, repDirOpt);
                foreach (var file in files)
                    copies.Add(file.CopyAsync(destFolder, file.Name, repFilesOpt).AsTask());
            }

            await Task.WhenAll(copies);
            await sourceFolder.DeleteAsync(StorageDeleteOption.PermanentDelete);
        }
        catch (Exception ex)
        {
            //Handle any needed cleanup tasks here
            throw new Exception(
              $"A fatal exception triggered within Move_Directory_Async:\r\n{ex.Message}", ex);
        }
    }
}
