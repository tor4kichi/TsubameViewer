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
using TsubameViewer.Presentation.Views.Helpers;

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

            this.FoldersAdaptiveGridView.ContainerContentChanging += FoldersAdaptiveGridView_ContainerContentChanging1;

            DataContextChanged += FolderListupPage_DataContextChanged;
        }

        private void FolderListupPage_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            var oldViewModel = _vm;
            _vm = args.NewValue as FolderListupPageViewModel;
            if (_vm != null && oldViewModel != _vm)
            {
                this.Bindings.Update();
            }
        }

        private FolderListupPageViewModel _vm { get; set; }

        private void FoldersAdaptiveGridView_ContainerContentChanging1(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.Item is StorageItemViewModel itemVM)
            {
                ToolTipService.SetToolTip(args.ItemContainer, new ToolTip() { Content = new TextBlock() { Text = itemVM .Name, TextWrapping = TextWrapping.Wrap } });
            }
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
                else
                {
                    ReturnSourceFolderPageButton.Focus(FocusState.Keyboard);

                    this.FoldersAdaptiveGridView.ContainerContentChanging -= FoldersAdaptiveGridView_ContainerContentChanging;
                    this.FoldersAdaptiveGridView.ContainerContentChanging += FoldersAdaptiveGridView_ContainerContentChanging;
                }
            }
            
        }

        private void FoldersAdaptiveGridView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            this.FoldersAdaptiveGridView.ContainerContentChanging -= FoldersAdaptiveGridView_ContainerContentChanging;

            args.ItemContainer.Focus(FocusState.Keyboard);
        }

        #endregion


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
                OpenListupItem.Command = (itemVM.Type == Models.Domain.StorageItemTypes.Archive) 
                    ? pageVM.OpenImageListupCommand 
                    : pageVM.OpenFolderItemCommand
                    ;
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






        public async void BringIntoViewLastIntractItem()
        {
            var pageVM = (DataContext as FolderListupPageViewModel);
            var lastIntaractItem = pageVM.FolderLastIntractItem.Value;
            if (lastIntaractItem != null)
            {
                if (lastIntaractItem.Type is not Models.Domain.StorageItemTypes.Image)
                {
                    FoldersAdaptiveGridView.ScrollIntoView(lastIntaractItem, ScrollIntoViewAlignment.Leading);

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
                            var sv = FoldersAdaptiveGridView.FindFirstChild<ScrollViewer>();
                            var transform = control.TransformToVisual(sv);
                            var positionInScrollViewer = transform.TransformPoint(new Point(0, 0));
                            //sv.ChangeView(null, positionInScrollViewer.Y + 64, null, true);
                            control.Focus(FocusState.Keyboard);
                        }
                    }
                }                
            }
            //else if (pageVM.ImageLastIntractItem.Value >= 1)
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
