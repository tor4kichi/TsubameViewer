using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;

namespace TsubameViewer.Core.Models.Albam;

public static class AlbamItemExtensions
{
    public static AlbamItemType GetAlbamItemType(this IImageSource imageSource)
    {
        return imageSource switch
        {
            StorageItemImageSource storageItem => storageItem.ItemTypes switch
            {
                StorageItemTypes.None => throw new NotSupportedException(),
                StorageItemTypes.Folder => AlbamItemType.FolderOrArchive,
                StorageItemTypes.Image => AlbamItemType.Image,
                StorageItemTypes.Archive => AlbamItemType.FolderOrArchive,
                StorageItemTypes.ArchiveFolder => AlbamItemType.FolderOrArchive,
                StorageItemTypes.EBook => AlbamItemType.FolderOrArchive,
                StorageItemTypes.AddAlbam => throw new NotSupportedException(),
                _ => throw new NotSupportedException()
            },
            AlbamItemImageSource albamItem => albamItem.InnerImageSource.GetAlbamItemType(),
            _ => AlbamItemType.Image
        };
    }
}
