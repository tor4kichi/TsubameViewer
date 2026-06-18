using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using I18NPortable;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using TsubameViewer.Contracts.Notification;
using TsubameViewer.Core.Contracts.Services;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.ViewModels;
using TsubameViewer.ViewModels.Albam.Commands;
using TsubameViewer.ViewModels.PageNavigation.Commands;
using TsubameViewer.ViewModels.SourceFolders.Commands;
using TsubameViewer.Views.Helpers;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using ZLinq;

#nullable enable
namespace TsubameViewer.Views.Flyouts;

public sealed partial class StorageItemMenuFlyout : MenuFlyout
{
    public bool IsRootPage { get; set; } = false;

    public StorageItemMenuFlyout()
    {
        this.InitializeComponent();

        OpenListupItem.Command = Ioc.Default.GetService<OpenSecondaryListupCommand>();
        OpenViewerItem.Command = Ioc.Default.GetService<OpenImageViewerCommand>();
        SetThumbnailImageMenuItem.Command = Ioc.Default.GetService<ChangeStorageItemThumbnailImageCommand>();
        RemoveFromAccessListMenuItem.Command = Ioc.Default.GetService<RegisterItemRemoveFromAccessListCommand>();
        SelectedItems_AddFavariteImageMenuItem.Command = Ioc.Default.GetService<FavoriteToggleCommand>();
        StorageItemDeleteMenuItem.Command = Ioc.Default.GetService<FileDeleteCommand>();
        FolderOrArchiveRestructureItem.Command = Ioc.Default.GetService<FolderOrArchiveResturctureCommand>();
        AddSecondaryTile.Command = Ioc.Default.GetService<SecondaryTileAddCommand>();
        RemoveSecondaryTile.Command = Ioc.Default.GetService<SecondaryTileRemoveCommand>();
        OpenWithExplorerItem.Command = Ioc.Default.GetService<OpenWithExplorerCommand>();
        SelectedItems_OpenWithExplorerItem.Command = OpenWithExplorerItem.Command;
        OpenWithExternalAppMenuItem.Command = Ioc.Default.GetService<OpenWithExternalApplicationCommand>();
        _secondaryTileManager = Ioc.Default.GetRequiredService<ISecondaryTileManager>();
        _favoriteAlbam = Ioc.Default.GetRequiredService<FavoriteAlbam>();
        _albamRepository = Ioc.Default.GetRequiredService<AlbamRepository>();
        _bookmarkRepository = Ioc.Default.GetRequiredService<LocalBookmarkRepository>();
        ToggleReadingFinishedStateItem.Command = ToggleReadingFinishedStateCommand;
        ToggleFavoriteMenuItem.Command = Ioc.Default.GetRequiredService<FavoriteToggleCommand>(); 
    }

    readonly ISecondaryTileManager _secondaryTileManager;
    readonly FavoriteAlbam _favoriteAlbam;
    readonly AlbamRepository _albamRepository;
    readonly LocalBookmarkRepository _bookmarkRepository;

    void FolderAndArchiveMenuFlyout_Opened(object sender, object e)
    {
        long time = TimeProvider.System.GetTimestamp();

        var flyout = (FlyoutBase)sender;
        IStorageItemViewModel? itemVM = flyout.Target.DataContext as IStorageItemViewModel;
        if (itemVM == null && flyout.Target is Control content)
        {
            itemVM = (content as ContentControl)?.Content as IStorageItemViewModel;
        }

        if (itemVM == null)
        {
            flyout.Hide();
            return;
        }

        if (itemVM.Selection is not null and var selection && selection.IsSelectionModeEnabled && selection.SelectedItems.Count >= 2)
        {
            SelectItemsSubItem.Visibility = Visibility.Visible;
            SelectedItems_AddFavariteImageMenuItem.CommandParameter = selection.SelectedItems;
            SelectedItems_AddFavariteImageMenuItem.Text = selection.SelectedItems.All(x => _favoriteAlbam.IsFavorite(x.Path))
                ? "Favorite_RemoveItem".Translate()
                : "Favorite_AddItem".Translate();

            SelectedItems_OpenWithExplorerItem.CommandParameter = selection.SelectedItems;
        }
        else
        {
            SelectItemsSubItem.Visibility = Visibility.Collapsed;
        }

        if (itemVM.Item is StorageItemImageSource or ArchiveEntryImageSource or PdfPageImageSource)
        {
            OpenListupItem.CommandParameter = itemVM;
            OpenListupItem.Visibility = (itemVM.Type == Core.Models.StorageItemTypes.Archive || itemVM.Type == Core.Models.StorageItemTypes.Folder).TrueToVisible();

            OpenViewerItem.CommandParameter = itemVM;
            OpenViewerItem.Visibility = (itemVM.Type == Core.Models.StorageItemTypes.Archive || itemVM.Type == Core.Models.StorageItemTypes.Folder).TrueToVisible();
            
            SetThumbnailImageMenuItem.CommandParameter = itemVM;
            SetThumbnailImageMenuItem.Visibility = (IsRootPage is false && itemVM.Type is Core.Models.StorageItemTypes.Image or Core.Models.StorageItemTypes.Folder or Core.Models.StorageItemTypes.Archive or Core.Models.StorageItemTypes.Movie).TrueToVisible();
            RemoveFromAccessListMenuItem.CommandParameter = itemVM;
            RemoveFromAccessListMenuItem.Visibility = IsRootPage.TrueToVisible();

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
            FolderOrArchiveRestructureItem.Visibility = (!IsRootPage && itemVM.Type is Core.Models.StorageItemTypes.Archive or Core.Models.StorageItemTypes.Folder).TrueToVisible();

            OpenWithExplorerItem.CommandParameter = itemVM;                
            OpenWithExplorerItem.Visibility = (itemVM.Item is StorageItemImageSource).TrueToVisible();

            OpenWithExternalAppMenuItem.CommandParameter = itemVM;                
            OpenWithExternalAppMenuItem.Visibility = (itemVM.Item is StorageItemImageSource item && item.StorageItem is StorageFile).TrueToVisible();
        }
        else if (itemVM.Item is ArchiveDirectoryImageSource)
        {
            OpenListupItem.CommandParameter = itemVM;
            OpenListupItem.Visibility = Visibility.Visible;
            OpenViewerItem.CommandParameter = itemVM;
            OpenViewerItem.Visibility = Visibility.Visible;

            SetThumbnailImageMenuItem.CommandParameter = itemVM;
            SetThumbnailImageMenuItem.Visibility = Visibility.Visible;            

            AddSecondaryTile.Visibility = Visibility.Collapsed;
            RemoveSecondaryTile.Visibility = Visibility.Collapsed;

            StorageItemDeleteMenuItem.Visibility = Visibility.Collapsed;

            OpenWithExplorerItem.CommandParameter = itemVM;
            OpenWithExplorerItem.Visibility = Visibility.Collapsed;

            OpenWithExternalAppMenuItem.CommandParameter = itemVM;
            OpenWithExternalAppMenuItem.Visibility = Visibility.Collapsed;

            SendOtherFolderMenuItem.Visibility = Visibility.Collapsed;
            FolderOrArchiveRestructureItem.Visibility = Visibility.Collapsed;
        }
        else if (itemVM.Item is AlbamImageSource albamImageSource)
        {
            bool isFavAlbamItem = albamImageSource.AlbamId == FavoriteAlbam.FavoriteAlbamId;

            OpenListupItem.CommandParameter = itemVM;
            OpenListupItem.Visibility = Visibility.Visible;
            OpenViewerItem.CommandParameter = itemVM;
            OpenViewerItem.Visibility = Visibility.Visible;

            SetThumbnailImageMenuItem.Visibility = Visibility.Collapsed;
            RemoveFromAccessListMenuItem.Visibility = Visibility.Collapsed;

            AddSecondaryTile.Visibility = Visibility.Collapsed;
            RemoveSecondaryTile.Visibility = Visibility.Collapsed;

            StorageItemDeleteMenuItem.Visibility = Visibility.Collapsed;

            OpenWithExplorerItem.CommandParameter = itemVM;
            OpenWithExplorerItem.Visibility = Visibility.Collapsed;

            OpenWithExternalAppMenuItem.CommandParameter = itemVM;
            OpenWithExternalAppMenuItem.Visibility = Visibility.Collapsed;

            FolderOrArchiveRestructureItem.Visibility = Visibility.Collapsed;
        }
        else if (itemVM.Item is AlbamItemImageSource albamItem)
        {
            bool isFavAlbamItem = albamItem.AlbamId == FavoriteAlbam.FavoriteAlbamId;

            AlbamItemType itemType = albamItem.GetAlbamItemType();
            var type = SupportedFileTypesHelper.StorageItemToStorageItemTypes(albamItem);
            OpenListupItem.CommandParameter = itemVM;
            OpenListupItem.Visibility = (type is Core.Models.StorageItemTypes.Archive or Core.Models.StorageItemTypes.ArchiveFolder or Core.Models.StorageItemTypes.Folder).TrueToVisible();
            OpenViewerItem.CommandParameter = itemVM;
            OpenViewerItem.Visibility = (type is Core.Models.StorageItemTypes.Archive or Core.Models.StorageItemTypes.ArchiveFolder or Core.Models.StorageItemTypes.Folder).TrueToVisible();

            SetThumbnailImageMenuItem.Visibility = Visibility.Collapsed;
            RemoveFromAccessListMenuItem.Visibility = Visibility.Collapsed;

            AddSecondaryTile.Visibility = Visibility.Collapsed;
            RemoveSecondaryTile.Visibility = Visibility.Collapsed;

            StorageItemDeleteMenuItem.CommandParameter = itemVM;
            StorageItemDeleteMenuItem.Visibility = (type is Core.Models.StorageItemTypes.Folder 
                or Core.Models.StorageItemTypes.Archive 
                or Core.Models.StorageItemTypes.EBook 
                or Core.Models.StorageItemTypes.Movie 
                or Core.Models.StorageItemTypes.Image)
                .TrueToVisible();

            FolderOrArchiveRestructureItem.CommandParameter = itemVM;
            FolderOrArchiveRestructureItem.Visibility = (type is Core.Models.StorageItemTypes.Archive or Core.Models.StorageItemTypes.Folder)
                .TrueToVisible();

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

        FileControlMenuSeparator.Visibility =
            (StorageItemDeleteMenuItem.Visibility == Visibility.Visible                
            )
            .TrueToVisible()
            ;

        var isFav = _favoriteAlbam.IsFavorite(itemVM.Path);
        ToggleFavoriteMenuItem.Text = isFav
            ? "Favorite_RemoveItem".Translate()
            : "Favorite_AddItem".Translate();
        ToggleFavoriteMenuItem.IsChecked = isFav;
        ToggleFavoriteMenuItem.CommandParameter = itemVM;

        //FolderAndArchiveMenuSeparator1.Visibility = (
        //    AddSecondaryTile.Visibility == Visibility.Visible
        //    || RemoveSecondaryTile.Visibility == Visibility.Visible
        //    ).TrueToVisible();

        FolderAndArchiveMenuSeparator2.Visibility = (
            OpenWithExplorerItem.Visibility == Visibility.Visible
            || OpenWithExternalAppMenuItem.Visibility == Visibility.Visible
            ).TrueToVisible();

        if (IsRootPage is true)
        {
            SetThumbnailImageMenuItem.Visibility = Visibility.Collapsed;            
        }

        RemoveFromAccessListMenuItem.Visibility = IsRootPage.TrueToVisible();
        RemoveFromAccessListMenuItem.CommandParameter = itemVM;


        if (IsRootPage)
        {
            SendOtherFolderMenuItem.Visibility = Visibility.Collapsed;
        }
        else
        {
            SendOtherFolderMenuItem.Visibility = 
                (itemVM.Type is not Core.Models.StorageItemTypes.ArchiveFolder
                && itemVM.Item is not AlbamImageSource).TrueToVisible();
            if (SendOtherFolderMenuItem.Visibility == Visibility.Visible)
            {
                var messenger = Ioc.Default.GetRequiredService<IMessenger>();
                var sourceFolderRegistrationRepository = Ioc.Default.GetRequiredService<SourceStorageItemsRepository>();
                SendOtherFolderMenuItem.Items.Clear();
                var parentFolderPath = Path.GetDirectoryName(itemVM.Path);
                foreach (SourceStorageItemsRepository.TokenToPathEntry item in sourceFolderRegistrationRepository.GetParsistantItemsFromCache().AsValueEnumerable().OrderBy(x => x.Order))
                {
                    var menuItem = new MenuFlyoutItem()
                    {
                        Text = Path.GetFileName(item.Path),
                        Command = new SendToOtherFolderCommand(item, sourceFolderRegistrationRepository, messenger),
                        CommandParameter = itemVM,
                        IsEnabled = !parentFolderPath.Equals(item.Path, System.StringComparison.Ordinal),
                    };
                    SendOtherFolderMenuItem.Items.Add(menuItem);
                }
            }
        }

        if (itemVM.Type is Core.Models.StorageItemTypes.Archive or Core.Models.StorageItemTypes.EBook or Core.Models.StorageItemTypes.Movie)
        {
            var facade = _bookmarkRepository.GetBookmarkFacade(itemVM.Path);
            ToggleReadingFinishedStateItem.CommandParameter = itemVM;
            ToggleReadingFinishedStateItem.Text = facade.IsFinishedReading
                ? "ReadingState_ToggleToUnfinished".Translate()
                : "ReadingState_ToggleToFinished".Translate();
            ToggleReadingFinishedStateItem.Visibility = Visibility.Visible;
        }
        else
        {
            ToggleReadingFinishedStateItem.Visibility = Visibility.Collapsed;
        }
        

        Debug.WriteLine($"StorateItemMenuFlyout.Opened: {TimeProvider.System.GetElapsedTime(time)}");
    }


    [RelayCommand]
    void ToggleReadingFinishedState(IStorageItemViewModel? itemVM)
    {
        if (itemVM == null) { return; }

        var facade = _bookmarkRepository.GetBookmarkFacade(itemVM.Path);
        facade.IsFinishedReading = !facade.IsFinishedReading;
        itemVM.UpdateLastReadPosition();
        Ioc.Default.GetRequiredService<IMessenger>().SendShowTextNotificationMessage(facade.IsFinishedReading
            ? "ReadingState_SetToFinished".Translate()
            : "ReadingState_SetToUnfinished".Translate());
    }
}
