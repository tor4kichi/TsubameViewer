using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
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
                JpegFileType,
                PngFileType,
                BmpFileType,
                GifFileType,
                TifFileType,
                TiffFileType,
                SvgFileType,
            }
            .SelectMany(x => new[] { x, x.ToUpper() })
            .ToHashSet();

            SupportedEBookFileExtensions = new string[]
            {
                EPubFileType,
            }
            .SelectMany(x => new[] { x, x.ToUpper() })
            .ToHashSet();
        }

        public const string ZipFileType = ".zip";
        public const string RarFileType = ".rar";
        public const string PdfFileType = ".pdf";

        public const string JpgFileType = ".jpg";
        public const string JpegFileType = ".jpeg";
        public const string PngFileType = ".png";
        public const string BmpFileType = ".bmp";
        public const string GifFileType = ".gif";
        public const string TifFileType = ".tif";
        public const string TiffFileType = ".tiff";
        public const string SvgFileType = ".svg";

        public const string EPubFileType = ".epub";


        public static readonly HashSet<string> SupportedArchiveFileExtensions;
        public static readonly HashSet<string> SupportedImageFileExtensions;
        public static readonly HashSet<string> SupportedEBookFileExtensions;

        public static bool IsSupportedFileExtension(string fileType)
        {
            return SupportedImageFileExtensions.Contains(fileType) 
                || SupportedArchiveFileExtensions.Contains(fileType)
                || SupportedEBookFileExtensions.Contains(fileType)
                ;
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

        public static bool IsSupportedEBookFileExtension(string fileNameOrExtension)
        {
            if (SupportedEBookFileExtensions.Contains(fileNameOrExtension)) { return true; }
            else { return SupportedEBookFileExtensions.Any(x => fileNameOrExtension.EndsWith(x)); }
        }

        private static StorageItemTypes FileExtensionToStorageItemType(string fileType)
        {
            if (IsSupportedArchiveFileExtension(fileType)) { return StorageItemTypes.Archive; }
            else if (IsSupportedImageFileExtension(fileType)) { return StorageItemTypes.Image; }
            else if (IsSupportedEBookFileExtension(fileType)) { return StorageItemTypes.EBook; }
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

        public static StorageItemTypes StorageItemToStorageItemTypes(IImageSource item)
        {
            return item switch
            {
                StorageItemImageSource storageItem => storageItem.ItemTypes,
                PdfPageImageSource _ => StorageItemTypes.Image,
                ZipArchiveEntryImageSource _ => StorageItemTypes.Image,
                RarArchiveEntryImageSource _ => StorageItemTypes.Image,
                _ => StorageItemTypes.None
            };
        }

    }
}
