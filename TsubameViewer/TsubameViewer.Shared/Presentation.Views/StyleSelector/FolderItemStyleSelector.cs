using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.UseCase.PageNavigation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace TsubameViewer.Presentation.Views.StyleSelector
{
    public sealed class FolderItemStyleSelector : Windows.UI.Xaml.Controls.StyleSelector
    {
        public Style AddNewFolder { get; set; }
        public Style Folder { get; set; }
        public Style Image { get; set; }
        public Style Archive { get; set; }

        protected override Style SelectStyleCore(object item, DependencyObject container)
        {
            if (item is StorageItemViewModel itemVM)
            {
                return itemVM.Type switch
                {
                    StorageItemTypes.None => AddNewFolder,
                    StorageItemTypes.Folder => Folder,
                    StorageItemTypes.Image => Image,
                    StorageItemTypes.Archive => Archive,
                    _ => throw new NotSupportedException()
                };
            }
            return base.SelectStyleCore(item, container);
        }
    }
}
