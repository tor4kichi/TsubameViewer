using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
#if WINDOWS_UWP || WINDOWS
using Windows.Storage.Search;
#endif

namespace TsubameViewer.Models.Domain
{
    public static class FolderHelper
    {
        public static async ValueTask<IStorageItem> GetFolderItemFromPath(StorageFolder parent, string subtractPath)
        {
            if (string.IsNullOrEmpty(subtractPath) || (subtractPath.Length == 1 && Path.DirectorySeparatorChar == subtractPath[0]))
            {
                return parent;
            }

            var folderDescendantsNames = subtractPath.Split(Path.DirectorySeparatorChar);
            StorageFolder currentFolder = parent;
            foreach (var descendantName in folderDescendantsNames.Skip(1).SkipLast(1))
            {
                var child = await currentFolder.GetFolderAsync(descendantName);
                if (child == null)
                {
                    throw new Exception("Folder not found. " + Path.Combine(parent.Path, subtractPath));
                }
                currentFolder = child;
            }

            var lastDescendantName = folderDescendantsNames.Last();
            return await currentFolder.GetItemAsync(lastDescendantName);
        }


#if WINDOWS_UWP || WINDOWS

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


    }
}
