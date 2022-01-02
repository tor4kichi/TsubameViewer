using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Uwp.UI.Animations;
using Microsoft.UI.Xaml.Controls;
using Prism.Commands;
using Prism.Ioc;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using TsubameViewer.Presentation.ViewModels;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using TsubameViewer.Presentation.Views.UINavigation;
using Uno.Disposables;
using Uno.Extensions;
using Uno.Extensions.Specialized;
using Uno.Threading;
using Windows.ApplicationModel.Core;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// 空白ページの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234238 を参照してください

namespace TsubameViewer.Presentation.Views
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class ImageViewerPage : Page
    {
        private ImageViewerPageViewModel _vm { get; set; }

        private readonly DispatcherQueue _dispatcherQueue;
        public ImageViewerPage()
        {
            this.InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            DataContextChanged += OnDataContextChanged;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            var oldViewModel = _vm;
            _vm = args.NewValue as ImageViewerPageViewModel;
            if (_vm != null && oldViewModel != _vm)
            {
                this.Bindings.Update();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            SystemNavigationManager.GetForCurrentView().BackRequested -= ImageViewerPage_BackRequested;

            IntaractionWall.Tapped -= SwipeProcessScreen_Tapped;
            IntaractionWall.ManipulationDelta -= ImagesContainer_ManipulationDelta;
            IntaractionWall.ManipulationStarted -= IntaractionWall_ManipulationStarted;
            IntaractionWall.ManipulationCompleted -= IntaractionWall_ManipulationCompleted;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            CloseBottomUI();

            IntaractionWall.ManipulationMode = ManipulationModes.Scale | ManipulationModes.TranslateX | ManipulationModes.TranslateY;
            IntaractionWall.Tapped += SwipeProcessScreen_Tapped;
            IntaractionWall.ManipulationDelta += ImagesContainer_ManipulationDelta;

            IntaractionWall.ManipulationStarted += IntaractionWall_ManipulationStarted;
            IntaractionWall.ManipulationCompleted += IntaractionWall_ManipulationCompleted;

            SystemNavigationManager.GetForCurrentView().BackRequested += ImageViewerPage_BackRequested;
        }

        public bool IsReadyToImageDisplay
        {
            get { return (bool)GetValue(IsReadyToImageDisplayProperty); }
            set { SetValue(IsReadyToImageDisplayProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsReadyToImageDisplay.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsReadyToImageDisplayProperty =
            DependencyProperty.Register("IsReadyToImageDisplay", typeof(bool), typeof(ImageViewerPage), new PropertyMetadata(false));




        #region Navigation

        private void ImageViewerPage_BackRequested(object sender, BackRequestedEventArgs e)
        {
            if (IsOpenBottomMenu)
            {
                ToggleOpenCloseBottomUI();
            }
            else
            {
                (_vm.BackNavigationCommand as ICommand).Execute(null);
            }
        }

        CompositeDisposable _navigationDisposables;
        CancellationTokenSource _navigaitonCts;
        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            _navigationDisposables.Dispose();

            _navigaitonCts.Cancel();
            _navigaitonCts.Dispose();
            _navigaitonCts = null;

            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = false;
            Window.Current.SetTitleBar(null);
            Windows.UI.Core.SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = Windows.UI.Core.AppViewBackButtonVisibility.Visible;

            var appView = ApplicationView.GetForCurrentView();
            appView.TitleBar.ButtonBackgroundColor = null;
            appView.TitleBar.ButtonHoverBackgroundColor = null;
            appView.TitleBar.ButtonInactiveBackgroundColor = null;
            appView.TitleBar.ButtonPressedBackgroundColor = null;

            appView.ExitFullScreenMode();

            PrimaryWindowCoreLayout.IsPreventSystemBackNavigation = false;

            base.OnNavigatingFrom(e);
        }


        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            IsReadyToImageDisplay = false;
            _navigationDisposables = new CompositeDisposable();

            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;

            Window.Current.SetTitleBar(DraggableTitleBarArea_Desktop);

            Windows.UI.Core.SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = Windows.UI.Core.AppViewBackButtonVisibility.Collapsed;

            var appView = ApplicationView.GetForCurrentView();
            appView.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            appView.TitleBar.ButtonHoverBackgroundColor = Color.FromArgb(0x7f, 0xff, 0xff, 0xff);
            appView.TitleBar.ButtonInactiveBackgroundColor = Color.FromArgb(0xcf, 0xff, 0xff, 0xff);
            appView.TitleBar.ButtonPressedBackgroundColor = Color.FromArgb(0x9f, 0xff, 0xff, 0xff);

            PrimaryWindowCoreLayout.IsPreventSystemBackNavigation = true;

            _navigaitonCts = new CancellationTokenSource();
            _ = StartNavigatedAnimationAsync(_navigaitonCts.Token);

            await WaitImageLoadingAsync(_navigaitonCts.Token);

            _navigationDisposables.Add(InitializeZoomReaction());

            base.OnNavigatedTo(e);
        }

        private async Task StartNavigatedAnimationAsync(CancellationToken navigationCt)
        {
            AnimationBuilder.Create()
                .Opacity(0.001, duration: TimeSpan.FromMilliseconds(1))
                .Start(ImageItemsControl_0);

            bool isConnectedAnimationDone = false;
            var connectedAnimationService = ConnectedAnimationService.GetForCurrentView();
            ConnectedAnimation animation = connectedAnimationService.GetAnimation(PageTransisionHelper.ImageJumpConnectedAnimationName);
            if (animation != null)
            {
                try
                {
                    isConnectedAnimationDone = await TryStartSingleImageAnimationAsync(animation, navigationCt);
                    if (isConnectedAnimationDone)
                    {
                        // ConnectedAnimation中に依存プロパティを変更してしまうと
                        // VisualState.StateTriggers が更新されないので待機する
                        await Task.Delay(connectedAnimationService.DefaultDuration + TimeSpan.FromMilliseconds(150));
                    }
                }
                catch (OperationCanceledException) { }
            }

            try
            {
                if (isConnectedAnimationDone is false)
                {
                    await WaitImageLoadingAsync(navigationCt);

                    await AnimationBuilder.Create()
                       .CenterPoint(ImagesContainer.ActualSize * 0.5f, duration: TimeSpan.FromMilliseconds(1))
                       .Scale()
                           .TimedKeyFrames(ke =>
                           {
                               ke.KeyFrame(TimeSpan.FromMilliseconds(0), new(0.95f));
                               ke.KeyFrame(TimeSpan.FromMilliseconds(150), new(1.0f));
                           })
                       .Opacity(1.0, delay: TimeSpan.FromMilliseconds(10), duration: TimeSpan.FromMilliseconds(250))
                       .StartAsync(ImageItemsControl_0, navigationCt);
                }
            }
            catch (OperationCanceledException) { }

            _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, () => IsReadyToImageDisplay = true);            
        }

        private async Task<bool> TryStartSingleImageAnimationAsync(ConnectedAnimation animation, CancellationToken navigationCt)
        {
            bool isConnectedAnimationDone = false;
            CancellationTokenSource timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(750));
            try
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, navigationCt);

                var ct = linkedCts.Token;


                if (await WaitImageLoadingAsync(ct) is not null and var images && images.Count() == 1)
                {
                    // ConnectedAnimation.Start後にタイムアウトでフォールバックのアニメーションが起動する可能性に配慮が必要

                    isConnectedAnimationDone = true;
                    animation.TryStart(images.ElementAt(0));
                    AnimationBuilder.Create()
                        .Opacity(1.0, duration: TimeSpan.FromMilliseconds(1))
                        .Start(ImageItemsControl_0);
                }
                else
                {
                    animation.Cancel();
                }

            }
            catch (OperationCanceledException oce) when (oce.CancellationToken != navigationCt && isConnectedAnimationDone is false)
            {
                animation.Cancel();
                throw;
            }
            catch (OperationCanceledException oce) when (oce.CancellationToken == navigationCt)
            {
                animation.Cancel();
                throw;
            }
            finally
            {
                timeoutCts.Dispose();
            }

            return isConnectedAnimationDone;
        }

        private async Task<IEnumerable<UIElement>> WaitImageLoadingAsync(CancellationToken ct)
        {
            if (_vm == null)
            {
                await this.ObserveDependencyProperty(DataContextProperty)
                       .Where(x => _vm is not null)
                       .Take(1)
                       .ToAsyncOperation()
                       .AsTask(ct);

                await _vm.ObserveProperty(x => x.DisplayImages_0, isPushCurrentValueAtFirst: false)
                    .Take(1)
                    .ToAsyncOperation()
                    .AsTask(ct);
            }

            if (_vm.DisplayImages_0.Length == 1)
            {
                UIElement image = null;
                if (ImageItemsControl_0.TryGetElement(0) is not null and var readyImage)
                {
                    image = readyImage;
                }
                else
                {
                    image = await WindowsObservable.FromEventPattern<ItemsRepeater, ItemsRepeaterElementPreparedEventArgs>(
                      h => ImageItemsControl_0.ElementPrepared += h,
                      h => ImageItemsControl_0.ElementPrepared -= h
                      )
                      .Select(x => x.EventArgs.Element)
                      .Take(1)
                      .ToAsyncOperation()
                      .AsTask(ct);

                }

                while (image.ActualSize is { X: 0, Y: 0 })
                {
                    await Task.Delay(1, ct);
                }

                return new[] { image };
            }
            else
            {
                IList<UIElement> images = null;
                if (ImageItemsControl_0.TryGetElement(0) is not null and var readyImage1
                    && ImageItemsControl_0.TryGetElement(1) is not null and var readyImage2
                    )
                {
                    images = new[] { readyImage1, readyImage2 };
                }
                else
                {
                    images = await WindowsObservable.FromEventPattern<ItemsRepeater, ItemsRepeaterElementPreparedEventArgs>(
                           h => ImageItemsControl_0.ElementPrepared += h,
                           h => ImageItemsControl_0.ElementPrepared -= h
                           )
                           .Select(x => x.EventArgs.Element)
                           .Take(2)
                           .Buffer(2)
                           .ToAsyncOperation()
                           .AsTask(ct);
                }

                while (images.All(image => image.ActualSize is { X: 0, Y: 0 }))
                {
                    await Task.Delay(1, ct);
                }

                return images;
            }
        }

        #endregion Navigation


        #region Touch and Controller UI

        private void SwipeProcessScreen_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (!_nowZoomCenterMovingWithPointer)
            {
                var pt = e.GetPosition(RootGrid);                
                if (VisualTreeHelper.FindElementsInHostCoordinates(pt, ButtonsContainer).Any()) { return; }
                if (VisualTreeHelper.FindElementsInHostCoordinates(pt, ImageSelectorContainer).Any()) { return; }

                if (!IsOpenBottomMenu && !IsZoomingEnabled)
                {
                    var uiItems = VisualTreeHelper.FindElementsInHostCoordinates(pt, UIContainer);
                    foreach (var item in uiItems)
                    {
                        if (item == RightPageMoveButton)
                        {
                            if (RightPageMoveButton.Command?.CanExecute(null) ?? false)
                            {
                                RightPageMoveButton.Command.Execute(null);
                                e.Handled = true;
                                break;
                            }
                        }
                        else if (item == LeftPageMoveButton)
                        {
                            if (LeftPageMoveButton.Command?.CanExecute(null) ?? false)
                            {
                                LeftPageMoveButton.Command.Execute(null);
                                e.Handled = true;
                                break;
                            }
                        }
                        else if (item == ToggleBottomMenuButton)
                        {
                            if (ToggleBottomMenuButton.Command?.CanExecute(null) ?? false)
                            {
                                ToggleBottomMenuButton.Command.Execute(null);
                                e.Handled = true;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    ToggleOpenCloseBottomUI();
                    e.Handled = true;
                }
            }
        }

        private string ToPercentage(double val)
        {
            return (val * 100).ToString("F0");
        }

        private void ShowBottomUI()
        {
            IsOpenBottomMenu = true;
            ButtonsContainer.Visibility = Visibility.Visible;
            ImageSelectorContainer.Visibility = Visibility.Visible;
            ZoomInButton.Focus(FocusState.Keyboard);
        }

        private void CloseBottomUI()
        {
            IsOpenBottomMenu = false;
            ButtonsContainer.Visibility = Visibility.Collapsed;
            ImageSelectorContainer.Visibility = Visibility.Collapsed;
        }



        // コントローラー操作用
        public void ToggleOpenCloseBottomUI()
        {
            if (IsOpenBottomMenu == false)
            {
                ShowBottomUI();
            }
            else
            {
                CloseBottomUI();
            }
        }

        private DelegateCommand _toggleBottomMenuCommand;
        public DelegateCommand ToggleBottomMenuCommand =>
            _toggleBottomMenuCommand ?? (_toggleBottomMenuCommand = new DelegateCommand(ExecuteToggleBottomMenuCommand, () => true) { IsActive = true });

        void ExecuteToggleBottomMenuCommand()
        {
            ToggleOpenCloseBottomUI();
        }

        public bool IsOpenBottomMenu
        {
            get { return (bool)GetValue(IsOpenBottomMenuProperty); }
            set { SetValue(IsOpenBottomMenuProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsOpenBottomMenu.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsOpenBottomMenuProperty =
            DependencyProperty.Register("IsOpenBottomMenu", typeof(bool), typeof(ImageViewerPage), new PropertyMetadata(false));



        #endregion


        #region ZoomInOut


        private const float MaxZoomFactor = 8.0f;
        private const float MinZoomFactor = 0.5f;        

        private static readonly float[] ZoomFactorList = Enumerable.Concat(
            new[] { 0.5f, .75f }, 
            new[] { 1.0f, 1.5f, 2.0f, 4.0f, 8f, 16f, 32f }
            ).ToArray();

        private int CurrentZoomFactorIndex;
        private static readonly TimeSpan DefaultZoomingDuration = TimeSpan.FromMilliseconds(150);
        private readonly AnimationBuilder ZoomCenterAb = AnimationBuilder.Create();

        private const float ControlerZoomCenterMoveAmount = 100.0f;
        float GetZoomCenterMoveingFactorForMouseTouch()
        {
            return (MaxZoomFactor - (float)ZoomFactor) / (MaxZoomFactor) + 0.375f;
        }

        float GetZoomCenterMoveingFactorForController()
        {
            return (MaxZoomFactor - (float)ZoomFactor) / (MaxZoomFactor) + 0.1f;
        }


        private Vector2 _CanvasHalfSize;

        private int GetDefaultZoomFactorListIndex()
        {
            return ZoomFactorList.IndexOf(1.0f);
        }

        private IDisposable InitializeZoomReaction()
        {
            CurrentZoomFactorIndex = GetDefaultZoomFactorListIndex();
            _CanvasHalfSize = ImagesContainer.ActualSize * 0.5f;
            ElementCompositionPreview.GetElementVisual(ImagesContainer).CenterPoint = new Vector3(_CanvasHalfSize, 0);

            var scheduler = CoreDispatcherScheduler.Current;

            var disposables = new CompositeDisposable(new[]
            {
                Observable.FromEventPattern<SizeChangedEventHandler, SizeChangedEventArgs>(
                    h => ImagesContainer.SizeChanged += h,
                    h => ImagesContainer.SizeChanged -= h
                    )
                .Subscribe(x => 
                {
                    _CanvasHalfSize = x.EventArgs.NewSize.ToVector2() * 0.5f;
                }),
                _vm.ObserveProperty(x => x.CurrentImageIndex)
                .Subscribe(_ =>
                {
                    ZoomFactor = 1.0;
                    CurrentZoomFactorIndex = ZoomFactorList.IndexOf(1.0f);
                }),
                this.ObserveDependencyProperty(ZoomFactorProperty)
                .Select(x => this.ZoomFactor)
                .Subscribe(zoom =>
                {
                    IsZoomingEnabled = zoom != 1.0;
                    AnimationBuilder.Create().Scale(zoom, duration: ZoomDuration).Start(ImagesContainer);
                }),
                this.ObserveDependencyProperty(ZoomCenterProperty)
                .Select(x => this.ZoomCenter)
                .Subscribe(center =>
                {
                    if (_nowZoomCenterMovingWithPointer is false)
                    {
                        ZoomCenterAb.CenterPoint(center, duration: ZoomDuration, easingType: EasingType.Quartic, easingMode: EasingMode.EaseOut).Start(ImagesContainer);
                    }
                }),
                this.ObserveDependencyProperty(IsZoomingEnabledProperty)
                .Subscribe(isEnabledZomming =>
                {
                    if (ZoomFactor > 1.0)
                    {
                        _ = _vm.DisableImageDecodeWhenImageSmallerCanvasSize();
                    }
                }),
            });

            ZoomCenter = _CanvasHalfSize;

            return disposables;
        }

        private void IntaractionWall_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            bool isMoveCenter = _nowZoomCenterMovingWithPointer;
            _nowZoomCenterMovingWithPointer = false;

            if (IsZoomingEnabled)
            {
                IsZoomingEnabled = ZoomFactor != 1.0f;

                if (isMoveCenter is false)
                {
                    ToggleOpenCloseBottomUI();
                    e.Handled = true;
                }
            }
            else
            {
                if (e.Cumulative.Translation.X > 60
                    || e.Velocities.Linear.X > 0.75
                    )
                {
                    // 右スワイプ
                    LeftPageMoveButton.Command.Execute(null);
                }
                else if (e.Cumulative.Translation.X < -60
                    || e.Velocities.Linear.X < -0.75
                    )
                {
                    // 左スワイプ
                    RightPageMoveButton.Command.Execute(null);
                }
                else if (e.Cumulative.Translation.Y < -60
                    || e.Velocities.Linear.Y < -0.25
                    )
                {
                    ToggleOpenCloseBottomUI();
                    e.Handled = true;
                }
                else
                {
                    CloseBottomUI();
                    e.Handled = true;
                }
            }
        }

        bool _nowZoomCenterMovingWithPointer;

        private void IntaractionWall_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            _startZoomFactor = (float)ZoomFactor;
            _nowZoomCenterMovingWithPointer = true;
        }

        float _startZoomFactor;
        float _sumScale;
        private void ImagesContainer_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            var factor = GetZoomCenterMoveingFactorForMouseTouch();
            if (e.PointerDeviceType is PointerDeviceType.Touch)
            {
                // ズーム操作と移動操作は排他的に行う
                if (e.Delta.Scale is not 1.0f)
                {
                    // 拡縮開始時との差分で計算する
                    _sumScale += (e.Delta.Scale - (e.Delta.Scale * 0.01f) - 1.0f);
                    var nextZoom = Math.Clamp(_startZoomFactor * (_sumScale + 1.0f), MinZoomFactor, MaxZoomFactor);
                    if (nextZoom < 1.0f)
                    {
                        nextZoom = 1.0f;
                    }

                    ZoomFactor = nextZoom;                    
                }
                else
                {
                    if (ZoomFactor > 1.0)
                    {
                        ZoomCenter = ZoomCenter - e.Delta.Translation.ToVector2() * MathF.Pow(factor, 2f);
                        var visual = ElementCompositionPreview.GetElementVisual(ImagesContainer);
                        visual.CenterPoint = new Vector3(ZoomCenter, 0);
                    }
                }
            }
            else
            {
                if (ZoomFactor > 1.0)
                {
                    // ズームが強くなるほど視点移動速度を下げて「滑ってる感」を小さくしたい
                    ZoomCenter = ZoomCenter - e.Delta.Translation.ToVector2() * MathF.Pow(factor, 2f);
                    var visual = ElementCompositionPreview.GetElementVisual(ImagesContainer);
                    visual.CenterPoint = new Vector3(ZoomCenter, 0);
                }
            }
        }


        RelayCommand<PointerRoutedEventArgs> _ZoomUpCommand;
        public RelayCommand<PointerRoutedEventArgs> ZoomUpCommand => _ZoomUpCommand
            ??= new RelayCommand<PointerRoutedEventArgs>(args =>
            {
                var targetUI = ImagesContainer;
                var lastZoom = (float)ZoomFactor;
                var nextCenter = args.GetCurrentPoint(targetUI).Position.ToVector2();
                var nextZoom = ZoomFactorList[CurrentZoomFactorIndex + 1 < ZoomFactorList.Length ? ++CurrentZoomFactorIndex : CurrentZoomFactorIndex];
                if (lastZoom < 1.0f && nextZoom >= 1.0f)
                {
                    nextZoom = 1.0f;
                    nextCenter = _CanvasHalfSize;
                }
                else if (nextZoom == lastZoom)
                {
                    return;
                }
                else if (nextZoom <= 1.0f)
                {
                    // マウス位置を無視して画像中央に向かうようにセンター位置を移動させていく
                    var imageCenterPos = targetUI.ActualSize;
                    Vector2 lastCenterPos = new Vector2(targetUI.CenterPoint.X, targetUI.CenterPoint.Y);
                    nextCenter = (imageCenterPos - lastCenterPos) * 0.5f + lastCenterPos;
                }

                ZoomFactor = nextZoom;
                ZoomCenter = nextCenter;
                IsZoomingEnabled = nextZoom != 1.0f;
            });

        RelayCommand<PointerRoutedEventArgs> _ZoomDownCommand;
        public RelayCommand<PointerRoutedEventArgs> ZoomDownCommand => _ZoomDownCommand
            ??= new RelayCommand<PointerRoutedEventArgs>(args =>
            {
                var targetUI = ImagesContainer;
                var lastZoom = (float)ZoomFactor;
                var lastCenter = ZoomCenter;
                var nextCenter = Vector2.Zero;
                var nextZoom = ZoomFactorList[CurrentZoomFactorIndex - 1 >= 0 ? --CurrentZoomFactorIndex : CurrentZoomFactorIndex];
                if (lastZoom - 1.0f > float.Epsilon && nextZoom <= 1.0f)
                {
                    nextZoom = 1.0f;
                    nextCenter = lastCenter;
                }
                else if (nextZoom == lastZoom)
                {
                    return;
                }
                else if (nextZoom > 1.0f)
                {
                    // マウス位置を無視して画像中央に向かうようにセンター位置を移動させていく
                    var imageCenterPos = _CanvasHalfSize;
                    nextCenter = (imageCenterPos - lastCenter) * 0.05f + lastCenter;
                }
                else
                {
                    nextCenter = _CanvasHalfSize;
                }

                ZoomFactor = nextZoom;
                IsZoomingEnabled = nextZoom != 1.0f;
                ZoomCenter = nextCenter;
            });

        RelayCommand _ZoomResetCommand;
        public RelayCommand ZoomResetCommand => _ZoomResetCommand
            ??= new RelayCommand(() =>
            {
                CurrentZoomFactorIndex = GetDefaultZoomFactorListIndex();
                ZoomCenter = _CanvasHalfSize;
                ZoomFactor = 1.0;
            });

        Vector2 ToZoomCenterInsideCanvas(Vector2 center)
        {
            var range = ImagesContainer.ActualSize;
            var x = Math.Clamp(center.X, -range.X, range.X);
            var y = Math.Clamp(center.Y, -range.Y, range.Y);
            return new Vector2(x, y);
        }

        RelayCommand _ZoomUpWithControllerCommand;
        public RelayCommand ZoomUpWithControllerCommand => _ZoomUpWithControllerCommand
            ??= new RelayCommand(() =>
            {
                var targetUI = ImagesContainer;
                var lastZoom = (float)ZoomFactor;
                var nextZoom = ZoomFactorList[CurrentZoomFactorIndex + 1 < ZoomFactorList.Length ? ++CurrentZoomFactorIndex : CurrentZoomFactorIndex];
                if (lastZoom < 1.0f && nextZoom >= 1.0f)
                {
                    nextZoom = 1.0f;
                }
                else if (nextZoom == lastZoom)
                {
                    return;
                }

                ZoomFactor = nextZoom;
                IsZoomingEnabled = nextZoom != 1.0f;
            });

        RelayCommand _ZoomDownWithControllerCommand;
        public RelayCommand ZoomDownWithControllerCommand => _ZoomDownWithControllerCommand
            ??= new RelayCommand(() =>
            {
                var targetUI = ImagesContainer;
                var lastZoom = (float)ZoomFactor;
                var lastCenter = ZoomCenter;
                var nextCenter = Vector2.Zero;
                var nextZoom = ZoomFactorList[CurrentZoomFactorIndex - 1 >= 0 ? --CurrentZoomFactorIndex : CurrentZoomFactorIndex];
                if (lastZoom - 1.0f > float.Epsilon && nextZoom <= 1.0f)
                {
                    nextZoom = 1.0f;
                    nextCenter = lastCenter;
                }
                else if (nextZoom == lastZoom)
                {
                    return;
                }
                else if (nextZoom > 1.0f)
                {
                    // マウス位置を無視して画像中央に向かうようにセンター位置を移動させていく
                    var imageCenterPos = _CanvasHalfSize;
                    nextCenter = (imageCenterPos - lastCenter) * 0.05f + lastCenter;
                }
                else
                {
                    nextCenter = _CanvasHalfSize;
                }

                ZoomFactor = nextZoom;
                IsZoomingEnabled = nextZoom != 1.0f;
                ZoomCenter = nextCenter;
            });


        RelayCommand _ZoomCenterMoveRightCommand;
        public RelayCommand ZoomCenterMoveRightCommand => _ZoomCenterMoveRightCommand
            ??= new RelayCommand(() =>
            {
                var targetUI = ImagesContainer;
                if (ZoomFactor > 1.0f)
                {
                    ZoomCenter += new Vector2(ControlerZoomCenterMoveAmount * GetZoomCenterMoveingFactorForController(), 0);
                }
            });

        RelayCommand _ZoomCenterMoveLeftCommand;
        public RelayCommand ZoomCenterMoveLeftCommand => _ZoomCenterMoveLeftCommand
            ??= new RelayCommand(() =>
            {
                var targetUI = ImagesContainer;
                if (ZoomFactor > 1.0f)
                {
                    ZoomCenter += new Vector2(-ControlerZoomCenterMoveAmount * GetZoomCenterMoveingFactorForController(), 0);
                }
            });

        RelayCommand _ZoomCenterMoveUpCommand;
        public RelayCommand ZoomCenterMoveUpCommand => _ZoomCenterMoveUpCommand
            ??= new RelayCommand(() =>
            {
                var targetUI = ImagesContainer;
                if (ZoomFactor > 1.0f)
                {
                    ZoomCenter += new Vector2(0, -ControlerZoomCenterMoveAmount * GetZoomCenterMoveingFactorForController());
                }
            });

        RelayCommand _ZoomCenterMoveDownCommand;

        public RelayCommand ZoomCenterMoveDownCommand => _ZoomCenterMoveDownCommand
            ??= new RelayCommand(() =>
            {
                var targetUI = ImagesContainer;
                if (ZoomFactor > 1.0f)
                {
                    ZoomCenter += new Vector2(0, ControlerZoomCenterMoveAmount * GetZoomCenterMoveingFactorForController());
                }
            });

        public bool IsZoomingEnabled
        {
            get { return (bool)GetValue(IsZoomingEnabledProperty); }
            set { SetValue(IsZoomingEnabledProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsZoomingEnabled.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsZoomingEnabledProperty =
            DependencyProperty.Register("IsZoomingEnabled", typeof(bool), typeof(ImageViewerPage), new PropertyMetadata(false));



        public TimeSpan ZoomDuration
        {
            get { return (TimeSpan)GetValue(ZoomDurationProperty); }
            set { SetValue(ZoomDurationProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ZoomDuration.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ZoomDurationProperty =
            DependencyProperty.Register("ZoomDuration", typeof(TimeSpan), typeof(ImageViewerPage), new PropertyMetadata(DefaultZoomingDuration));


        private string ToDisplayString(double zoomFactor)
        {
            return zoomFactor.ToString("F1");
        }

        public double ZoomFactor
        {
            get { return (double)GetValue(ZoomFactorProperty); }
            set { SetValue(ZoomFactorProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ZoomFactor.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ZoomFactorProperty =
            DependencyProperty.Register("ZoomFactor", typeof(double), typeof(ImageViewerPage), new PropertyMetadata(1.0));




        public Vector2 ZoomCenter
        {
            get { return (Vector2)GetValue(ZoomCenterProperty); }
            set { SetValue(ZoomCenterProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ZoomCenter.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ZoomCenterProperty =
            DependencyProperty.Register("ZoomCenter", typeof(Vector2), typeof(ImageViewerPage), new PropertyMetadata(Vector2.Zero));

        #endregion
    }
}
