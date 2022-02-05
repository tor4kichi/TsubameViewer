using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace TsubameViewer.Presentation.Views.StyleSelector
{
    public sealed class FolderItemStyleSelector : Microsoft.UI.Xaml.Controls.StyleSelector
    {
        public Style AddNewFolder { get; set; }
        public Style Folder { get; set; }
        public Style Image { get; set; }
        public Style Archive { get; set; }
        public Style Albam { get; set; }
        public Style AlbamImage { get; set; }
        public Style EBook { get; set; }


        protected override Style SelectStyleCore(object item, DependencyObject container)
        {
            if (item is StorageItemViewModel itemVM)
            {
                return itemVM.Type switch
                {
                    StorageItemTypes.AddFolder => AddNewFolder,
                    StorageItemTypes.AddAlbam => AddNewFolder,
                    StorageItemTypes.Folder => Folder,
                    StorageItemTypes.Image => Image,
                    StorageItemTypes.Archive => Archive,
                    StorageItemTypes.Albam => Albam,
                    StorageItemTypes.AlbamImage => AlbamImage,
                    StorageItemTypes.EBook => EBook,
                    _ => throw new NotSupportedException()
                };
            }
            return base.SelectStyleCore(item, container);
        }
    }
}
