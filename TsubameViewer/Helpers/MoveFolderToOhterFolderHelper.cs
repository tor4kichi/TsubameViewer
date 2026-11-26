using CommunityToolkit.Diagnostics;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Search;

namespace TsubameViewer.Helpers;

internal static class MoveFolderToOhterFolderHelper
{
    private const uint BatchSize = 1000;

    public static bool CanMove(StorageFolder sourceFolder, StorageFolder destFolder)
    {
        Guard.IsNotNull(sourceFolder);
        Guard.IsNotNull(destFolder);

        var sourcePath = Path.GetFullPath(sourceFolder.Path);
        var destPath = Path.GetFullPath(destFolder.Path);
        var targetPath = Path.GetFullPath(Path.Combine(destPath, sourceFolder.Name));

        // 移動先自体、または移動先に作る同名フォルダーが移動元と同じなら、自己移動になる。
        return !string.Equals(sourcePath, destPath, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase)
            && !IsDescendantPath(sourcePath, destPath);
    }

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
        Guard.IsNotNull(sourceFolder);
        Guard.IsNotNull(destFolder);

        cancellationToken.ThrowIfCancellationRequested();
        if (!CanMove(sourceFolder, destFolder))
        {
            return;
        }

        var movedFolder = await destFolder
            .CreateFolderAsync(sourceFolder.Name, repDirOpt)
            .AsTask(cancellationToken);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Note: 各アイテムの移動でソース一覧が縮むため startIndex は 0 でよい。BatchSize で分割して移動する。
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
        if ((await sourceFolder.GetItemsAsync(0, 1).AsTask(cancellationToken)).Count != 0)
        {
            throw new IOException($"The source folder '{sourceFolder.Path}' still contains items after the move.");
        }

        await sourceFolder
            .DeleteAsync(StorageDeleteOption.PermanentDelete)
            .AsTask(cancellationToken);
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
