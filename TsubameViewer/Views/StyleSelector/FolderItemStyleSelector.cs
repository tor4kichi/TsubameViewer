using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Core.Models;
using TsubameViewer.ViewModels.PageNavigation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TsubameViewer.Views.StyleSelector
{
    public sealed class FolderItemStyleSelector : Windows.UI.Xaml.Controls.StyleSelector
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
