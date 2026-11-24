using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Search;

namespace TsubameViewer.Helpers;

internal static class MoveFolderToOhterFolderHelper
{
    private const uint BatchSize = 1000;

    public static Task MoveAsync(
        this StorageFolder sourceFolder,
        StorageFolder destFolder,
        CreationCollisionOption repDirOpt,
        NameCollisionOption repFilesOpt)
    {
        return MoveAsync(sourceFolder, destFolder, repDirOpt, repFilesOpt, CancellationToken.None);
    }

    public static async Task MoveAsync(
        this StorageFolder sourceFolder,
        StorageFolder destFolder,
        CreationCollisionOption repDirOpt,
        NameCollisionOption repFilesOpt,
        CancellationToken cancellationToken)
    {
        if (sourceFolder == null) { throw new ArgumentNullException(nameof(sourceFolder)); }
        if (destFolder == null) { throw new ArgumentNullException(nameof(destFolder)); }
        cancellationToken.ThrowIfCancellationRequested();

        var sourcePath = sourceFolder.Path;
        var destPath = destFolder.Path;

        if (!string.IsNullOrEmpty(sourcePath)
            && !string.IsNullOrEmpty(destPath)
            && sourcePath.Equals(destPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!string.IsNullOrEmpty(sourcePath)
            && !string.IsNullOrEmpty(destPath)
            && IsDescendantPath(sourcePath, destPath))
        {
            throw new InvalidOperationException("The destination folder cannot be inside the source folder.");
        }

        var movedFolder = await destFolder
            .CreateFolderAsync(sourceFolder.Name, repDirOpt)
            .AsTask(cancellationToken);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var folders = await sourceFolder
                .GetFoldersAsync(CommonFolderQuery.DefaultQuery, 0, BatchSize)
                .AsTask(cancellationToken);

            if (folders.Count == 0)
            {
                break;
            }

            foreach (var folder in folders)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await MoveAsync(folder, movedFolder, repDirOpt, repFilesOpt, cancellationToken);
            }

            if (folders.Count < BatchSize)
            {
                break;
            }
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var files = await sourceFolder
                .GetFilesAsync(CommonFileQuery.DefaultQuery, 0, BatchSize)
                .AsTask(cancellationToken);

            if (files.Count == 0)
            {
                break;
            }

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await file
                    .MoveAsync(movedFolder, file.Name, repFilesOpt)
                    .AsTask(cancellationToken);
            }

            if (files.Count < BatchSize)
            {
                break;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        if ((await sourceFolder.GetItemsAsync(0, 1).AsTask(cancellationToken)).Count == 0)
        {
            await sourceFolder
                .DeleteAsync(StorageDeleteOption.PermanentDelete)
                .AsTask(cancellationToken);
        }
    }

    private static bool IsDescendantPath(string sourcePath, string destPath)
    {
        if (!destPath.StartsWith(sourcePath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return destPath.Length == sourcePath.Length
            || destPath[sourcePath.Length] is '\\' or '/';
    }
}
