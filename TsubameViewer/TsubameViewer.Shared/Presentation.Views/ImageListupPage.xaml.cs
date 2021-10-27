using Microsoft.Toolkit.Uwp.UI.Animations;
using Microsoft.Toolkit.Uwp.UI.Extensions;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using TsubameViewer.Presentation.ViewModels;
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

// 空白ページの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234238 を参照してください

namespace TsubameViewer.Presentation.Views
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class ImageListupPage : Page
    {
        public ImageListupPage()
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

            FileItemsRepeater_Small.ElementClearing += FileItemsRepeater_Large_ElementClearing;
            FileItemsRepeater_Midium.ElementClearing += FileItemsRepeater_Large_ElementClearing;
            FileItemsRepeater_Large.ElementClearing += FileItemsRepeater_Large_ElementClearing;
        }

        private void FolderListupPage_Unloaded(object sender, RoutedEventArgs e)
        {
            FileItemsRepeater_Small.ElementPrepared -= FileItemsRepeater_ElementPrepared;
            FileItemsRepeater_Midium.ElementPrepared -= FileItemsRepeater_ElementPrepared;
            FileItemsRepeater_Large.ElementPrepared -= FileItemsRepeater_ElementPrepared;

            FileItemsRepeater_Small.ElementClearing -= FileItemsRepeater_Large_ElementClearing;
            FileItemsRepeater_Midium.ElementClearing -= FileItemsRepeater_Large_ElementClearing;
            FileItemsRepeater_Large.ElementClearing -= FileItemsRepeater_Large_ElementClearing;
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
                if (FileItemsRepeater_Small.ItemsSource != null && FileItemsRepeater_Small.Visibility == Visibility.Visible)
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

                    this.FileItemsRepeater_Small.ElementPrepared -= FileItemsRepeater_Large_ElementPrepared;
                    this.FileItemsRepeater_Midium.ElementPrepared -= FileItemsRepeater_Large_ElementPrepared;
                    this.FileItemsRepeater_Large.ElementPrepared -= FileItemsRepeater_Large_ElementPrepared;

                    this.FileItemsRepeater_Small.ElementPrepared += FileItemsRepeater_Large_ElementPrepared;
                    this.FileItemsRepeater_Midium.ElementPrepared += FileItemsRepeater_Large_ElementPrepared;
                    this.FileItemsRepeater_Large.ElementPrepared += FileItemsRepeater_Large_ElementPrepared;
                }
            }

        }

        private void FileItemsRepeater_Large_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            this.FileItemsRepeater_Small.ElementPrepared -= FileItemsRepeater_Large_ElementPrepared;
            this.FileItemsRepeater_Midium.ElementPrepared -= FileItemsRepeater_Large_ElementPrepared;
            this.FileItemsRepeater_Large.ElementPrepared -= FileItemsRepeater_Large_ElementPrepared;

            (args.Element as Control).Focus(FocusState.Keyboard);
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

        private void FileItemsRepeater_Large_ElementClearing(ItemsRepeater sender, ItemsRepeaterElementClearingEventArgs args)
        {
            if (args.Element is FrameworkElement fe
               && fe.DataContext is StorageItemViewModel itemVM
               )
            {
                itemVM.StopImageLoading();
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



        // {StaticResource FolderAndArchiveMenuFlyout} で指定すると表示されない不具合がある
        // 原因は Microsoft.Xaml.UI にありそうだけど特定はしてない。
        // （2.4.2から2.5.0 preに変更したところで問題が起きるようになった）
        private void FoldersAdaptiveGridView_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = Resources["FolderAndArchiveMenuFlyout"] as FlyoutBase;
            flyout.ShowAt(args.OriginalSource as FrameworkElement);
        }

        private void FolderAndArchiveMenuFlyout_Opened(object sender, object e)
        {
            var flyout = sender as FlyoutBase;
            var pageVM = DataContext as ImageListupPageViewModel;

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

            if (itemVM.Item is StorageItemImageSource == false)
            {
                NoActionDescMenuItem.Visibility = Visibility.Visible;

                OpenListupItem.Visibility = Visibility.Collapsed;
                AddSecondaryTile.Visibility = Visibility.Collapsed;
                RemoveSecondaryTile.Visibility = Visibility.Collapsed;
                OpenWithExplorerItem.Visibility = Visibility.Collapsed;
                FolderAndArchiveMenuSeparator1.Visibility = Visibility.Collapsed;
                FolderAndArchiveMenuSeparator2.Visibility = Visibility.Collapsed;
            }
            else
            {
                OpenListupItem.CommandParameter = itemVM;
                OpenListupItem.Command = pageVM.OpenFolderListupCommand;
                OpenListupItem.Visibility = (itemVM.Type == Models.Domain.StorageItemTypes.Archive || itemVM.Type == Models.Domain.StorageItemTypes.Folder)
                    ? Visibility.Visible
                    : Visibility.Collapsed
                    ;
                FolderAndArchiveMenuSeparator1.Visibility = OpenListupItem.Visibility;

                AddSecondaryTile.CommandParameter = itemVM;
                AddSecondaryTile.Command = pageVM.SecondaryTileAddCommand;
                AddSecondaryTile.Visibility = !pageVM.SecondaryTileManager.ExistTile(itemVM.Path)
                    ? Visibility.Visible
                    : Visibility.Collapsed
                    ;

                RemoveSecondaryTile.CommandParameter = itemVM;
                RemoveSecondaryTile.Command = pageVM.SecondaryTileRemoveCommand;
                RemoveSecondaryTile.Visibility = pageVM.SecondaryTileManager.ExistTile(itemVM.Path)
                    ? Visibility.Visible
                    : Visibility.Collapsed
                    ;

                FolderAndArchiveMenuSeparator2.Visibility = Visibility.Visible;

                OpenWithExplorerItem.CommandParameter = itemVM;
                OpenWithExplorerItem.Command = pageVM.OpenWithExplorerCommand;
                OpenWithExplorerItem.Visibility = Visibility.Visible;
                ;

                NoActionDescMenuItem.Visibility = Visibility.Collapsed;
            }
        }
    }
}
