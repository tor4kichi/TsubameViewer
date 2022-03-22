using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain.Albam;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;

namespace TsubameViewer.Models.Domain.Albam
{
    public static class AlbamItemExtensions
    {
        public static AlbamItemType GetAlbamItemType(this IImageSource imageSource)
        {
            return imageSource switch
            {
                StorageItemImageSource storageItem => storageItem.ItemTypes switch
                {
                    Models.Domain.StorageItemTypes.None => throw new NotSupportedException(),
                    Models.Domain.StorageItemTypes.Folder => AlbamItemType.FolderOrArchive,
                    Models.Domain.StorageItemTypes.Image => AlbamItemType.Image,
                    Models.Domain.StorageItemTypes.Archive => AlbamItemType.FolderOrArchive,
                    Models.Domain.StorageItemTypes.ArchiveFolder => AlbamItemType.FolderOrArchive,
                    Models.Domain.StorageItemTypes.EBook => AlbamItemType.FolderOrArchive,
                    Models.Domain.StorageItemTypes.AddAlbam => throw new NotSupportedException(),
                    _ => throw new NotSupportedException()
                },
                AlbamItemImageSource albamItem => albamItem.InnerImageSource.GetAlbamItemType(),
                _ => AlbamItemType.Image
            };
        }
    }
}
