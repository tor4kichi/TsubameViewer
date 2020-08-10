using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
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
using Microsoft.Toolkit.Uwp.UI.Animations.Effects;
using Microsoft.Toolkit.Uwp.UI.Animations;
using Xamarin.Essentials;
using Microsoft.Toolkit.Uwp.UI.Extensions;
using System.Reactive;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using Reactive.Bindings.Extensions;
using Uno.Extensions;
using Uno.Threading;
using System.Threading.Tasks;
using TsubameViewer.Presentation.ViewModels;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace TsubameViewer.Presentation.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class FolderListupPage : Page
    {
        public FolderListupPage()
        {
            this.InitializeComponent();

            this.Loaded += FolderListupPage_Loaded;
            this.Unloaded += FolderListupPage_Unloaded;
        }

        private void FolderListupPage_Loaded(object sender, RoutedEventArgs e)
        {
            FileItemsRepeater_Small.ElementPrepared += FileItemsRepeater_ElementPrepared;
            FileItemsRepeater_Midium.ElementPrepared += FileItemsRepeater_ElementPrepared;
            FileItemsRepeater_Large.ElementPrepared += FileItemsRepeater_ElementPrepared;
        }

        private void FolderListupPage_Unloaded(object sender, RoutedEventArgs e)
        {
            FileItemsRepeater_Small.ElementPrepared += FileItemsRepeater_ElementPrepared;
            FileItemsRepeater_Midium.ElementPrepared += FileItemsRepeater_ElementPrepared;
            FileItemsRepeater_Large.ElementPrepared += FileItemsRepeater_ElementPrepared;
        }




        #region 初期フォーカス設定

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var settings = new Models.Domain.FolderItemListing.FolderListingSettings();
            if (settings.IsForceEnableXYNavigation
                || Xamarin.Essentials.DeviceInfo.Idiom == Xamarin.Essentials.DeviceIdiom.TV
                )
            {
                if (FoldersAdaptiveGridView.Items.Any())
                {
                    var firstItem = FoldersAdaptiveGridView.Items.First();
                    var itemContainer = FoldersAdaptiveGridView.ContainerFromItem(firstItem) as Control;
                    itemContainer.Focus(FocusState.Keyboard);
                }
                else if (FileItemsRepeater_Small.ItemsSource != null && FileItemsRepeater_Small.Visibility == Visibility.Visible)
                {
                    var item = FileItemsRepeater_Small.FindDescendant<Control>();
                    item?.Focus(FocusState.Keyboard);
                }
                else if (FileItemsRepeater_Midium.ItemsSource != null && FileItemsRepeater_Midium.Visibility == Visibility.Visible)
                {
                    var item = FileItemsRepeater_Midium.FindDescendant<Control>();
                    item?.Focus(FocusState.Keyboard);
                }
                else if (FileItemsRepeater_Large.ItemsSource != null && FileItemsRepeater_Large.Visibility == Visibility.Visible)
                {
                    var item = FileItemsRepeater_Large.FindDescendant<Control>();
                    item?.Focus(FocusState.Keyboard);
                }
                else
                {
                    ReturnSourceFolderPageButton.Focus(FocusState.Keyboard);

                    this.FoldersAdaptiveGridView.ContainerContentChanging -= FoldersAdaptiveGridView_ContainerContentChanging;
                    this.FileItemsRepeater_Small.ElementPrepared -= FileItemsRepeater_Large_ElementPrepared;
                    this.FileItemsRepeater_Midium.ElementPrepared -= FileItemsRepeater_Large_ElementPrepared;
                    this.FileItemsRepeater_Large.ElementPrepared -= FileItemsRepeater_Large_ElementPrepared;

                    this.FoldersAdaptiveGridView.ContainerContentChanging += FoldersAdaptiveGridView_ContainerContentChanging;
                    this.FileItemsRepeater_Small.ElementPrepared += FileItemsRepeater_Large_ElementPrepared;
                    this.FileItemsRepeater_Midium.ElementPrepared += FileItemsRepeater_Large_ElementPrepared;
                    this.FileItemsRepeater_Large.ElementPrepared += FileItemsRepeater_Large_ElementPrepared;
                }
            }
            
        }

        private void FileItemsRepeater_Large_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            this.FoldersAdaptiveGridView.ContainerContentChanging -= FoldersAdaptiveGridView_ContainerContentChanging;
            this.FileItemsRepeater_Small.ElementPrepared -= FileItemsRepeater_Large_ElementPrepared;
            this.FileItemsRepeater_Midium.ElementPrepared -= FileItemsRepeater_Large_ElementPrepared;
            this.FileItemsRepeater_Large.ElementPrepared -= FileItemsRepeater_Large_ElementPrepared;

            (args.Element as Control).Focus(FocusState.Keyboard);
        }


        private void FoldersAdaptiveGridView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            this.FoldersAdaptiveGridView.ContainerContentChanging -= FoldersAdaptiveGridView_ContainerContentChanging;
            this.FileItemsRepeater_Small.ElementPrepared -= FileItemsRepeater_Large_ElementPrepared;
            this.FileItemsRepeater_Midium.ElementPrepared -= FileItemsRepeater_Large_ElementPrepared;
            this.FileItemsRepeater_Large.ElementPrepared -= FileItemsRepeater_Large_ElementPrepared;

            args.ItemContainer.Focus(FocusState.Keyboard);
        }

        #endregion



        private void FileItemsRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            if (args.Element is FrameworkElement fe
                && fe.DataContext is StorageItemViewModel itemVM
                )
            {
                itemVM.Initialize();
            }
        }




        private void Image_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            var item = sender as FrameworkElement;
            item.Scale(1.020f, 1.020f, centerX: (float)item.ActualWidth * 0.5f, centerY: (float)item.ActualHeight * 0.5f, duration: 50)
                .Start();
        }

        private void Image_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            var item = sender as FrameworkElement;
            item.Scale(1.0f, 1.0f, centerX: (float)item.ActualWidth * 0.5f, centerY: (float)item.ActualHeight * 0.5f, duration: 50)
                .Start();
        }






        private void MenuFlyout_Opened(object sender, object e)
        {
            var flyout = sender as FlyoutBase;
            var pageVM = DataContext as FolderListupPageViewModel;
            if (flyout.Target is Control content)
            {
                var itemVM = (content.DataContext as StorageItemViewModel)
                    ?? (content as ContentControl)?.Content as StorageItemViewModel
                    ;
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
                OpenWithExplorerItem.Visibility = (itemVM.Item is StorageItemImageSource) ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                flyout.Hide();
                return;
            }
        }
    }
}
