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
using System.Diagnostics;
using System.Threading;

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

        CancellationTokenSource _navigationCts;
      
        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            _navigationCts?.Cancel();
            _navigationCts?.Dispose();
            _navigationCts = null;

            if (e.SourcePageType != typeof(ImageViewerPage))
            {
                if (_vm.DisplayCurrentPath is not null)
                {
                    _vm.ClearLastIntractItem();
                }
            }

            base.OnNavigatingFrom(e);
        }


        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            _navigationCts?.Cancel();
            _navigationCts?.Dispose();
            _navigationCts = new CancellationTokenSource();
            var ct = _navigationCts.Token;
            ItemsScrollViewer.Opacity = 0.01;
            try
            {
                if (e.NavigationMode == NavigationMode.New)
                {
                    await ResetScrollPosition(ct);

                    if (IsRequireSetFocus())
                    {
                        var firstItem = await WaitTargetIndexItemLoadingAsync(0, ct);
                        if (firstItem != null)
                        {
                            (firstItem as Control)?.Focus(FocusState.Keyboard);
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
            finally
            {
                ItemsScrollViewer.Opacity = 1.0;
            }
        }


        private bool IsRequireSetFocus()
        {
            _FolderListingSettings ??= new Models.Domain.FolderItemListing.FolderListingSettings();
            return _FolderListingSettings.IsForceEnableXYNavigation
                || Xamarin.Essentials.DeviceInfo.Idiom == Xamarin.Essentials.DeviceIdiom.TV;
        }


        private void SaveScrollStatus(UIElement target)
        {
            if (target is FrameworkElement fe
                        && fe.DataContext is StorageItemViewModel itemVM)
            {
                _vm.SetLastIntractItem(itemVM);
            }
        }

        private async Task<UIElement> WaitTargetIndexItemLoadingAsync(int index, CancellationToken ct)
        {
            while (_vm == null || _vm.NowProcessing)
            {
                await Task.Delay(1, ct);
            }

            UIElement lastIntractItem = null;
            var currentItemsRepeater = GetCurrentDisplayItemsRepeater();
            foreach (int count in Enumerable.Range(0, 1000))
            {
                lastIntractItem = currentItemsRepeater.TryGetElement(index);
                if (lastIntractItem is not null)
                {
                    await Task.Delay(50, ct);

                    break;
                }

                await Task.Delay(1, ct);
            }
            return lastIntractItem;
        }


        private async Task ResetScrollPosition(CancellationToken ct)
        {
            var lastIntractItem = await WaitTargetIndexItemLoadingAsync(0, ct);
            ItemsScrollViewer.ChangeView(null, 0.0, null, disableAnimation: true);

            if (IsRequireSetFocus() && lastIntractItem is Control control)
            {
                control.Focus(FocusState.Keyboard);
            }
        }


        private void FileItemsRepeater_Large_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            this.FileItemsRepeater_Small.ElementPrepared -= FileItemsRepeater_Large_ElementPrepared;
            this.FileItemsRepeater_Midium.ElementPrepared -= FileItemsRepeater_Large_ElementPrepared;
            this.FileItemsRepeater_Large.ElementPrepared -= FileItemsRepeater_Large_ElementPrepared;

            (args.Element as Control)?.Focus(FocusState.Keyboard);
        }


        private ItemsRepeater GetCurrentDisplayItemsRepeater()
        {
            if (FileItemsRepeater_Small.Visibility == Visibility.Visible) { return FileItemsRepeater_Small; }
            else if (FileItemsRepeater_Midium.Visibility == Visibility.Visible) { return FileItemsRepeater_Midium; }
            else if (FileItemsRepeater_Large.Visibility == Visibility.Visible) { return FileItemsRepeater_Large; }
            else { return null; }
        }

        public async Task BringIntoViewLastIntractItem(CancellationToken ct)
        {
            while (_vm == null || _vm.NowProcessing)
            {
                await Task.Delay(1, ct);
            }

            if (_vm.DisplayCurrentPath == null)
            { 
                return;
            }

            if (_vm.GetLastIntractItem() is not null and var lastIntractItemVM)
            {
                var lastIntractItemIndex = _vm.FileItemsView.IndexOf(lastIntractItemVM);
                if (lastIntractItemIndex > 0)
                {
                    UIElement lastIntractItem = await WaitTargetIndexItemLoadingAsync(lastIntractItemIndex, ct);
                    if (lastIntractItem is Control control)
                    {
                        var transform = lastIntractItem.TransformToVisual(ItemsScrollViewer);
                        var pt = transform.TransformPoint(new Point(0, 0));
                        ItemsScrollViewer.ChangeView(null, pt.Y, null, disableAnimation: true);
                        
                        if (IsRequireSetFocus())
                        {
                            await Task.Delay(100, ct);

                            control.Focus(FocusState.Keyboard);
                        }
                    }
                }
            }
        }

        struct LastIntractInfo
        {
            public string ItemPath;
            public double? ScrollPositionRatio;
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


        private readonly AnimationBuilder _zoomUpAnimation = AnimationBuilder.Create()
                .Scale(new Vector2(1.020f, 1.020f), duration: TimeSpan.FromMilliseconds(50));

        private readonly AnimationBuilder _zoomDownAnimation = AnimationBuilder.Create()
                .Scale(new Vector2(1, 1), duration: TimeSpan.FromMilliseconds(50));

        private void Image_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            var item = sender as FrameworkElement;

            _zoomUpAnimation
                .CenterPoint(new Vector2((float)item.ActualWidth * 0.5f, (float)item.ActualHeight * 0.5f), duration: TimeSpan.FromMilliseconds(1))
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

            _zoomDownAnimation
                .CenterPoint(new Vector2((float)item.ActualWidth * 0.5f, (float)item.ActualHeight * 0.5f), duration: TimeSpan.FromMilliseconds(1))
                .Start(item);

            if (item.DataContext is StorageItemViewModel itemVM)
            {
                if (itemVM.Image?.IsAnimatedBitmap ?? false)
                {
                    itemVM.Image.Stop();
                }
            }
            
        }

        RelayCommand<TappedRoutedEventArgs> _PrepareConnectedAnimationWithTappedItemCommand;
        public RelayCommand<TappedRoutedEventArgs> PrepareConnectedAnimationWithTappedItemCommand => _PrepareConnectedAnimationWithTappedItemCommand ??= new RelayCommand<TappedRoutedEventArgs>(item =>
        {
            var image = (item.OriginalSource as UIElement).FindDescendantOrSelf<Image>();
            if (image?.Source != null)
            {
                var anim = ConnectedAnimationService.GetForCurrentView()
                    .PrepareToAnimate(PageTransisionHelper.ImageJumpConnectedAnimationName, image);
                anim.Configuration = new BasicConnectedAnimationConfiguration();
            }

            SaveScrollStatus(item.OriginalSource as UIElement);
        });

        RelayCommand<UIElement> _OpenItemCommand;
        private Models.Domain.FolderItemListing.FolderListingSettings _FolderListingSettings;

        public RelayCommand<UIElement> PrepareConnectedAnimationWithCurrentFocusElementCommand => _OpenItemCommand ??= new RelayCommand<UIElement>(item =>
        {
            var image = item.FindDescendantOrSelf<Image>();
            if (image?.Source != null)
            {                
                var anim = ConnectedAnimationService.GetForCurrentView()
                    .PrepareToAnimate(PageTransisionHelper.ImageJumpConnectedAnimationName, image);
                anim.Configuration = new BasicConnectedAnimationConfiguration();                
            }

            SaveScrollStatus(item);
        });
    }


    public class ImageItemWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double ratioWH)
            {
                int itemHeight = (int)parameter;
                return (double)(itemHeight * (float)ratioWH);
            }
            else if (value is float ratioWHf)
            {
                int itemHeight = (int)parameter;
                return (double)(itemHeight * ratioWHf);
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
