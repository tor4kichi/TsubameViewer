using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using TsubameViewer.Presentation.ViewModels;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using Uno;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace TsubameViewer.Presentation.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SourceStorageItemsPage : Page
    {
        public SourceStorageItemsPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            await Task.Delay(500);

            var settings = new Models.Domain.FolderItemListing.FolderListingSettings();
            if (settings.IsForceEnableXYNavigation
                || Xamarin.Essentials.DeviceInfo.Idiom == Xamarin.Essentials.DeviceIdiom.TV
                )
            {
                if (FoldersAdaptiveGridView.Items.Any())
                {
                    var firstItem = FoldersAdaptiveGridView.Items.First();
                    var itemContainer = FoldersAdaptiveGridView.ContainerFromItem(firstItem) as Control;
                    if (itemContainer != null)
                    {
                        itemContainer.Focus(FocusState.Keyboard);
                    }
                }
            }
        }


        private void FoldersMenuFlyout_Opened(object sender, object e)
        {
            var flyout = sender as FlyoutBase;
            var pageVM = DataContext as SourceStorageItemsPageViewModel;
            if (flyout.Target is ContentControl content)
            {
                var itemVM = (content.DataContext as StorageItemViewModel) ?? content.Content as StorageItemViewModel;
                if (itemVM == null)
                {
                    flyout.Hide();
                    return;
                }

                OpenImageViewerItem.CommandParameter = itemVM;
                OpenImageViewerItem.Command = pageVM.OpenImageViewerCommand;

                OpenListupItem.CommandParameter = itemVM;
                OpenListupItem.Command = pageVM.OpenFolderListupCommand;
                OpenListupItem.Visibility = (itemVM.Type == Models.Domain.StorageItemTypes.Archive || itemVM.Type == Models.Domain.StorageItemTypes.Folder) 
                    ? Visibility.Visible
                    : Visibility.Collapsed
                    ;
                AddSecondaryTile.CommandParameter = itemVM;
                AddSecondaryTile.Command = pageVM.SecondaryTileAddCommand;
                AddSecondaryTile.Visibility = pageVM.SecondaryTileManager.ExistTile(itemVM.Path) ? Visibility.Collapsed : Visibility.Visible;

                RemoveSecondaryTile.CommandParameter = itemVM;
                RemoveSecondaryTile.Command = pageVM.SecondaryTileRemoveCommand;
                RemoveSecondaryTile.Visibility = pageVM.SecondaryTileManager.ExistTile(itemVM.Path) ? Visibility.Visible : Visibility.Collapsed;

                OpenWithExplorerItem.CommandParameter = itemVM;
                OpenWithExplorerItem.Command = pageVM.OpenWithExplorerCommand;

                RemoveSourceStorageItem.CommandParameter = itemVM;
                RemoveSourceStorageItem.Command = pageVM.DeleteStoredFolderCommand;
            }
            else
            {
                flyout.Hide();
                return;
            }
        }
    }
}
