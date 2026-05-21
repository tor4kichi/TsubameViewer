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
        public DataTemplate MovieIcon { get; set; }
        public DataTemplate ImageIcon { get; set; }

        public DataTemplate AddFolderIcon { get; set; }
        public DataTemplate AddAlbamIcon { get; set; }
        public DataTemplate FavoriteIcon { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item == null) { return base.SelectTemplateCore(item, container); }


            if (item is StorageItemTypes type)
            {
                return type switch
                {
                    StorageItemTypes.Folder => FolderIcon,
                    StorageItemTypes.Archive => ArchiveIcon,
                    StorageItemTypes.ArchiveFolder => ArchiveFolderIcon,
                    StorageItemTypes.AlbamImage => AlbamImageIcon,
                    StorageItemTypes.EBook => EBookIcon,
                    StorageItemTypes.Image => ImageIcon,
                    StorageItemTypes.AddFolder => AddFolderIcon,
                    StorageItemTypes.AddAlbam => AddAlbamIcon,
                    StorageItemTypes.Movie => MovieIcon,
                    var otherType => ImageIcon,
                };
            }
            else if (item is IStorageItemViewModel itemVM)
            {
                return itemVM.Type switch
                {
                    StorageItemTypes.Folder => FolderIcon,
                    StorageItemTypes.Archive => ArchiveIcon,
                    StorageItemTypes.ArchiveFolder => ArchiveFolderIcon,
                    StorageItemTypes.Albam => (itemVM.Item as AlbamImageSource).AlbamId == FavoriteAlbam.FavoriteAlbamId ? FavoriteIcon : AlbamIcon,
                    StorageItemTypes.AlbamImage => AlbamImageIcon,
                    StorageItemTypes.EBook => EBookIcon,
                    StorageItemTypes.Image => ImageIcon,
                    StorageItemTypes.AddFolder => AddFolderIcon,
                    StorageItemTypes.AddAlbam => AddAlbamIcon,
                    StorageItemTypes.Movie => MovieIcon,
                    var otherType => ImageIcon,
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
