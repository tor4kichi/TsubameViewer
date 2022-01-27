using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// ユーザー コントロールの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234236 を参照してください

namespace TsubameViewer.Presentation.Views.FolderListup
{
    public sealed partial class FolderListupItemTemplate : ResourceDictionary
    {
        public FolderListupItemTemplate()
        {
            this.InitializeComponent();
        }
    }

    public sealed class StorageItemIconTemplateSelector : DataTemplateSelector
    {
        public DataTemplate FolderIcon { get; set; }
        public DataTemplate ArchiveIcon { get; set; }
        public DataTemplate ArchiveFolderIcon { get; set; }
        public DataTemplate AlbamIcon { get; set; }
        public DataTemplate AlbamImageIcon { get; set; }
        public DataTemplate EBookIcon { get; set; }
        public DataTemplate ImageIcon { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item == null) { return base.SelectTemplateCore(item, container); }

            return (item as StorageItemViewModel).Type switch
            {
                Models.Domain.StorageItemTypes.Folder => FolderIcon,
                Models.Domain.StorageItemTypes.Archive => ArchiveIcon,
                Models.Domain.StorageItemTypes.ArchiveFolder => ArchiveFolderIcon,
                Models.Domain.StorageItemTypes.Albam => AlbamIcon,
                Models.Domain.StorageItemTypes.AlbamImage => AlbamImageIcon,
                Models.Domain.StorageItemTypes.EBook => EBookIcon,
                Models.Domain.StorageItemTypes.Image => ImageIcon,
                var type => throw new NotSupportedException(type.ToString()),
            };

            //return base.SelectTemplateCore(item, container);
        }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            return this.SelectTemplateCore(item, null);
        }
    }
}
