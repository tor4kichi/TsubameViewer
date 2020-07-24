using System;
using System.Collections.Generic;
using System.Text;
using Windows.Storage;

namespace TsubameViewer.Models.Domain.FolderItemListing
{
    public static class PresentedFileTypesHelper
    {
        public static readonly HashSet<string> SupportedFileExtensions = new HashSet<string>
        {
            ".png", ".jpg",
            ".zip"
        };

        public static bool IsSupportedFileExtension(string fileType)
        {
            return SupportedFileExtensions.Contains(fileType);
        }

        public static StorageItemTypes StorageItemToStorageItemTypes(IStorageItem item)
        {
            return item switch
            {
                StorageFile file => file.FileType switch
                {
                    ".jpg" => StorageItemTypes.File,
                    ".png" => StorageItemTypes.File,
                    ".zip" => StorageItemTypes.Archive,
                    _ => StorageItemTypes.None,
                },
                StorageFolder _ => StorageItemTypes.Folder,
                _ => StorageItemTypes.None
            };
        }
    }
}
