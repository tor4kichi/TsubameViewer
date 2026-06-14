using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using Windows.Storage;

namespace TsubameViewer.Core.Models;

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

        SupportedMovieFileExtensions = new string[]
        {
            Movie_Mp4FileType,
            Movie_WebMFileType,
            Movie_HevcFileType,
            Movie_MkvFileType,
            Movie_MovFileType,
            Movie_TsFileType,
            Movie_MTsFileType,
            Movie_M2TsFileType,
            Movie_AviFileType,
            Movie_WmvFileType,
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

    public const string Movie_Mp4FileType = ".mp4";
    public const string Movie_WebMFileType = ".webm";
    public const string Movie_HevcFileType = ".hevc";
    public const string Movie_MkvFileType = ".mkv";
    public const string Movie_M4vFileType = ".m4v";
    public const string Movie_MovFileType = ".mov";
    public const string Movie_TsFileType = ".ts";
    public const string Movie_MTsFileType = ".mts";
    public const string Movie_M2TsFileType = ".m2ts";
    public const string Movie_AviFileType = ".avi";
    public const string Movie_WmvFileType = ".wmv";

    public static readonly HashSet<string> SupportedArchiveFileExtensions;
    public static readonly HashSet<string> SupportedImageFileExtensions;
    public static readonly HashSet<string> SupportedEBookFileExtensions;
    public static readonly HashSet<string> SupportedMovieFileExtensions;

    public static bool IsSupportedImageFile(this StorageFile file)
    {
        return IsSupportedImageFileExtension(file.FileType);
    }

    public static bool IsSupportedMangaFile(this StorageFile file)
    {
        return IsSupportedArchiveFileExtension(file.FileType);
    }

    public static bool IsSupportedEBookFile(this StorageFile file)
    {
        return IsSupportedEBookFileExtension(file.FileType);
    }

    public static bool IsSupportedMovieFile(this StorageFile file)
    {
        return IsSupportedMovieFileExtension(file.FileType);
    }




    public static IEnumerable<string> GetAllSupportedFileExtensions()
    {
        return [..SupportedArchiveFileExtensions,
            .. SupportedImageFileExtensions,
            .. SupportedEBookFileExtensions,
            .. SupportedMovieFileExtensions,
            ];
    }

    public static bool IsSupportedFileExtension(string fileType)
    {
        return SupportedImageFileExtensions.Contains(fileType) 
            || SupportedArchiveFileExtensions.Contains(fileType)
            || SupportedEBookFileExtensions.Contains(fileType)
            || SupportedMovieFileExtensions.Contains(fileType)
            ;
    }

    public static bool IsSupportedArchiveFileExtension(string fileNameOrExtension)
    {
        if (SupportedArchiveFileExtensions.Contains(fileNameOrExtension)) { return true; }
        else { return SupportedArchiveFileExtensions.Any(x => fileNameOrExtension.EndsWith(x, StringComparison.Ordinal)); }
    }

    public static bool IsSupportedImageFileExtension(string fileNameOrExtension)
    {
        if (SupportedImageFileExtensions.Contains(fileNameOrExtension)) { return true; }
        else { return SupportedImageFileExtensions.Any(x => fileNameOrExtension.EndsWith(x, StringComparison.Ordinal)); }
    }

    public static bool IsSupportedEBookFileExtension(string fileNameOrExtension)
    {
        if (SupportedEBookFileExtensions.Contains(fileNameOrExtension)) { return true; }
        else { return SupportedEBookFileExtensions.Any(x => fileNameOrExtension.EndsWith(x, StringComparison.Ordinal)); }
    }

    public static bool IsSupportedMovieFileExtension(string fileNameOrExtension)
    {
        if (SupportedMovieFileExtensions.Contains(fileNameOrExtension)) { return true; }
        else { return SupportedMovieFileExtensions.Any(x => fileNameOrExtension.EndsWith(x, StringComparison.Ordinal)); }
    }

    private static StorageItemTypes FileExtensionToStorageItemType(string fileType)
    {
        if (IsSupportedArchiveFileExtension(fileType)) { return StorageItemTypes.Archive; }
        else if (IsSupportedImageFileExtension(fileType)) { return StorageItemTypes.Image; }
        else if (IsSupportedEBookFileExtension(fileType)) { return StorageItemTypes.EBook; }
        else if (IsSupportedMovieFileExtension(fileType)) { return StorageItemTypes.Movie; }
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
            AlbamItemImageSource albamItem when SupportedFileTypesHelper.IsSupportedArchiveFileExtension(albamItem.Path) => StorageItemTypes.Archive,
            AlbamItemImageSource albamItem when SupportedFileTypesHelper.IsSupportedEBookFileExtension(albamItem.Path) => StorageItemTypes.EBook,
            AlbamItemImageSource albamItem when SupportedFileTypesHelper.IsSupportedMovieFileExtension(albamItem.Path) => StorageItemTypes.Movie,
            AlbamItemImageSource albamItem when SupportedFileTypesHelper.IsSupportedImageFileExtension(albamItem.Path) => StorageItemTypes.Image,
            AlbamItemImageSource albamItem => StorageItemTypes.Folder,
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
