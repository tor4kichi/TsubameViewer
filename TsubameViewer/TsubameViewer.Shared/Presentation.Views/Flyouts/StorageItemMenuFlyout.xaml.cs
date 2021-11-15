using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
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
using Prism.Ioc;
using TsubameViewer.Presentation.ViewModels.PageNavigation.Commands;
using TsubameViewer.Presentation.Services.UWP;
using TsubameViewer.Presentation.Views.Helpers;
using TsubameViewer.Presentation.ViewModels.SourceFolders.Commands;
using Windows.Storage;

// ユーザー コントロールの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234236 を参照してください

namespace TsubameViewer.Presentation.Views.Flyouts
{
    public sealed partial class StorageItemMenuFlyout : MenuFlyout
    {
        public StorageItemMenuFlyout()
        {
            this.InitializeComponent();

            var container = App.Current.Container;
            OpenListupItem.Command = container.Resolve<OpenListupCommand>();
            SetThumbnailImageMenuItem.Command = container.Resolve<ChangeStorageItemThumbnailImageCommand>();
            AddSecondaryTile.Command = container.Resolve<SecondaryTileAddCommand>();
            RemoveSecondaryTile.Command = container.Resolve<SecondaryTileRemoveCommand>();
            OpenWithExplorerItem.Command = container.Resolve<OpenWithExplorerCommand>();
            OpenWithExternalAppMenuItem.Command = container.Resolve<OpenWithExternalApplicationCommand>();
            RemoveSourceStorageItem.Command = container.Resolve<DeleteStoredFolderCommand>();
            _secondaryTileManager = container.Resolve<SecondaryTileManager>();
        }

        private SecondaryTileManager _secondaryTileManager;

        private void FolderAndArchiveMenuFlyout_Opened(object sender, object e)
        {
            var flyout = sender as FlyoutBase;
            
            StorageItemViewModel itemVM = flyout.Target.DataContext as StorageItemViewModel;
            if (itemVM == null && flyout.Target is Control content)
            {
                itemVM = (content as ContentControl)?.Content as StorageItemViewModel;
            }

            if (itemVM == null)
            {
                flyout.Hide();
                return;
            }

            if (itemVM.Item is StorageItemImageSource or ArchiveEntryImageSource or PdfPageImageSource)
            {
                NoActionDescMenuItem.Visibility = Visibility.Collapsed;

                bool isSourceStorageItem = (itemVM.Token.RootItemPath == itemVM.Path);

                OpenListupItem.CommandParameter = itemVM;
                OpenListupItem.Visibility = (itemVM.Type == Models.Domain.StorageItemTypes.Archive || itemVM.Type == Models.Domain.StorageItemTypes.Folder).TrueToVisible();

                SetThumbnailImageMenuItem.CommandParameter = itemVM;
                SetThumbnailImageMenuItem.Visibility = (isSourceStorageItem is false && itemVM.Type is Models.Domain.StorageItemTypes.Image or Models.Domain.StorageItemTypes.Folder or Models.Domain.StorageItemTypes.Archive).TrueToVisible();

                FolderAndArchiveMenuSeparator1.Visibility = OpenListupItem.Visibility;

                if (itemVM.Path is not null)
                {
                    bool isExistTile = _secondaryTileManager.ExistTile(itemVM.Path);
                    AddSecondaryTile.CommandParameter = itemVM;
                    AddSecondaryTile.Visibility = isExistTile.FalseToVisible();

                    RemoveSecondaryTile.CommandParameter = itemVM;
                    RemoveSecondaryTile.Visibility = isExistTile.TrueToVisible();
                }
                else
                {
                    AddSecondaryTile.Visibility = Visibility.Collapsed;
                    RemoveSecondaryTile.Visibility = Visibility.Collapsed;
                }

                FolderAndArchiveMenuSeparator2.Visibility = Visibility.Visible;

                OpenWithExplorerItem.CommandParameter = itemVM;                
                OpenWithExplorerItem.Visibility = (itemVM.Item is StorageItemImageSource).TrueToVisible();

                OpenWithExternalAppMenuItem.CommandParameter = itemVM;                
                OpenWithExternalAppMenuItem.Visibility = (itemVM.Item is StorageItemImageSource item && item.StorageItem is StorageFile).TrueToVisible();

                RemoveSourceStorageItem.CommandParameter = itemVM;
                SourceManageSeparetor.Visibility = isSourceStorageItem.TrueToVisible();
                SourceManageSubItem.Visibility = isSourceStorageItem.TrueToVisible();
            }
            else if (itemVM.Item is ArchiveDirectoryImageSource)
            {
                NoActionDescMenuItem.Visibility = Visibility.Collapsed;

                bool isSourceStorageItem = (itemVM.Token.RootItemPath == itemVM.Path);

                OpenListupItem.CommandParameter = itemVM;
                OpenListupItem.Visibility = Visibility.Visible;

                SetThumbnailImageMenuItem.CommandParameter = itemVM;
                SetThumbnailImageMenuItem.Visibility = Visibility.Visible;

                FolderAndArchiveMenuSeparator1.Visibility = OpenListupItem.Visibility;

                AddSecondaryTile.Visibility = Visibility.Collapsed;
                RemoveSecondaryTile.Visibility = Visibility.Collapsed;

                FolderAndArchiveMenuSeparator2.Visibility = Visibility.Collapsed;

                OpenWithExplorerItem.CommandParameter = itemVM;
                OpenWithExplorerItem.Visibility = Visibility.Collapsed;

                OpenWithExternalAppMenuItem.CommandParameter = itemVM;
                OpenWithExternalAppMenuItem.Visibility = Visibility.Collapsed;

                RemoveSourceStorageItem.CommandParameter = itemVM;
                SourceManageSeparetor.Visibility = Visibility.Collapsed;
                SourceManageSubItem.Visibility = Visibility.Collapsed;
            }
            else
            {
                NoActionDescMenuItem.Visibility = Visibility.Visible;

                OpenListupItem.Visibility = Visibility.Collapsed;
                SetThumbnailImageMenuItem.Visibility = Visibility.Collapsed;
                AddSecondaryTile.Visibility = Visibility.Collapsed;
                RemoveSecondaryTile.Visibility = Visibility.Collapsed;
                OpenWithExplorerItem.Visibility = Visibility.Collapsed;
                OpenWithExternalAppMenuItem.Visibility = Visibility.Collapsed;
                FolderAndArchiveMenuSeparator1.Visibility = Visibility.Collapsed;
                FolderAndArchiveMenuSeparator2.Visibility = Visibility.Collapsed;
                SourceManageSeparetor.Visibility = Visibility.Collapsed;
                SourceManageSubItem.Visibility = Visibility.Collapsed;
            }
        }
    }
}
