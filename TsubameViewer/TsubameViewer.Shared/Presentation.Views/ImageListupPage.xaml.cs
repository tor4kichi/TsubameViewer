using Microsoft.Toolkit.Uwp.UI;
using Microsoft.Toolkit.Uwp.UI.Animations;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Toolkit.Uwp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using TsubameViewer.Presentation.ViewModels;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using TsubameViewer.Presentation.Views.Helpers;
using Uno.Threading;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Numerics;
using Microsoft.Toolkit.Mvvm.Input;
using Windows.UI.Xaml.Media.Animation;
using System.Windows.Input;
using System.Threading.Tasks;

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
            InitializeComponent();

            Loaded += FolderListupPage_Loaded;
            Unloaded += FolderListupPage_Unloaded;

            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            var oldViewModel = _vm;
            _vm = args.NewValue as ImageListupPageViewModel;
            if (_vm != null && oldViewModel != _vm)
            {
                this.Bindings.Update();
            }
        }

        private ImageListupPageViewModel _vm { get; set; }


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

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            var currentFocus = FocusManager.GetFocusedElement();
            if (currentFocus is FrameworkElement fe 
                && fe.DataContext is StorageItemViewModel itemVM)
            {
                _PathToLastIntractMap[_vm.DisplayCurrentPath] = 
                    new LastIntractInfo()
                    { 
                        ItemPath = itemVM.Path, 
                        ScrollPositionRatio = ItemsScrollViewer.VerticalOffset / ItemsScrollViewer.ScrollableHeight
                    };

                _vm.SetLastIntractItem(itemVM);
            }
            else
            {
                _PathToLastIntractMap[_vm.DisplayCurrentPath] =
                    new LastIntractInfo()
                    {
                        ItemPath = null,
                        ScrollPositionRatio = ItemsScrollViewer.VerticalOffset / ItemsScrollViewer.ScrollableHeight
                    };

            }

            base.OnNavigatingFrom(e);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.NavigationMode == NavigationMode.New)
            {
                var settings = new Models.Domain.FolderItemListing.FolderListingSettings();
                if (settings.IsForceEnableXYNavigation
                    || Xamarin.Essentials.DeviceInfo.Idiom == Xamarin.Essentials.DeviceIdiom.TV
                    )
                {
                    if (GetCurrentDisplayItemsRepeater() is not null and var currentItemsRepeater 
                        && currentItemsRepeater.ItemsSource != null
                        && currentItemsRepeater.FindDescendant<Control>() is not null and var firstItem
                        )
                    {
                        firstItem.Focus(FocusState.Keyboard);
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
            else
            {
                BringIntoViewLastIntractItem();
            }
        }

        private void FileItemsRepeater_Large_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            this.FileItemsRepeater_Small.ElementPrepared -= FileItemsRepeater_Large_ElementPrepared;
            this.FileItemsRepeater_Midium.ElementPrepared -= FileItemsRepeater_Large_ElementPrepared;
            this.FileItemsRepeater_Large.ElementPrepared -= FileItemsRepeater_Large_ElementPrepared;

            (args.Element as Control).Focus(FocusState.Keyboard);
        }


        private ItemsRepeater GetCurrentDisplayItemsRepeater()
        {
            if (FileItemsRepeater_Small.Visibility == Visibility.Visible) { return FileItemsRepeater_Small; }
            else if (FileItemsRepeater_Midium.Visibility == Visibility.Visible) { return FileItemsRepeater_Midium; }
            else if (FileItemsRepeater_Large.Visibility == Visibility.Visible) { return FileItemsRepeater_Large; }
            else { return null; }
        }

        Dictionary<string, LastIntractInfo> _PathToLastIntractMap = new();
        public async void BringIntoViewLastIntractItem()
        {
            while (_vm == null || _vm.NowProcessing)
            {
                await Task.Delay(10);
            }

            async void SetFocusToItem(StorageItemViewModel itemVM, bool withScroll = false)
            {
                var lastIntractItemIndex = _vm.FileItemsView.IndexOf(itemVM);
                if (lastIntractItemIndex > 0)
                {
                    UIElement lastIntractItem = null;
                    var currentItemsRepeater = GetCurrentDisplayItemsRepeater();
                    foreach (int count in Enumerable.Range(0, 10))                    
                    {
                        lastIntractItem = currentItemsRepeater.TryGetElement(lastIntractItemIndex);
                        if (lastIntractItem is not null)
                        {
                            break;
                        }
                        await Task.Delay(100);
                    }

                    if (lastIntractItem is Control control) 
                    {
                        if (withScroll)
                        {
                            var transform = lastIntractItem.TransformToVisual(ItemsScrollViewer);
                            var pt = transform.TransformPoint(new Point(0, 0));
                            ItemsScrollViewer.ChangeView(null, pt.Y, null);

                            await Task.Delay(100);
                        }

                        control.Focus(FocusState.Keyboard);
                    }
                }
            }

            if (_PathToLastIntractMap.Remove(_vm.DisplayCurrentPath, out LastIntractInfo info) is false)
            {
                if (_vm.GetLastIntractItem() is not null and var lastIntractItemVM)
                {
                    SetFocusToItem(lastIntractItemVM, withScroll: true);                    
                }

                return;
            }

            ItemsScrollViewer.ChangeView(0, ItemsScrollViewer.ScrollableHeight * info.ScrollPositionRatio, 0);

            await Task.Delay(100);
            if (info.ItemPath is not null and String itemPath)
            {
                var lastIntractItemVM = _vm.ImageFileItems.FirstOrDefault(x => x.Path == itemPath);
                SetFocusToItem(lastIntractItemVM);
            }
        }

        struct LastIntractInfo
        {
            public string ItemPath;
            public double ScrollPositionRatio;
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

            AnimationBuilder.Create()
                .CenterPoint(new Vector2((float)item.ActualWidth * 0.5f, (float)item.ActualHeight * 0.5f), duration: TimeSpan.FromMilliseconds(1))
                .Scale(new Vector2(1.020f, 1.020f), duration: TimeSpan.FromMilliseconds(50))
                .Start(item);

            if (item.DataContext is StorageItemViewModel itemVM)
            {
                if (itemVM.Image?.IsAnimatedBitmap ?? false)
                {
                    itemVM.Image.Play();
                }
            }
        }

        
        private void Image_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            var item = sender as FrameworkElement;

            AnimationBuilder.Create()
                .CenterPoint(new Vector2((float)item.ActualWidth * 0.5f, (float)item.ActualHeight * 0.5f), duration: TimeSpan.FromMilliseconds(1))
                .Scale(new Vector2(1, 1), duration: TimeSpan.FromMilliseconds(50))
                .Start(item);

            if (item.DataContext is StorageItemViewModel itemVM)
            {
                if (itemVM.Image?.IsAnimatedBitmap ?? false)
                {
                    itemVM.Image.Stop();
                }
            }
            
        }



        RelayCommand<UIElement> _OpenItemCommand;
        public RelayCommand<UIElement> PrepareConnectedAnimationWithCurrentFocusElementCommand => _OpenItemCommand ??= new RelayCommand<UIElement>(item =>
        {
            var image = item.FindDescendantOrSelf<Image>();
            if (image.Source != null)
            {
                ConnectedAnimationService.GetForCurrentView()
                    .PrepareToAnimate("ImageJumpInAnimation", image);
            }
        });
    }


    public class ImageItemWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double ratioWH)
            {
                double itemHeight = (int)parameter;
                return itemHeight * ratioWH;
            }
            else 
            {
                return double.NaN;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException();
        }
    }
}
