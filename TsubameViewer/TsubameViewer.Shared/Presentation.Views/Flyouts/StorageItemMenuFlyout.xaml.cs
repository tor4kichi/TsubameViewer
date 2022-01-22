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
using TsubameViewer.Models.Domain.Albam;
using TsubameViewer.Presentation.ViewModels.Albam.Commands;
using TsubameViewer.Models.UseCase;

// ユーザー コントロールの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234236 を参照してください

namespace TsubameViewer.Presentation.Views.Flyouts
{
    public sealed partial class StorageItemMenuFlyout : MenuFlyout
    {
        public bool IsRootPage { get; set; } = false;

        public StorageItemMenuFlyout()
        {
            this.InitializeComponent();

            var container = App.Current.Container;
            OpenListupItem.Command = container.Resolve<OpenListupCommand>();
            SetThumbnailImageMenuItem.Command = container.Resolve<ChangeStorageItemThumbnailImageCommand>();
            AddFavariteImageMenuItem.Command = container.Resolve<FavoriteAddCommand>();
            SelectedItems_AddFavariteImageMenuItem.Command = AddFavariteImageMenuItem.Command;
            RemoveFavariteImageMenuItem.Command = container.Resolve<FavoriteRemoveCommand>();
            AlbamItemEditMenuItem.Command = container.Resolve<AlbamItemEditCommand>();
            SelectedItems_AlbamItemEditMenuItem.Command = AlbamItemEditMenuItem.Command;
            AlbamItemRemoveMenuItem.Command = container.Resolve<AlbamItemRemoveCommand>();
            AlbamEditMenuItem.Command = container.Resolve<AlbamEditCommand>();
            AlbamDeleteMenuItem.Command = container.Resolve<AlbamDeleteCommand>();
            AddSecondaryTile.Command = container.Resolve<SecondaryTileAddCommand>();
            RemoveSecondaryTile.Command = container.Resolve<SecondaryTileRemoveCommand>();
            OpenWithExplorerItem.Command = container.Resolve<OpenWithExplorerCommand>();
            SelectedItems_OpenWithExplorerItem.Command = OpenWithExplorerItem.Command;
            OpenWithExternalAppMenuItem.Command = container.Resolve<OpenWithExternalApplicationCommand>();
            _secondaryTileManager = container.Resolve<SecondaryTileManager>();
            _favoriteAlbam = container.Resolve<FavoriteAlbam>();
        }

        private readonly SecondaryTileManager _secondaryTileManager;
        private readonly FavoriteAlbam _favoriteAlbam;

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
                OpenListupItem.Visibility = (itemVM.Type == Models.Domain.StorageItemTypes.Archive || itemVM.Type == Models.Domain.StorageItemTypes.Folder).TrueToVisible();

                if (itemVM.Type == Models.Domain.StorageItemTypes.Image)
                {
                    var isFav = _favoriteAlbam.IsFavorite(itemVM.Path);
                    AddFavariteImageMenuItem.Visibility = isFav.FalseToVisible();
                    AddFavariteImageMenuItem.CommandParameter = itemVM;
                    RemoveFavariteImageMenuItem.Visibility = isFav.TrueToVisible();
                    RemoveFavariteImageMenuItem.CommandParameter = itemVM;
                    
                    AlbamItemEditMenuItem.Visibility = Visibility.Visible;
                    AlbamItemEditMenuItem.CommandParameter = itemVM;
                }
                else
                {
                    AddFavariteImageMenuItem.Visibility = Visibility.Collapsed;
                    RemoveFavariteImageMenuItem.Visibility = Visibility.Collapsed;
                    AlbamItemEditMenuItem.Visibility = Visibility.Collapsed;
                }

                AlbamItemRemoveMenuItem.Visibility = Visibility.Collapsed;

                AlbamEditMenuItem.Visibility = Visibility.Collapsed;
                AlbamDeleteMenuItem.Visibility = Visibility.Collapsed;

                SetThumbnailImageMenuItem.CommandParameter = itemVM;
                SetThumbnailImageMenuItem.Visibility = (IsRootPage is false && itemVM.Type is Models.Domain.StorageItemTypes.Image or Models.Domain.StorageItemTypes.Folder or Models.Domain.StorageItemTypes.Archive).TrueToVisible();

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

                OpenWithExplorerItem.CommandParameter = itemVM;                
                OpenWithExplorerItem.Visibility = (itemVM.Item is StorageItemImageSource).TrueToVisible();

                OpenWithExternalAppMenuItem.CommandParameter = itemVM;                
                OpenWithExternalAppMenuItem.Visibility = (itemVM.Item is StorageItemImageSource item && item.StorageItem is StorageFile).TrueToVisible();
            }
            else if (itemVM.Item is ArchiveDirectoryImageSource)
            {
                OpenListupItem.CommandParameter = itemVM;
                OpenListupItem.Visibility = Visibility.Visible;

                AddFavariteImageMenuItem.Visibility = Visibility.Collapsed;
                RemoveFavariteImageMenuItem.Visibility = Visibility.Collapsed;
                AlbamItemEditMenuItem.Visibility = Visibility.Collapsed;
                AlbamDeleteMenuItem.Visibility = Visibility.Collapsed;

                AlbamEditMenuItem.Visibility = Visibility.Collapsed;
                AlbamItemRemoveMenuItem.Visibility = Visibility.Collapsed;

                SetThumbnailImageMenuItem.CommandParameter = itemVM;
                SetThumbnailImageMenuItem.Visibility = Visibility.Visible;

                AddSecondaryTile.Visibility = Visibility.Collapsed;
                RemoveSecondaryTile.Visibility = Visibility.Collapsed;

                OpenWithExplorerItem.CommandParameter = itemVM;
                OpenWithExplorerItem.Visibility = Visibility.Collapsed;

                OpenWithExternalAppMenuItem.CommandParameter = itemVM;
                OpenWithExternalAppMenuItem.Visibility = Visibility.Collapsed;
            }
            else if (itemVM.Item is AlbamImageSource albam)
            {
                bool isFavAlbamItem = albam.AlbamId == FavoriteAlbam.FavoriteAlbamId;

                OpenListupItem.CommandParameter = itemVM;
                OpenListupItem.Visibility = Visibility.Visible;

                AddFavariteImageMenuItem.Visibility = Visibility.Collapsed;
                RemoveFavariteImageMenuItem.Visibility = Visibility.Collapsed;
                AlbamItemEditMenuItem.Visibility = Visibility.Collapsed;
                AlbamItemRemoveMenuItem.Visibility = Visibility.Collapsed;

                AlbamEditMenuItem.Visibility = isFavAlbamItem.FalseToVisible();
                AlbamEditMenuItem.CommandParameter = itemVM;
                AlbamDeleteMenuItem.Visibility = isFavAlbamItem.FalseToVisible();
                AlbamDeleteMenuItem.CommandParameter = itemVM;

                SetThumbnailImageMenuItem.Visibility = Visibility.Collapsed;

                AddSecondaryTile.Visibility = Visibility.Collapsed;
                RemoveSecondaryTile.Visibility = Visibility.Collapsed;

                OpenWithExplorerItem.CommandParameter = itemVM;
                OpenWithExplorerItem.Visibility = Visibility.Collapsed;

                OpenWithExternalAppMenuItem.CommandParameter = itemVM;
                OpenWithExternalAppMenuItem.Visibility = Visibility.Collapsed;
            }
            else if (itemVM.Item is AlbamItemImageSource albamItem)
            {
                bool isFavAlbamItem = albamItem.AlbamId == FavoriteAlbam.FavoriteAlbamId;

                OpenListupItem.CommandParameter = itemVM;
                OpenListupItem.Visibility = Visibility.Collapsed;

                AddFavariteImageMenuItem.Visibility = Visibility.Collapsed;
                RemoveFavariteImageMenuItem.Visibility = isFavAlbamItem.TrueToVisible();
                RemoveFavariteImageMenuItem.CommandParameter = itemVM;
                AlbamItemEditMenuItem.Visibility = Visibility.Visible;
                AlbamItemEditMenuItem.CommandParameter = itemVM;
                AlbamItemRemoveMenuItem.Visibility = isFavAlbamItem.FalseToVisible();
                AlbamItemRemoveMenuItem.CommandParameter = itemVM;

                AlbamEditMenuItem.Visibility = Visibility.Collapsed;
                AlbamDeleteMenuItem.Visibility = Visibility.Collapsed;

                SetThumbnailImageMenuItem.Visibility = Visibility.Collapsed;

                AddSecondaryTile.Visibility = Visibility.Collapsed;
                RemoveSecondaryTile.Visibility = Visibility.Collapsed;

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
                && (AddFavariteImageMenuItem.Visibility == Visibility.Visible
                    || RemoveFavariteImageMenuItem.Visibility == Visibility.Visible
                    || AlbamItemEditMenuItem.Visibility == Visibility.Visible
                    || AlbamItemRemoveMenuItem.Visibility == Visibility.Visible
                    || AlbamEditMenuItem.Visibility == Visibility.Visible
                    || AlbamDeleteMenuItem.Visibility == Visibility.Visible
                    )
                ).TrueToVisible();

            ThumbnailMenuSeparator.Visibility = SetThumbnailImageMenuItem.Visibility;

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
