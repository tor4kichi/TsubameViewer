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
using Windows.Storage;
using Uno.UI.Toolkit;

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






        private async void MenuFlyout_Opened(object sender, object e)
        {
            var flyout = sender as FlyoutBase;
            var pageVM = DataContext as FolderListupPageViewModel;

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

            OpenImageViewerItem.CommandParameter = itemVM;
            OpenImageViewerItem.Command = pageVM.OpenImageViewerCommand;
            if (itemVM.Type == Models.Domain.StorageItemTypes.Folder)
            {
                var folderContainerType = await pageVM.FolderContainerTypeManager.GetFolderContainerType((itemVM.Item as StorageItemImageSource).StorageItem as StorageFolder);
                OpenImageViewerItem.Visibility = folderContainerType == Models.Domain.FolderItemListing.FolderContainerType.OnlyImages 
                    ? Visibility.Visible 
                    : Visibility.Collapsed
                    ;
            }
            else
            {
                OpenImageViewerItem.Visibility = Visibility.Visible;
            }

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




        public async void BringIntoViewLastIntractItem()
        {
            var pageVM = (DataContext as FolderListupPageViewModel);
            var lastIntaractItem = pageVM.FolderLastIntractItem.Value;
            if (lastIntaractItem != null)
            {
                DependencyObject item;
                do
                {
                    item = FoldersAdaptiveGridView.ContainerFromItem(lastIntaractItem);

                    await Task.Delay(10);
                }
                while (item == null);

                if (item is Control control)
                {
                    var transform = control.TransformToVisual(RootScrollViewer);
                    var positionInScrollViewer = transform.TransformPoint(new Point(0, 0));
                    RootScrollViewer.ChangeView(null, positionInScrollViewer.Y, null, true);
                    control.Focus(FocusState.Keyboard);
                }
            }
            else if (pageVM.ImageLastIntractItem.Value >= 1)
            {
                // 実際にスクロールするまでItemTemplateは解決されない
                // 一旦Opacity=0.0に設定した上で要素が取れるまでプログラマチックにスクロールしていく
                // 要素が取れてスクロールが完了したらOpacity=1.0に戻す
                /*
                DependencyObject item;
                var visibleItemsRepeater = new[] { FileItemsRepeater_Line, FileItemsRepeater_Small, FileItemsRepeater_Midium, FileItemsRepeater_Large }.First(x => x.Visibility == Visibility.Visible);
                visibleItemsRepeater.Opacity = 0.0;
                RootScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
                double offset = 0;
                {
                    var transform = visibleItemsRepeater.TransformToVisual(RootScrollViewer);
                    var positionInScrollViewer = transform.TransformPoint(new Point(0, 0));
                    RootScrollViewer.ChangeView(null, positionInScrollViewer.Y, null, true);
                    offset = positionInScrollViewer.Y;
                }
                
                do
                {
                    item = visibleItemsRepeater.TryGetElement(pageVM.ImageLastIntractItem.Value);

                    RootScrollViewer.ChangeView(null, offset, null, true);

                    offset += RootScrollViewer.ViewportHeight;

                    await Task.Delay(10);
                }
                while (item == null);

                await Task.Delay(100);

                if (item is Control control)
                {
                    var transform = control.TransformToVisual(RootScrollViewer);
                    var positionInScrollViewer = transform.TransformPoint(new Point(0, 0));
                    control.Focus(FocusState.Keyboard);
                    RootScrollViewer.StartBringIntoView(new BringIntoViewOptions() { AnimationDesired = false });
//                    RootScrollViewer.ChangeView(null, positionInScrollViewer.Y, null, true);
                }

                visibleItemsRepeater.Opacity = 1.0;
                RootScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
                */
            }
        }

    }
}
