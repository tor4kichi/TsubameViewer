using System;
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

namespace TsubameViewer.Presentation.ViewModels
{
    public static class FolderHelper
    {
        public static async ValueTask<IStorageItem> GetFolderItemFromPath(StorageFolder parent, string path)
        {
            if (string.IsNullOrEmpty(path) || (path.Length == 1 && Path.DirectorySeparatorChar == path[0]))
            {
                return parent;
            }

            var folderDescendantsNames = path.Split(Path.DirectorySeparatorChar);
            StorageFolder currentFolder = parent;
            foreach (var descendantName in folderDescendantsNames.Skip(1).SkipLast(1))
            {
                var child = await currentFolder.GetFolderAsync(descendantName);
                if (child == null)
                {
                    throw new Exception("Folder not found. " + Path.Combine(parent.Path, path));
                }
                currentFolder = child;
            }

            var lastDescendantName = folderDescendantsNames.Last();
            return await currentFolder.GetItemAsync(lastDescendantName);
        }


#if WINDOWS_UWP

        public static uint GetEnumeratorOneTimeGetCount = 20;
        public static async IAsyncEnumerable<IStorageItem> GetEnumerator(StorageItemQueryResult query, uint itemsCount, [EnumeratorCancellation] CancellationToken ct = default)
        {
            uint currentCount = 0;
            while (currentCount < itemsCount)
            {
                ct.ThrowIfCancellationRequested();

                var items = await query.GetItemsAsync(currentCount, GetEnumeratorOneTimeGetCount).AsTask(ct);
                foreach (var item in items)
                {
                    yield return item;
                }

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
