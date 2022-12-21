using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Toolkit.Uwp.UI.Animations;
using Microsoft.UI.Xaml.Controls;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.UseCases;
using TsubameViewer.ViewModels;
using TsubameViewer.ViewModels.Albam.Commands;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.Views.Helpers;
using TsubameViewer.Views.UINavigation;
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
using Xamarin.Essentials;

// 空白ページの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234238 を参照してください

namespace TsubameViewer.Views
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class ImageViewerPage : Page
    {
        private readonly ImageViewerPageViewModel _vm;

        private readonly IMessenger _messenger;
        private readonly FocusHelper _focusHelper;

        public ImageViewerPage()
        {
            this.InitializeComponent();

            DataContext = _vm = Ioc.Default.GetService<ImageViewerPageViewModel>();
            _messenger = Ioc.Default.GetService<IMessenger>();
            _focusHelper = Ioc.Default.GetService<FocusHelper>();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;            
        }

        private void ImageViewerPage_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Escape && e.OriginalKey != VirtualKey.GamepadB)
            {
                if (IsOpenBottomMenu)
                {
                    CloseBottomUI();
                }
                else
                {
                    ClosePage();
                }
            }
            else if (e.Key is VirtualKey.Number1 or VirtualKey.Number2 or VirtualKey.Number3 or VirtualKey.Number4)
            {
                ShowBottomUI();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            IntaractionWall.Tapped -= IntaractionWall_Tapped;
            IntaractionWall.ManipulationDelta -= ImagesContainer_ManipulationDelta;
            IntaractionWall.ManipulationStarted -= IntaractionWall_ManipulationStarted;
            IntaractionWall.ManipulationCompleted -= IntaractionWall_ManipulationCompleted;

            KeyDown -= ImageViewerPage_KeyDown;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            CloseBottomUI();

            IntaractionWall.ManipulationMode = ManipulationModes.Scale | ManipulationModes.TranslateX | ManipulationModes.TranslateY;
            IntaractionWall.Tapped += IntaractionWall_Tapped;

            IntaractionWall.ManipulationDelta += ImagesContainer_ManipulationDelta;
            IntaractionWall.ManipulationStarted += IntaractionWall_ManipulationStarted;
            IntaractionWall.ManipulationCompleted += IntaractionWall_ManipulationCompleted;

            KeyDown += ImageViewerPage_KeyDown;
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

        void ForceClosePage(object sender, RoutedEventArgs e)
        {
            ClosePage();
        }

        void ClosePage()
        {
            _messenger.Unregister<BackNavigationRequestingMessage>(this);
            (_vm.BackNavigationCommand as ICommand).Execute(null);
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
            
            _messenger.Unregister<BackNavigationRequestingMessage>(this);
            _messenger.Unregister<ImageLoadedMessage>(this);

            base.OnNavigatingFrom(e);
        }


        protected override void OnNavigatedTo(NavigationEventArgs e)
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

            _messenger.Register<BackNavigationRequestingMessage>(this, (r, m) => 
            {
                if (IsOpenBottomMenu)
                {
                    m.Value.IsHandled = true;
                    ToggleOpenCloseBottomUI();
                }
            });

            _navigaitonCts = new CancellationTokenSource();
            var ct = _navigaitonCts.Token;
            bool isFirst = true;
            _messenger.Register<ImageLoadedMessage>(this, (r, m) => 
            {
                async Task<Unit> DelayReply()
                { 
                    _navigationDisposables.Add(InitializeZoomReaction());

                    await StartNavigatedAnimationAsync(ct);

                    return Unit.Default;
                }

                if (isFirst)
                {
                    isFirst = false;
                    m.Reply(DelayReply());
                }
                else
                {
                    m.Reply(Unit.Default);
                }
            });

            AnimationBuilder.Create()
                .Opacity(0.001, duration: TimeSpan.FromMilliseconds(1))
                .Start(ImageItemsControl_0);

            base.OnNavigatedTo(e);
        }

        private async Task StartNavigatedAnimationAsync(CancellationToken navigationCt)
        {
            IsReadyToImageDisplay = true;
            while (VSG_MouseScrool.CurrentState == VS_MouseScroolNotReadyToDisplay)
            {
                await Task.Delay(5, navigationCt);
            }

            bool isConnectedAnimationDone = false;
            var connectedAnimationService = ConnectedAnimationService.GetForCurrentView();
            ConnectedAnimation animation = connectedAnimationService.GetAnimation(PageTransitionHelper.ImageJumpConnectedAnimationName);
            if (animation != null)
            {
                try
                {
                    isConnectedAnimationDone = await TryStartSingleImageAnimationAsync(animation, navigationCt);
                }
                catch (OperationCanceledException) { }
            }

            try
            {
                if (isConnectedAnimationDone is false)
                {
                    await WaitImageLoadingAsync(navigationCt);
                    AnimationBuilder.Create()
                       .CenterPoint(ImageItemsControl_0.ActualSize * 0.5f, duration: TimeSpan.FromMilliseconds(1))
                       .Scale()
                           .TimedKeyFrames(ke =>
                           {
                               ke.KeyFrame(TimeSpan.FromMilliseconds(0), new(0.95f));
                               ke.KeyFrame(TimeSpan.FromMilliseconds(150), new(1.0f));
                           })
                       .Opacity(1.0, delay: TimeSpan.FromMilliseconds(10), duration: TimeSpan.FromMilliseconds(250))
                       .Start(ImageItemsControl_0, navigationCt);
                }
            }
            catch (OperationCanceledException) { }
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
            if (_vm.DisplayImages_0.Length == 1)
            {
                UIElement image = null;
                await VisualTreeExtentions.WaitFillingValue(() => 
                {
                    image ??= ImageItemsControl_0.TryGetElement(0);
                    if (image == null) { return false; }
                    return image.ActualSize.X is not 0 && image.ActualSize.Y is not 0;
                }, ct);
                return new[] { image };
            }
            else
            {
                UIElement[] images = new UIElement[2];
                await VisualTreeExtentions.WaitFillingValue(() =>
                {
                    images[0] ??= ImageItemsControl_0.TryGetElement(0);
                    images[1] ??= ImageItemsControl_0.TryGetElement(1);
                    if (images.Any(x => x is null)) { return false; }
                    return images.All(x => x.ActualSize.X is not 0 && x.ActualSize.Y is not 0);
                }, ct);
                return images;
            }
        }

        #endregion Navigation


        #region Page Next/Prev

        [RelayCommand]
        private void ReversableGoNext()
        {
            if (!_vm.IsLeftBindingEnabled.Value)
            {
                if (_vm.GoNextImageCommand.CanExecute(null))
                {
                    _vm.GoNextImageCommand.Execute(null);
                }
            }
            else
            {
                if (_vm.GoPrevImageCommand.CanExecute(null))
                {
                    _vm.GoPrevImageCommand.Execute(null);
                }
            }
        }

        [RelayCommand]
        private void ReversableGoPrev()
        {
            if (!_vm.IsLeftBindingEnabled.Value)
            {
                if (_vm.GoPrevImageCommand.CanExecute(null))
                {
                    _vm.GoPrevImageCommand.Execute(null);
                }
            }
            else
            {
                if (_vm.GoNextImageCommand.CanExecute(null))
                {
                    _vm.GoNextImageCommand.Execute(null);
                }
            }
        }

        #endregion


        #region Touch and Controller UI

        private void IntaractionWall_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (!_nowZoomCenterMovingWithPointer && !IsZoomingEnabled)
            {                
                var pt = e.GetPosition(RootGrid);
                
                if (VisualTreeHelper.FindElementsInHostCoordinates(pt, ButtonsContainer).Any()) { return; }
                if (VisualTreeHelper.FindElementsInHostCoordinates(pt, ImageSelectorContainer).Any()) { return; }
                
                if (!IsOpenBottomMenu)
                {
                    var uiItems = VisualTreeHelper.FindElementsInHostCoordinates(pt, UIContainer);
                    foreach (var item in uiItems)
                    {
                        if (item.Visibility == Visibility.Collapsed) { continue; }

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
                        else if (item == ToggleMenuButton)
                        {
                            if (ToggleBottomMenuCommand is ICommand command && command.CanExecute(null))
                            {
                                command.Execute(null);
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

            if (_focusHelper.IsRequireSetFocus())
            {
                ZoomInButton.Focus(FocusState.Keyboard);
            }            
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

        private RelayCommand _toggleBottomMenuCommand;
        public RelayCommand ToggleBottomMenuCommand =>
            _toggleBottomMenuCommand ?? (_toggleBottomMenuCommand = new RelayCommand(ExecuteToggleBottomMenuCommand, () => true));

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
            return Array.IndexOf(ZoomFactorList, 1.0f);
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
                    CurrentZoomFactorIndex = GetDefaultZoomFactorListIndex();
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
                if (e.Cumulative.Translation.X > 1
                    || e.Velocities.Linear.X > 0.01
                    )
                {
                    // 右スワイプ
                    ReversableGoNext();
                }
                else if (e.Cumulative.Translation.X < -1
                    || e.Velocities.Linear.X < -0.01
                    )
                {
                    // 左スワイプ
                    ReversableGoPrev();
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

        private void Page1MenuFlyout_Opening(object sender, object e)
        {
            Page1AlbamAddItemButton.Items.Clear();
            var albamRepository = Ioc.Default.GetRequiredService<AlbamRepository>();
            var imageSource = Page1AlbamAddItemButton.DataContext as IImageSource;
            foreach (var albam in albamRepository.GetAlbams())
            {
                Page1AlbamAddItemButton.Items.Add(new ToggleMenuFlyoutItem() { Text = albam.Name, Command = new AlbamItemAddCommand(albamRepository, albam), CommandParameter = imageSource, IsChecked = albamRepository.IsExistAlbamItem(albam._id, imageSource.FlattenAlbamItemInnerImageSource().Path) });
            }
        }

        private void Page2MenuFlyout_Opening(object sender, object e)
        {
            Page2AlbamAddItemButton.Items.Clear();
            var albamRepository = Ioc.Default.GetRequiredService<AlbamRepository>();
            var imageSource = Page2AlbamAddItemButton.DataContext as IImageSource;
            foreach (var albam in albamRepository.GetAlbams())
            {
                Page2AlbamAddItemButton.Items.Add(new ToggleMenuFlyoutItem() { Text = albam.Name, Command = new AlbamItemAddCommand(albamRepository, albam), CommandParameter = imageSource, IsChecked = albamRepository.IsExistAlbamItem(albam._id, imageSource.FlattenAlbamItemInnerImageSource().Path) });
            }
        }
    }

    public class SelectorSelectedChangedToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is SelectionChangedEventArgs args)
            {
                return args.AddedItems.FirstOrDefault() as string;
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
