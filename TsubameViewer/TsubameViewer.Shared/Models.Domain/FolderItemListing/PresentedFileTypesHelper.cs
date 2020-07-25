using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Windows.Storage;

namespace TsubameViewer.Models.Domain.FolderItemListing
{
    public static class PresentedFileTypesHelper
    {
        static PresentedFileTypesHelper()
        {
            SupportedFileExtensions = SupportedArchiveFileExtensions.Concat(SupportedImageFileExtensions).ToHashSet();
        }

        public static readonly HashSet<string> SupportedFileExtensions;


        public static readonly HashSet<string> SupportedArchiveFileExtensions = new HashSet<string>
        {
            ".zip", ".rar",
        };

        public static readonly HashSet<string> SupportedImageFileExtensions = new HashSet<string>
        {
            ".png", ".jpg",
        };

        public static bool IsSupportedFileExtension(string fileType)
        {
            return SupportedFileExtensions.Contains(fileType);
        }

        public static bool IsSupportedArchiveFileExtension(string fileType)
        {
            return SupportedArchiveFileExtensions.Contains(fileType);
        }

        public static bool IsSupportedImageFileExtension(string fileNameOrExtension)
        {
            if (SupportedImageFileExtensions.Contains(fileNameOrExtension)) { return true; }
            else { return SupportedImageFileExtensions.Any(x => fileNameOrExtension.EndsWith(x)); }
        }

        private static StorageItemTypes FileExtensionToStorageItemType(string fileType)
        {
            if (IsSupportedArchiveFileExtension(fileType)) { return StorageItemTypes.Archive; }
            else if (IsSupportedImageFileExtension(fileType)) { return StorageItemTypes.Image; }
            else { return StorageItemTypes.None; }
        }

        public static StorageItemTypes StorageItemToStorageItemTypes(IStorageItem item)
        {
            return item switch
            {
                StorageFile file => FileExtensionToStorageItemType(file.FileType),
                StorageFolder _ => StorageItemTypes.Folder,
                _ => StorageItemTypes.None
            };
        }
    }
}
