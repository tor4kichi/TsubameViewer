using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Core.Models;
using TsubameViewer.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

#nullable enable
namespace TsubameViewer.Views.TemplateSelector;

public sealed class FolderItemTemplateSelector : DataTemplateSelector
{
    public Windows.UI.Xaml.DataTemplate? LazyFolderOrArchiveFile { get; set; }
    public Windows.UI.Xaml.DataTemplate? AddNewFolder { get; set; }
    public Windows.UI.Xaml.DataTemplate? Folder { get; set; }
    public Windows.UI.Xaml.DataTemplate? Image { get; set; }
    public Windows.UI.Xaml.DataTemplate? Archive { get; set; }
    public Windows.UI.Xaml.DataTemplate? ArchiveFolder { get; set; }
    public Windows.UI.Xaml.DataTemplate? Albam { get; set; }
    public Windows.UI.Xaml.DataTemplate? AlbamImage { get; set; }
    public Windows.UI.Xaml.DataTemplate? EBook { get; set; }

    protected override Windows.UI.Xaml.DataTemplate? SelectTemplateCore(object item)
    {
        return SelectTemplateCore(item, null!);
    }
    protected override Windows.UI.Xaml.DataTemplate? SelectTemplateCore(object item, DependencyObject container)
    {
        if (item is LazyFolderOrArchiveFileViewModel)
        {
            return LazyFolderOrArchiveFile;
        }
        else if (item is IStorageItemViewModel itemVM)
        {
            return itemVM.Type switch
            {
                StorageItemTypes.Image => Image,
                StorageItemTypes.Folder => Folder,
                StorageItemTypes.Archive => Archive,
                StorageItemTypes.ArchiveFolder => ArchiveFolder,
                StorageItemTypes.Albam => Albam,
                StorageItemTypes.AlbamImage => AlbamImage,
                StorageItemTypes.EBook => EBook,
                StorageItemTypes.AddFolder => AddNewFolder,
                StorageItemTypes.AddAlbam => AddNewFolder,
                _ => throw new NotSupportedException()
            };
        }
        
        return base.SelectTemplateCore(item, container);
    }
}
