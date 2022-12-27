﻿using CommunityToolkit.Mvvm.DependencyInjection;
using TsubameViewer.Core.Contracts.Services;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using TsubameViewer.Core.UseCases;
using TsubameViewer.ViewModels.Albam.Commands;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.ViewModels.PageNavigation.Commands;
using TsubameViewer.ViewModels.SourceFolders.Commands;
using TsubameViewer.Views.Helpers;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

// ユーザー コントロールの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234236 を参照してください

namespace TsubameViewer.Views.Flyouts
{
    public sealed partial class StorageItemMenuFlyout : MenuFlyout
    {
        public bool IsRootPage { get; set; } = false;

        public StorageItemMenuFlyout()
        {
            this.InitializeComponent();

            OpenListupItem.Command = Ioc.Default.GetService<OpenListupCommand>();
            SetThumbnailImageMenuItem.Command = Ioc.Default.GetService<ChangeStorageItemThumbnailImageCommand>();
            SelectedItems_AddFavariteImageMenuItem.Command = Ioc.Default.GetService<FavoriteAddCommand>();
            SelectedItems_AlbamItemEditMenuItem.Command = Ioc.Default.GetService<AlbamItemEditCommand>();
            AlbamEditMenuItem.Command = Ioc.Default.GetService<AlbamEditCommand>();
            AlbamDeleteMenuItem.Command = Ioc.Default.GetService<AlbamDeleteCommand>();
            StorageItemDeleteMenuItem.Command = Ioc.Default.GetService<FileDeleteCommand>();
            FolderOrArchiveRestructureItem.Command = Ioc.Default.GetService<FolderOrArchiveResturctureCommand>();
            AddSecondaryTile.Command = Ioc.Default.GetService<SecondaryTileAddCommand>();
            RemoveSecondaryTile.Command = Ioc.Default.GetService<SecondaryTileRemoveCommand>();
            OpenWithExplorerItem.Command = Ioc.Default.GetService<OpenWithExplorerCommand>();
            SelectedItems_OpenWithExplorerItem.Command = OpenWithExplorerItem.Command;
            OpenWithExternalAppMenuItem.Command = Ioc.Default.GetService<OpenWithExternalApplicationCommand>();
            _secondaryTileManager = Ioc.Default.GetService<ISecondaryTileManager>();
            _favoriteAlbam = Ioc.Default.GetService<FavoriteAlbam>();
            _albamRepository = Ioc.Default.GetService<AlbamRepository>();
        }

        private readonly ISecondaryTileManager _secondaryTileManager;
        private readonly FavoriteAlbam _favoriteAlbam;
        private readonly AlbamRepository _albamRepository;
        
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

            if (itemVM.Selection is not null and var selection && selection.IsSelectionModeEnabled && selection.SelectedItems.Count >= 2)
            {
                SelectItemsSubItem.Visibility = Visibility.Visible;
                SelectedItems_AddFavariteImageMenuItem.CommandParameter = itemVM.Selection.SelectedItems;
                SelectedItems_AlbamItemEditMenuItem.CommandParameter = itemVM.Selection.SelectedItems;
                SelectedItems_OpenWithExplorerItem.CommandParameter = itemVM.Selection.SelectedItems;
            }
            else
            {
                SelectItemsSubItem.Visibility = Visibility.Collapsed;
            }

            if (itemVM.Item is StorageItemImageSource or ArchiveEntryImageSource or PdfPageImageSource)
            {
                OpenListupItem.CommandParameter = itemVM;
                OpenListupItem.Visibility = (itemVM.Type == Core.Models.StorageItemTypes.Archive || itemVM.Type == Core.Models.StorageItemTypes.Folder).TrueToVisible();

                var isFav = _favoriteAlbam.IsFavorite(itemVM.Path);

                AlbamEditMenuItem.Visibility = Visibility.Collapsed;
                AlbamDeleteMenuItem.Visibility = Visibility.Collapsed;

                AlbamMenuSubItem.Visibility = Visibility.Visible;
                AlbamMenuSubItem.Items.Clear();
                foreach (var albam in _albamRepository.GetAlbams())
                {
                    AlbamMenuSubItem.Items.Add(new ToggleMenuFlyoutItem { Text = albam.Name, Command = new AlbamItemAddCommand(_albamRepository, albam), CommandParameter = itemVM, IsChecked = _albamRepository.IsExistAlbamItem(albam._id, itemVM.Path) });
                }

                SetThumbnailImageMenuItem.CommandParameter = itemVM;
                SetThumbnailImageMenuItem.Visibility = (IsRootPage is false && itemVM.Type is Core.Models.StorageItemTypes.Image or Core.Models.StorageItemTypes.Folder or Core.Models.StorageItemTypes.Archive).TrueToVisible();

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

                StorageItemDeleteMenuItem.CommandParameter = itemVM;
                StorageItemDeleteMenuItem.Visibility = (IsRootPage is false && itemVM.Item is StorageItemImageSource).TrueToVisible();

                FolderOrArchiveRestructureItem.CommandParameter = itemVM;
                FolderOrArchiveRestructureItem.Visibility = (itemVM.Type is Core.Models.StorageItemTypes.Archive or Core.Models.StorageItemTypes.Folder).TrueToVisible();

                OpenWithExplorerItem.CommandParameter = itemVM;                
                OpenWithExplorerItem.Visibility = (itemVM.Item is StorageItemImageSource).TrueToVisible();

                OpenWithExternalAppMenuItem.CommandParameter = itemVM;                
                OpenWithExternalAppMenuItem.Visibility = (itemVM.Item is StorageItemImageSource item && item.StorageItem is StorageFile).TrueToVisible();
            }
            else if (itemVM.Item is ArchiveDirectoryImageSource)
            {
                OpenListupItem.CommandParameter = itemVM;
                OpenListupItem.Visibility = Visibility.Visible;

                var isFav = _favoriteAlbam.IsFavorite(itemVM.Path);

                AlbamEditMenuItem.Visibility = Visibility.Collapsed;
                AlbamDeleteMenuItem.Visibility = Visibility.Collapsed;

                AlbamMenuSubItem.Visibility= Visibility.Visible;
                AlbamMenuSubItem.Items.Clear();
                foreach (var albam in _albamRepository.GetAlbams())
                {
                    AlbamMenuSubItem.Items.Add(new ToggleMenuFlyoutItem { Text = albam.Name, Command = new AlbamItemAddCommand(_albamRepository, albam), CommandParameter = itemVM, IsChecked = _albamRepository.IsExistAlbamItem(albam._id, itemVM.Path) });
                }

                SetThumbnailImageMenuItem.CommandParameter = itemVM;
                SetThumbnailImageMenuItem.Visibility = Visibility.Visible;

                AddSecondaryTile.Visibility = Visibility.Collapsed;
                RemoveSecondaryTile.Visibility = Visibility.Collapsed;

                StorageItemDeleteMenuItem.Visibility = Visibility.Collapsed;

                OpenWithExplorerItem.CommandParameter = itemVM;
                OpenWithExplorerItem.Visibility = Visibility.Collapsed;

                OpenWithExternalAppMenuItem.CommandParameter = itemVM;
                OpenWithExternalAppMenuItem.Visibility = Visibility.Collapsed;
            }
            else if (itemVM.Item is AlbamImageSource albamImageSource)
            {
                bool isFavAlbamItem = albamImageSource.AlbamId == FavoriteAlbam.FavoriteAlbamId;

                OpenListupItem.CommandParameter = itemVM;
                OpenListupItem.Visibility = Visibility.Visible;

                AlbamEditMenuItem.Visibility = isFavAlbamItem.FalseToVisible();
                AlbamEditMenuItem.CommandParameter = itemVM;
                AlbamDeleteMenuItem.Visibility = isFavAlbamItem.FalseToVisible();
                AlbamDeleteMenuItem.CommandParameter = itemVM;

                AlbamMenuSubItem.Visibility = Visibility.Collapsed;

                SetThumbnailImageMenuItem.Visibility = Visibility.Collapsed;

                AddSecondaryTile.Visibility = Visibility.Collapsed;
                RemoveSecondaryTile.Visibility = Visibility.Collapsed;

                StorageItemDeleteMenuItem.Visibility = Visibility.Collapsed;

                OpenWithExplorerItem.CommandParameter = itemVM;
                OpenWithExplorerItem.Visibility = Visibility.Collapsed;

                OpenWithExternalAppMenuItem.CommandParameter = itemVM;
                OpenWithExternalAppMenuItem.Visibility = Visibility.Collapsed;
            }
            else if (itemVM.Item is AlbamItemImageSource albamItem)
            {
                bool isFavAlbamItem = albamItem.AlbamId == FavoriteAlbam.FavoriteAlbamId;

                AlbamItemType itemType = albamItem.GetAlbamItemType();
                var type = SupportedFileTypesHelper.StorageItemToStorageItemTypes(albamItem);
                OpenListupItem.CommandParameter = itemVM;
                OpenListupItem.Visibility = (type is Core.Models.StorageItemTypes.Archive or Core.Models.StorageItemTypes.ArchiveFolder or Core.Models.StorageItemTypes.Folder).TrueToVisible();

                AlbamEditMenuItem.Visibility = Visibility.Collapsed;
                AlbamDeleteMenuItem.Visibility = Visibility.Collapsed;

                AlbamMenuSubItem.Visibility = Visibility.Visible;
                AlbamMenuSubItem.Items.Clear();                
                foreach (var albam in _albamRepository.GetAlbams())
                {
                    var isExistInAlbam = _albamRepository.IsExistAlbamItem(albam._id, albamItem.Path);
                    AlbamMenuSubItem.Items.Add(new ToggleMenuFlyoutItem { Text = albam.Name, Command = new AlbamItemAddCommand(_albamRepository, albam), CommandParameter = itemVM, IsChecked = isExistInAlbam, IsEnabled = albamItem.InnerImageSource != null || isExistInAlbam });
                }

                SetThumbnailImageMenuItem.Visibility = Visibility.Collapsed;

                AddSecondaryTile.Visibility = Visibility.Collapsed;
                RemoveSecondaryTile.Visibility = Visibility.Collapsed;

                StorageItemDeleteMenuItem.CommandParameter = itemVM;
                StorageItemDeleteMenuItem.Visibility = (albamItem.InnerImageSource is StorageItemImageSource).TrueToVisible();

                var transformMenuItemVisibility = (albamItem.InnerImageSource is StorageItemImageSource imageSource && imageSource.ItemTypes is Core.Models.StorageItemTypes.Archive or Core.Models.StorageItemTypes.Folder).TrueToVisible();
                FolderOrArchiveRestructureItem.CommandParameter = itemVM;
                FolderOrArchiveRestructureItem.Visibility = transformMenuItemVisibility;

                OpenWithExplorerItem.CommandParameter = itemVM;
                OpenWithExplorerItem.Visibility = Visibility.Visible;

                OpenWithExternalAppMenuItem.CommandParameter = itemVM;
                OpenWithExternalAppMenuItem.Visibility = Visibility.Visible;
            }
            else
            {
                this.Hide();
                return;
            }

            AlbamMenuSeparator.Visibility = (
                (OpenListupItem.Visibility == Visibility.Visible || SelectItemsSubItem.Visibility is Visibility.Visible)
                && (AlbamEditMenuItem.Visibility == Visibility.Visible
                    || AlbamDeleteMenuItem.Visibility == Visibility.Visible
                    || AlbamMenuSubItem.Visibility == Visibility.Visible
                    )
                ).TrueToVisible();

            ThumbnailMenuSeparator.Visibility = SetThumbnailImageMenuItem.Visibility;

            FileControlMenuSeparator.Visibility =
                (StorageItemDeleteMenuItem.Visibility == Visibility.Visible                
                )
                .TrueToVisible()
                ;

            FolderAndArchiveMenuSeparator1.Visibility = (
                AddSecondaryTile.Visibility == Visibility.Visible
                || RemoveSecondaryTile.Visibility == Visibility.Visible
                ).TrueToVisible();

            FolderAndArchiveMenuSeparator2.Visibility = (
                OpenWithExplorerItem.Visibility == Visibility.Visible
                || OpenWithExternalAppMenuItem.Visibility == Visibility.Visible
                ).TrueToVisible();

            if (IsRootPage is true)
            {
                SetThumbnailImageMenuItem.Visibility = Visibility.Collapsed;
            }
        }
    }
}
