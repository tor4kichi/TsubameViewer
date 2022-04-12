using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TsubameViewer.Models.Domain.Albam;
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
                CbrFileType,
                CbzFileType,
                SevenZipFileType,
                Cb7FileType,
                TarFileType,                
            }
            .SelectMany(x => new[] { x, x.ToUpper() })
            .ToHashSet();

            SupportedImageFileExtensions = new string[]
            {
                JpgFileType,
                JpegFileType,
                JfifFileType,
                PngFileType,
                BmpFileType,
                GifFileType,
                TifFileType,
                TiffFileType,
                SvgFileType,
                WebpFileType,
                AvifFileType,
                JpegXRFileType,
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
        public const string CbrFileType = ".cbr";
        public const string CbzFileType = ".cbz";
        public const string SevenZipFileType = ".7z";
        public const string Cb7FileType = ".cb7";
        public const string TarFileType = ".tar";
        
        public const string JpgFileType = ".jpg";
        public const string JpegFileType = ".jpeg";
        public const string JfifFileType = ".jfif";
        public const string PngFileType = ".png";
        public const string BmpFileType = ".bmp";
        public const string GifFileType = ".gif";
        public const string TifFileType = ".tif";
        public const string TiffFileType = ".tiff";
        public const string SvgFileType = ".svg";
        public const string WebpFileType = ".webp";
        public const string AvifFileType = ".avif";
        public const string JpegXRFileType = ".jxr";

        public const string EPubFileType = ".epub";


        public static readonly HashSet<string> SupportedArchiveFileExtensions;
        public static readonly HashSet<string> SupportedImageFileExtensions;
        public static readonly HashSet<string> SupportedEBookFileExtensions;

        public static bool IsSupportedImageFile(this StorageFile file)
        {
            return IsSupportedImageFileExtension(file.FileType);
        }

        public static bool IsSupportedMangaOrEBookFile(this StorageFile file)
        {
            return IsSupportedArchiveFileExtension(file.FileType)
                || IsSupportedEBookFileExtension(file.FileType);
        }

        public static bool IsSupportedMangaFile(this StorageFile file)
        {
            return IsSupportedArchiveFileExtension(file.FileType);
        }

        public static bool IsSupportedEBookFile(this StorageFile file)
        {
            return IsSupportedEBookFileExtension(file.FileType);
        }




        public static IEnumerable<string> GetAllSupportedFileExtensions()
        {
            return SupportedArchiveFileExtensions.Concat(SupportedImageFileExtensions).Concat(SupportedEBookFileExtensions);
        }

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

        public static StorageItemTypes StorageItemToStorageItemTypes(object item)
        {
            return item switch
            {
                IStorageItem file => StorageItemToStorageItemTypes(file),
                AlbamEntry _ => StorageItemTypes.Albam,
                _ => StorageItemTypes.None
            };
        }

        public static StorageItemTypes StorageItemToStorageItemTypes(IImageSource item)
        {
            return item switch
            {
                StorageItemImageSource storageItem => storageItem.ItemTypes,
                PdfPageImageSource _ => StorageItemTypes.Image,
                ArchiveEntryImageSource _ => StorageItemTypes.Image,
                ArchiveDirectoryImageSource _ => StorageItemTypes.ArchiveFolder,
                Albam.AlbamImageSource _ => StorageItemTypes.Albam,
                Albam.AlbamItemImageSource source => source.GetAlbamItemType() switch
                {
                    AlbamItemType.Image => StorageItemTypes.AlbamImage,
                    AlbamItemType.FolderOrArchive => StorageItemToStorageItemTypes(source.InnerImageSource),
                    _ => StorageItemTypes.None
                },
                _ => StorageItemTypes.None
            };
        }

        public static StorageItemTypes StorageItemToStorageItemTypesWithFlattenAlbamItem(this IImageSource item)
        {
            return StorageItemToStorageItemTypes(FlattenAlbamItemInnerImageSource(item));
        }

        public static IImageSource FlattenAlbamItemInnerImageSource(this IImageSource imageSource)
        {
            return imageSource is AlbamItemImageSource albam ? albam.InnerImageSource : imageSource;
        }
    }
}
