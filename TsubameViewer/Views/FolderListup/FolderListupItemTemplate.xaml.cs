using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models;
using TsubameViewer.ViewModels;
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

namespace TsubameViewer.Views.FolderListup
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

        public DataTemplate AddFolderIcon { get; set; }
        public DataTemplate AddAlbamIcon { get; set; }
        public DataTemplate FavoriteIcon { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item == null) { return base.SelectTemplateCore(item, container); }

            if (item is StorageItemViewModel itemVM)
            {
                return itemVM.Type switch
                {
                    Core.Models.StorageItemTypes.Folder => FolderIcon,
                    Core.Models.StorageItemTypes.Archive => ArchiveIcon,
                    Core.Models.StorageItemTypes.ArchiveFolder => ArchiveFolderIcon,
                    Core.Models.StorageItemTypes.Albam => (itemVM.Item as AlbamImageSource).AlbamId == FavoriteAlbam.FavoriteAlbamId ? FavoriteIcon : AlbamIcon,
                    Core.Models.StorageItemTypes.AlbamImage => AlbamImageIcon,
                    Core.Models.StorageItemTypes.EBook => EBookIcon,
                    Core.Models.StorageItemTypes.Image => ImageIcon,
                    Core.Models.StorageItemTypes.AddFolder => AddFolderIcon,
                    Core.Models.StorageItemTypes.AddAlbam => AddAlbamIcon,
                    var type => throw new NotSupportedException(type.ToString()),
                };
            }

            return base.SelectTemplateCore(item, container);
        }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            return this.SelectTemplateCore(item, null);
        }
    }
}
