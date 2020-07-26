using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Windows.Storage;

namespace TsubameViewer.Models.Domain
{
    public static class SupportedFileTypesHelper
    {
        static SupportedFileTypesHelper()
        {
            SupportedArchiveFileExtensions = new string[]
            {
                ZipFileType,
                RarFileType,
                PdfFileType,
            }
            .SelectMany(x => new[] { x, x.ToUpper() })
            .ToHashSet();

            SupportedImageFileExtensions = new string[]
            {
                JpgFileType,
                PngFileType,
            }
            .SelectMany(x => new[] { x, x.ToUpper() })
            .ToHashSet();
        }

        public const string ZipFileType = ".zip";
        public const string RarFileType = ".rar";
        public const string PdfFileType = ".pdf";

        public const string JpgFileType = ".jpg";
        public const string PngFileType = ".png";

        public static readonly HashSet<string> SupportedArchiveFileExtensions;
        public static readonly HashSet<string> SupportedImageFileExtensions;

        public static bool IsSupportedFileExtension(string fileType)
        {
            return SupportedImageFileExtensions.Contains(fileType) || SupportedArchiveFileExtensions.Contains(fileType);
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
