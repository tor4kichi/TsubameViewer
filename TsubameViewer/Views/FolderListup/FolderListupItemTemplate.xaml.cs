using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using I18NPortable;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Contracts.Notification;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Helpers;
using TsubameViewer.ViewModels;
using TsubameViewer.ViewModels.PageNavigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using ZLinq;

#nullable enable
namespace TsubameViewer.Views.FolderListup;

public sealed partial class FolderListupItemTemplate : ResourceDictionary
{
    public FolderListupItemTemplate()
    {
        this.InitializeComponent();
    }

    private void Grid_DragEnter(object sender, DragEventArgs e)
    {
        var hostUI = (FrameworkElement)sender;
        if (e.DataView.Properties.TryGetValue("MyCustomDroppedItems", out object itemsRaw) is false) { return; }
        var items = (itemsRaw as List<object>);
        if (items is null or { Count: 0 }) { return; }
        var deferral = R3.Disposable.Create(e.GetDeferral(), deferral => deferral.Complete());
        AsyncTaskErrorHandler.Handle((this, hostUI, e, deferral, items), static async (s) =>
        {
            var (_this, hostUI, e, deferral, items) = s;
            using (deferral)
            {
                foreach (var item in items)
                {
                    if (item is not IStorageItemViewModel myItem)
                    {
                        System.Diagnostics.Debug.WriteLine($"処理できないドラッグされたアイテム: {item?.GetType().Name}");
                        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
                        return;
                    }
                }
                if (hostUI.DataContext is IStorageItemViewModel hostUIItemVM
                    && hostUIItemVM.Item.StorageItem is Windows.Storage.StorageFolder hostFolder)
                {
                    e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
                    e.DragUIOverride.Caption = "MoveToFolder_WithFolderName".Translate(hostFolder.Name);
                }
            }
        });        
    }

    private void Grid_Drop(object sender, DragEventArgs e)
    {        
        var hostUI = (FrameworkElement)sender;
        if (e.DataView.Properties.TryGetValue("MyCustomDroppedItems", out object itemsRaw) is false) { return; }
        var items = (itemsRaw as List<object>).ToList();
        if (items is null or { Count: 0 }) { return; }
        AsyncTaskErrorHandler.Handle((this, hostUI, e, items), static async (s) =>
        {
            var (_this, hostUI, e, items) = s;            
            if (hostUI.DataContext is IStorageItemViewModel hostUIItemVM
                && hostUIItemVM.Item.StorageItem is Windows.Storage.StorageFolder hostFolder)
            {
                var messenger = Ioc.Default.GetRequiredService<IMessenger>();
                await _this.MoveItemsToAsync(hostFolder, items.Cast<IStorageItemViewModel>().Select(x => x.Item.StorageItem), default);
                messenger.SendShowTextNotificationMessage(items.Count == 1
                    ? "MoveToFolder_Completed_Single".Translate(((IStorageItemViewModel)items[0]).Name, hostFolder.Name)
                    : "MoveToFolder_Completed_Multi".Translate(items.Count, hostFolder.Name));

                foreach (var item in items.Cast<IStorageItemViewModel>())
                {
                    messenger.Send(new StorageItemNotFoundMessage(item.Path));
                }
            }
            
        });

        // TODO: インスタントな「元に戻す」UIの表示
    }

    private async Task MoveItemsToAsync(Windows.Storage.StorageFolder targetFolder, IEnumerable<Windows.Storage.IStorageItem> items, CancellationToken ct)
    {
        foreach (var item in items)
        {
            Debug.WriteLine($"Move to {targetFolder.Path}: {item.Name}");
            List<Windows.Storage.IStorageItem> failedItems = [];
            if (item is Windows.Storage.StorageFile file)
            {
                try
                {
                    await file.MoveAsync(targetFolder, file.Name, Windows.Storage.NameCollisionOption.FailIfExists).AsTask(ct);                    
                }
                catch 
                {
                    failedItems.Add(item);
                }
            }
            else if (item is Windows.Storage.StorageFolder folder)
            {
                await folder.MoveAsync(targetFolder, Windows.Storage.CreationCollisionOption.OpenIfExists, Windows.Storage.NameCollisionOption.FailIfExists);
            }
        }
    }
}

public sealed class StorageItemIconTemplateSelector : DataTemplateSelector
{
    public DataTemplate? FolderIcon { get; set; }
    public DataTemplate? ArchiveIcon { get; set; }
    public DataTemplate? ArchiveFolderIcon { get; set; }
    public DataTemplate? AlbamIcon { get; set; }
    public DataTemplate? AlbamImageIcon { get; set; }
    public DataTemplate? EBookIcon { get; set; }
    public DataTemplate? MovieIcon { get; set; }
    public DataTemplate? ImageIcon { get; set; }

    public DataTemplate? AddFolderIcon { get; set; }
    public DataTemplate? AddAlbamIcon { get; set; }
    public DataTemplate? FavoriteIcon { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
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
                StorageItemTypes.Albam => (itemVM.Item as AlbamImageSource)!.AlbamId == FavoriteAlbam.FavoriteAlbamId ? FavoriteIcon : AlbamIcon,
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

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        return this.SelectTemplateCore(item, null!);
    }
}
