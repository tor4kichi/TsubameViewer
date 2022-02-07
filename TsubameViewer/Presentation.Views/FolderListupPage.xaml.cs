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
using Xamarin.Essentials;
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
using Microsoft.Toolkit.Uwp.UI;
using Microsoft.Toolkit.Mvvm.Input;
using System.Windows.Input;
using Windows.UI.Xaml.Media.Animation;

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

        CancellationTokenSource _navigationCts;

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            _navigationCts?.Cancel();
            _navigationCts?.Dispose();
            _navigationCts = null;

            if (_vm.DisplayCurrentPath != null) 
            {
                try
                {
                    var sv = FoldersAdaptiveGridView.FindFirstChild<ScrollViewer>();
                    var ratio = sv.VerticalOffset / sv.ScrollableHeight;
                    _PathToLastScrollPosition[_vm.DisplayCurrentPath] = ratio;

                    Debug.WriteLine(ratio);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                }
            }

            base.OnNavigatingFrom(e);
        }
        #region 初期フォーカス設定

        private bool IsRequireSetFocus()
        {
            return Xamarin.Essentials.DeviceInfo.Idiom == Xamarin.Essentials.DeviceIdiom.TV
                || Microsoft.Toolkit.Uwp.Helpers.SystemInformation.Instance.DeviceFamily == "Windows.Xbox"
                || UINavigation.UINavigationManager.NowControllerConnected
                ;
        }



        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            _navigationCts?.Cancel();
            _navigationCts?.Dispose();
            _navigationCts = new CancellationTokenSource();
            var ct = _navigationCts.Token;

            try
            {
                ConnectedAnimationService.GetForCurrentView()
                            .GetAnimation(PageTransitionHelper.ImageJumpConnectedAnimationName)?.Cancel();

                base.OnNavigatedTo(e);

                var settings = new Models.Domain.FolderItemListing.FolderListingSettings();
                if (e.NavigationMode == Windows.UI.Xaml.Navigation.NavigationMode.New)
                {
                    if (IsRequireSetFocus())
                    {
                        await FoldersAdaptiveGridView.WaitFillingValue(x => x.Items.Any(), ct);
                        var firstItem = FoldersAdaptiveGridView.Items.First();
                        if (firstItem is not null)
                        {
                            await FoldersAdaptiveGridView.WaitFillingValue(x => FoldersAdaptiveGridView.ContainerFromItem(firstItem) != null, ct);
                            var itemContainer = FoldersAdaptiveGridView.ContainerFromItem(firstItem) as Control;

                            await Task.Delay(50);
                            itemContainer.Focus(FocusState.Keyboard);
                        }
                        else
                        {
                            ReturnSourceFolderPageButton.Focus(FocusState.Keyboard);
                        }
                    }
                }
                else
                {
                    await BringIntoViewLastIntractItem(ct);
                }
            }
            catch (OperationCanceledException) { }
        }

        #endregion

        // 前回スクロール位置への復帰に対応する
        // valueはスクロール位置のスクロール可能範囲に対する割合で示される 0.0 ~ 1.0 の範囲の値
        Dictionary<string, double> _PathToLastScrollPosition = new();

        public void DeselectItem()
        {
            FoldersAdaptiveGridView.DeselectAll();
        }

        public async Task BringIntoViewLastIntractItem(CancellationToken ct)
        {
            await this.WaitFillingValue(x => x._vm != null && x._vm.NowProcessing is false, ct);

            var lastIntaractItem = _vm.GetLastIntractItem();
            if (lastIntaractItem != null)
            {
                if (lastIntaractItem.Type is not Models.Domain.StorageItemTypes.Image)
                {
                    FoldersAdaptiveGridView.ScrollIntoView(lastIntaractItem, ScrollIntoViewAlignment.Leading);

                    // 並び替えを伴う場合にスクロール位置がズレる問題を回避するためDelayを入れてる                    
                    DependencyObject item;
                    do
                    {
                        item = FoldersAdaptiveGridView.ContainerFromItem(lastIntaractItem);

                        await Task.Delay(1, ct);
                    }
                    while (item == null);

                    if (item is Control control)
                    {
                        var sv = FoldersAdaptiveGridView.FindFirstChild<ScrollViewer>();
                        if (_PathToLastScrollPosition.TryGetValue(_vm.DisplayCurrentPath, out double ratio) && double.IsNaN(ratio) is false)
                        {
                            sv.ChangeView(null, sv.ScrollableHeight * ratio, null, true);
                        }

                        await Task.Delay(50, ct);
                        control.Focus(FocusState.Keyboard);
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


        void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FoldersAdaptiveGridView.SelectionMode == ListViewSelectionMode.None)
            {                
                return;
            }

            if (e.AddedItems?.Any() ?? false)
            {
                _vm.Selection.SelectedItems.AddRange(e.AddedItems.Cast<StorageItemViewModel>());
            }
            
            if (e.RemovedItems?.Any() ?? false)
            {
                foreach (var itemVM in e.RemovedItems.Cast<StorageItemViewModel>())
                {
                    _vm.Selection.SelectedItems.Remove(itemVM);
                }

                if (FoldersAdaptiveGridView.SelectedItems.Count == 0)
                {
                    _vm.Selection.EndSelection();
                }
            }
        }


        RelayCommand<object> _SelectionChangeCommand;
        public RelayCommand<object> SelectionChangeCommand => _SelectionChangeCommand ??= new RelayCommand<object>(item =>
        {
            if (item is StorageItemViewModel itemVM)
            {
                if (_vm.Selection.IsSelectionModeEnabled is false)
                {
                    _vm.Selection.StartSelection();
                }

                if (FoldersAdaptiveGridView.SelectedItems.Any(x => x == itemVM))
                {
                    FoldersAdaptiveGridView.SelectedItems.Remove(itemVM);
                }
                else
                {
                    FoldersAdaptiveGridView.SelectedItems.Add(itemVM);
                }
            }
        });

        RelayCommand<StorageItemViewModel> _OpenItemCommand;
        public RelayCommand<StorageItemViewModel> OpenItemCommand => _OpenItemCommand ??= new RelayCommand<StorageItemViewModel>(itemVM => 
        {                                    
            if (_vm.Selection.IsSelectionModeEnabled is false
                && ((uint)Window.Current.CoreWindow.GetKeyState(Windows.System.VirtualKey.Control) & 0x01) != 0)
            {
                _vm.Selection.StartSelection();
                return;
            }

            if (FoldersAdaptiveGridView.SelectionMode != ListViewSelectionMode.None)
            {
                return;
            }

            var container = FoldersAdaptiveGridView.ContainerFromItem(itemVM);
            if (container is GridViewItem gvi)
            {
                var image = gvi.ContentTemplateRoot.FindDescendant<Image>();
                if (image.Source != null)
                {
                    //ConnectedAnimationService.GetForCurrentView()
                    //    .PrepareToAnimate(PageTransisionHelper.ImageJumpConnectedAnimationName, image);
                }
            }

            (_vm.OpenFolderItemCommand as ICommand).Execute(itemVM);
        });
    }
}
