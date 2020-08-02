using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TsubameViewer.Presentation.Views.TemplateSelector
{
    public sealed class FolderItemTemplateSelector : DataTemplateSelector
    {
        public Windows.UI.Xaml.DataTemplate AddNewFolder { get; set; }
        public Windows.UI.Xaml.DataTemplate Folder { get; set; }
        public Windows.UI.Xaml.DataTemplate Image { get; set; }
        public Windows.UI.Xaml.DataTemplate Archive { get; set; }
        public Windows.UI.Xaml.DataTemplate EBook { get; set; }

        protected override Windows.UI.Xaml.DataTemplate SelectTemplateCore(object item)
        {
            return SelectTemplateCore(item, null);
        }
        protected override Windows.UI.Xaml.DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is StorageItemViewModel itemVM)
            {
                return itemVM.Type switch
                {
                    StorageItemTypes.None => AddNewFolder,
                    StorageItemTypes.Folder => Folder,
                    StorageItemTypes.Image => Image,
                    StorageItemTypes.Archive => Archive,
                    StorageItemTypes.EBook => EBook,
                    _ => throw new NotSupportedException()
                };
            }
            
            return base.SelectTemplateCore(item, container);
        }
    }
}
