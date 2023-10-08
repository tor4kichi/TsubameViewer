﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
#if WINDOWS_UWP
using Windows.Storage.Search;
#endif

namespace TsubameViewer.Core;

public static class FolderHelper
{
    public static async ValueTask<IStorageItem> GetFolderItemFromPath(StorageFolder parent, string subtractPath)
    {
        if (string.IsNullOrEmpty(subtractPath) || (subtractPath.Length == 1 && Path.DirectorySeparatorChar == subtractPath[0]))
        {
            return parent;
        }

        var folderDescendantsNames = subtractPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        StorageFolder currentFolder = parent;
        foreach (var descendantName in folderDescendantsNames.SkipLast(1))
        {
            var child = await currentFolder.TryGetItemAsync(descendantName).AsTask() as StorageFolder;
            if (child == null)
            {
                //throw new Exception("Folder not found. " + Path.Combine(parent.Path, subtractPath));
                return null;
            }
            currentFolder = child;
        }

        var lastDescendantName = folderDescendantsNames.Last();
        return await currentFolder.TryGetItemAsync(lastDescendantName).AsTask();
    }


#if WINDOWS_UWP

    public static uint GetEnumeratorOneTimeGetCount = 100;
    public static async IAsyncEnumerable<IStorageItem> ToAsyncEnumerable(this StorageItemQueryResult query, [EnumeratorCancellation] CancellationToken ct = default)
    {
        uint currentCount = 0;
        while (await query.GetItemsAsync(currentCount, GetEnumeratorOneTimeGetCount).AsTask(ct) is not null and var items && items.Any())
        {
            foreach (var item in items)
            {
                ct.ThrowIfCancellationRequested();
                yield return item;
            }

            ct.ThrowIfCancellationRequested();

            currentCount += (uint)items.Count;
        }
    }

    public static async IAsyncEnumerable<StorageFile> ToAsyncEnumerable(this StorageFileQueryResult query, [EnumeratorCancellation] CancellationToken ct = default)
    {
        uint currentCount = 0;
        while (await query.GetFilesAsync(currentCount, GetEnumeratorOneTimeGetCount).AsTask(ct) is not null and var items && items.Any())
        {
            foreach (var item in items)
            {
                ct.ThrowIfCancellationRequested();
                yield return item;
            }

            ct.ThrowIfCancellationRequested();

            currentCount += (uint)items.Count;
        }
    }

    public static async IAsyncEnumerable<StorageFolder> ToAsyncEnumerable(this StorageFolderQueryResult query, [EnumeratorCancellation] CancellationToken ct = default)
    {
        uint currentCount = 0;
        while (await query.GetFoldersAsync(currentCount, GetEnumeratorOneTimeGetCount).AsTask(ct) is not null and var items && items.Any())
        {
            foreach (var item in items)
            {
                ct.ThrowIfCancellationRequested();
                yield return item;
            }

            ct.ThrowIfCancellationRequested();

            currentCount += (uint)items.Count;
        }
    }
#else
    public static async IAsyncEnumerable<IStorageItem> GetEnumerator(StorageFolder folder, IEnumerable<string> items, [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var fileName in items)
        {
            ct.ThrowIfCancellationRequested();
            yield return await folder.GetFileAsync(Path.Combine(folder.Path, fileName));
        }
    }
#endif

    public static async Task<StorageFile> DigStorageFileFromPathAsync(this StorageFolder parentFolder, string relativePath, CreationCollisionOption fileCollitionOption, CancellationToken ct)
    {
        var pathItems = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        StorageFolder targetFolder = parentFolder;
        foreach (var pathName in pathItems.SkipLast(1))
        {
            targetFolder = await targetFolder.CreateFolderAsync(pathName, CreationCollisionOption.OpenIfExists);
        }

        // Note: ここで System.UnauthorizedAccessException が出る
        return await targetFolder.CreateFileAsync(pathItems.Last(), fileCollitionOption);
    }
}
