using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace TsubameViewer.Presentation.Views.TemplateSelector
{
    public sealed class FolderItemTemplateSelector : DataTemplateSelector
    {
        public Microsoft.UI.Xaml.DataTemplate AddNewFolder { get; set; }
        public Microsoft.UI.Xaml.DataTemplate Folder { get; set; }
        public Microsoft.UI.Xaml.DataTemplate Image { get; set; }
        public Microsoft.UI.Xaml.DataTemplate Archive { get; set; }
        public Microsoft.UI.Xaml.DataTemplate ArchiveFolder { get; set; }
        public Microsoft.UI.Xaml.DataTemplate Albam { get; set; }
        public Microsoft.UI.Xaml.DataTemplate AlbamImage { get; set; }
        public Microsoft.UI.Xaml.DataTemplate EBook { get; set; }

        protected override Microsoft.UI.Xaml.DataTemplate SelectTemplateCore(object item)
        {
            return SelectTemplateCore(item, null);
        }
        protected override Microsoft.UI.Xaml.DataTemplate SelectTemplateCore(object item, DependencyObject container)
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
                    StorageItemTypes.ArchiveFolder => ArchiveFolder,
                    StorageItemTypes.Albam => Albam,
                    StorageItemTypes.AlbamImage => AlbamImage,
                    StorageItemTypes.EBook => EBook,
                    _ => throw new NotSupportedException()
                };
            }
            
            return base.SelectTemplateCore(item, container);
        }
    }
}
