using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.Toolkit.Uwp.UI.Animations;
using Microsoft.Toolkit.Uwp.UI.Controls;
using R3;
using R3.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.ViewModels;
using TsubameViewer.ViewModels.Albam.Commands;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.Views.Helpers;
using Windows.ApplicationModel.Core;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.Graphics.Display;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
#nullable enable
namespace TsubameViewer.Views;


public sealed class RequestConnectedAnimationMessage : AsyncRequestMessage<UIElement?>
{
    public RequestConnectedAnimationMessage(string targetPageName, string targetItemPath)
    {
        TargetPageName = targetPageName;
        TargetItemPath = targetItemPath;
    }

    public string TargetPageName { get; }
    public string TargetItemPath { get; }
}

[ObservableObject]
public sealed partial class ImageViewerPage : Page, ITitlebarContentAware
{
    public DataTemplate? GetContent()
    {
        return TitlebarContent;
    }

    public R3.Observable<string> ObserveTitleChanged()
    {
        return _vm.ObservePropertyChanged(x => x.ParentFolderOrArchiveName);
    }

    internal readonly ImageViewerPageViewModel _vm;

    readonly IMessenger _messenger;
    readonly FocusHelper _focusHelper;

    public ImageViewerPage()
    {
        this.InitializeComponent();

        DataContext = _vm = Ioc.Default.GetRequiredService<ImageViewerPageViewModel>();
        _messenger = Ioc.Default.GetRequiredService<IMessenger>();
        _focusHelper = Ioc.Default.GetRequiredService<FocusHelper>();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;            
    }

    void ImageViewerPage_KeyDown(object sender, KeyRoutedEventArgs e)
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

    void OnUnloaded(object sender, RoutedEventArgs e)
    {
        IntaractionWall.PointerPressed -= IntaractionWall_PointerPressed;
        IntaractionWall.PointerReleased -= IntaractionWall_PointerReleased;
        IntaractionWall.ManipulationDelta -= ImagesContainer_ManipulationDelta;
        IntaractionWall.ManipulationStarted -= IntaractionWall_ManipulationStarted;
        IntaractionWall.ManipulationCompleted -= IntaractionWall_ManipulationCompleted;

        KeyDown -= ImageViewerPage_KeyDown;

        Window.Current.CoreWindow.PointerMoved -= CoreWindow_PageSlider_PointerMoved;
    }

    void OnLoaded(object sender, RoutedEventArgs e)
    {
        CloseBottomUI();

        IntaractionWall.ManipulationMode = ManipulationModes.Scale | ManipulationModes.TranslateX | ManipulationModes.TranslateY;
        IntaractionWall.PointerPressed += IntaractionWall_PointerPressed;
        IntaractionWall.PointerReleased += IntaractionWall_PointerReleased;

        IntaractionWall.ManipulationDelta += ImagesContainer_ManipulationDelta;
        IntaractionWall.ManipulationStarted += IntaractionWall_ManipulationStarted;
        IntaractionWall.ManipulationCompleted += IntaractionWall_ManipulationCompleted;

        KeyDown += ImageViewerPage_KeyDown;

        Window.Current.CoreWindow.PointerPressed += CoreWindow_PageSlider_PointerPressed;
        Window.Current.CoreWindow.PointerReleased += CoreWindow_PageSlider_PointerReleased;
        Window.Current.CoreWindow.PointerMoved += CoreWindow_PageSlider_PointerMoved;

        var thumbnailManager = Ioc.Default.GetRequiredService<ThumbnailImageManager>();
        this.ObservePropertyChanged(x => x.PageSelectorCandidateImageIndex, false)
            .DistinctUntilChanged()
            .Debounce(TimeSpan.FromMilliseconds(10))
            .SubscribeAwait((this, thumbnailManager), static async (x, state, ct) => 
            {
                var (s, thumbnailManager) = state;
                //if (s._lastPointerDeviceType == PointerDeviceType.Touch)
                {
                    //s.MovieSeekbarTooltipImage.Visibility = Visibility.Collapsed;
                    //return;
                }

                long ts = TimeProvider.System.GetTimestamp();

                var imageSource = await s._vm.GetImageSourceWithCacheAsync(s.PageSelectorCandidateImageIndex, ct);
                using (var imageStream = await thumbnailManager.GetThumbnailImageStreamAsync(imageSource, ct: ct))
                {
                    if (s.MovieSeekbarTooltipImage.Source is not BitmapImage image)
                    {
                        s.MovieSeekbarTooltipImage.Source  = image = new BitmapImage();
                    }

                    await image.SetSourceAsync(imageStream.AsRandomAccessStream());

                    s.MovieSeekbarTooltipImage.Source = image;                    
                }

                s.MovieSeekbarTooltipImage.Visibility = Visibility.Visible;
                Debug.WriteLine($"SeekBarFrameRenderTime: {TimeProvider.System.GetElapsedTime(ts)}");
            }, AwaitOperation.Drop)
            .RegisterTo(this.GetCancellationTokenOnUnloaded());
    }

    [ObservableProperty]
    int _pageSelectorCandidateImageIndex;

    [ObservableProperty]
    CanvasImageSource? _seekbarFrameImageSource;

    CanvasBitmap? _videoFrameBitmap;

    PointerDeviceType _lastPointerDeviceType;

    bool _nowPressedOnPageSlider;
    private void CoreWindow_PageSlider_PointerPressed(CoreWindow sender, PointerEventArgs args)
    {
        _nowPressedOnPageSlider = args.IsContactUIElement(PageSelector, Window.Current.Content, out Vector2 pos) 
            && ImageSelectorContainer.Visibility == Visibility.Visible;
        if (_nowPressedOnPageSlider)
        {
            _lastPointerDeviceType = args.CurrentPoint.PointerDevice.PointerDeviceType;
            _lastPointerPosition = pos;
            RefreshPageSelectorTooltipContainerTranslation();
            if (_nowPressedOnPageSlider)
            {
                if (_lastPageChangeRequestImageIndex != PageSelectorCandidateImageIndex)
                {
                    _vm.ChangePageCommand.Execute(PageSelectorCandidateImageIndex);
                    _lastPageChangeRequestImageIndex = PageSelectorCandidateImageIndex;
                }
                PageSelectorTooltipContainer.Visibility = Visibility.Collapsed;
            }
            else
            {
                PageSelectorTooltipContainer.Visibility = Visibility.Visible;
            }
        }
    }
    private void CoreWindow_PageSlider_PointerReleased(CoreWindow sender, PointerEventArgs args)
    {
        MovieSeekbarTooltipImage.Visibility = Visibility.Collapsed;
        _nowPressedOnPageSlider = false;
    }

    Vector2 _lastPointerPosition;
    int _lastPageChangeRequestImageIndex;
    void RefreshPageSelectorTooltipContainerTranslation()
    {
        var pos = _lastPointerPosition;
        bool isRightToLeft = PageSelector.FlowDirection == FlowDirection.RightToLeft;
        var ts = Window.Current.Content.TransformToVisual(PageSelector);
        var offset = ts.TransformPoint(new Point()).ToVector2();
        var posRatio = pos.X / (PageSelector.ActualWidth - 1);
        var pagePos = (int)((_vm.ImageCount) * posRatio) - 1;
        PageSelectorTooltipText.Text = (pagePos + 1).ToString();        
        PageSelectorTooltipContainer.Translation = new Vector3(
            isRightToLeft
                ? -pos.X + offset.X - (float)PageSelectorTooltipContainer.ActualWidth * 0.5f
                : pos.X - offset.X - (float)PageSelectorTooltipContainer.ActualWidth * 0.5f,
            -offset.Y - 48  - (float)PageSelectorTooltipContainer.ActualHeight,
            0);

        PageSelectorCandidateImageIndex = pagePos;
    }

    private void CoreWindow_PageSlider_PointerMoved(CoreWindow sender, PointerEventArgs args)
    {
        if (args.IsContactUIElement(PageSelector, Window.Current.Content, out Vector2 pos)
            && ImageSelectorContainer.Visibility == Visibility.Visible)
        {
            _lastPointerDeviceType = args.CurrentPoint.PointerDevice.PointerDeviceType;
            _lastPointerPosition = pos;
            RefreshPageSelectorTooltipContainerTranslation();
            if (_nowPressedOnPageSlider)
            {
                if (_lastPageChangeRequestImageIndex != PageSelectorCandidateImageIndex)
                {
                    _vm.ChangePageCommand.Execute(PageSelectorCandidateImageIndex);
                    _lastPageChangeRequestImageIndex = PageSelectorCandidateImageIndex;
                }
                PageSelectorTooltipContainer.Visibility = Visibility.Collapsed;
            }
            else
            {
                PageSelectorTooltipContainer.Visibility = Visibility.Visible;
            }
        }
        else
        {
            PageSelectorTooltipContainer.Visibility = Visibility.Collapsed;
        }
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

    R3.CompositeDisposable? _navigationDisposables;
    CancellationTokenSource? _navigaitonCts;
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        IsReadyToImageDisplay = false;
        _navigationDisposables = new R3.CompositeDisposable();

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
            async Task<R3.Unit> DelayReply()
            { 
                _navigationDisposables.Add(InitializeZoomReaction());

                await StartNavigatedAnimationAsync(ct);

                return R3.Unit.Default;
            }

            if (isFirst)
            {
                isFirst = false;
                m.Reply(DelayReply());
            }
            else
            {
                m.Reply(R3.Unit.Default);
            }
        });

        AnimationBuilder.Create()
            .Opacity(0.001, duration: TimeSpan.FromMilliseconds(1))
            .Start(ImageItemsControl_0);

        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        _navigationDisposables?.Dispose();

        _navigaitonCts?.Cancel();
        _navigaitonCts?.Dispose();
        _navigaitonCts = null;

        _messenger.Unregister<BackNavigationRequestingMessage>(this);
        _messenger.Unregister<ImageLoadedMessage>(this);

        d().FireAndForgetSafe();
        async Task d()
        {
            if (!_vm.NowDoubleImageView
                && _vm.CurrentDisplayImageSources.ElementAtOrDefault(0) is { } imageSource)
            {
                var imageContainer = _vm.CurrentDisplayImageIndex switch
                {
                    0 => ImageItemsControl_0,
                    1 => ImageItemsControl_1,
                    2 => ImageItemsControl_2,
                    _ => throw new InvalidOperationException(),
                };
                var connectedAnimationService = ConnectedAnimationService.GetForCurrentView();
                var anim = connectedAnimationService.PrepareToAnimate(PageTransitionHelper.BackToImageListConnectedAnimationName, imageContainer);
                try
                {
                    var res = await _messenger.Send(new RequestConnectedAnimationMessage(nameof(ImageListupPage), imageSource.Path));
                    if (res is { } target)
                    {
                        anim.Configuration = new DirectConnectedAnimationConfiguration();
                        anim.TryStart(target);
                    }
                    else { anim.Cancel(); }
                }
                catch
                {
                    anim.Cancel();
                }
            }

            base.OnNavigatingFrom(e);
        }
    }



    async Task StartNavigatedAnimationAsync(CancellationToken navigationCt)
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
                await AnimationBuilder.Create()
                   .CenterPoint(ImageItemsControl_0.ActualSize * 0.5f, duration: TimeSpan.FromMilliseconds(1))
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
    }

    async Task<bool> TryStartSingleImageAnimationAsync(ConnectedAnimation animation, CancellationToken navigationCt)
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

    async Task<IEnumerable<UIElement>> WaitImageLoadingAsync(CancellationToken ct)
    {
        if (_vm.DisplayImages_0.Length == 1)
        {
            UIElement? image = null;
            await VisualTreeExtentions.WaitFillingValue(() => 
            {
                image ??= ImageItemsControl_0.TryGetElement(0);
                if (image == null) { return false; }
                return image.ActualSize.X is not 0 && image.ActualSize.Y is not 0;
            }, ct);
            return new[] { image! };
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
    void ReversableGoNext()
    {
        if (!_vm.IsLeftBindingEnabled)
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
    void ReversableGoPrev()
    {
        if (!_vm.IsLeftBindingEnabled)
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

    void IntaractionWall_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var lastMnipulating = _nowZoomCenterMovingWithPointer;
        _nowZoomCenterMovingWithPointer = false;
        if (e.Handled) { return; }
        if (!lastMnipulating && !IsZoomingEnabled)
        {
            var pointer = e.GetCurrentPoint(RootGrid);
            if (!_isLastPointerPressedLeft) { return; }

            var pt = pointer.Position;
            if (VisualTreeHelper.FindElementsInHostCoordinates(pt, ButtonsContainer).Any()) { return; }
            if (VisualTreeHelper.FindElementsInHostCoordinates(pt, ImageSelectorContainer).Any()) { return; }
            if (_nowZoomCenterMovingWithPointer) { return; }

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
                        if (ToggleBottomMenuCommand is IRelayCommand command && command.CanExecute(null))
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

    bool _isLastPointerPressedLeft;
    void IntaractionWall_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var pointer = e.GetCurrentPoint(null);
        _isLastPointerPressedLeft = pointer.Properties.IsLeftButtonPressed;
    }


    string ToPercentage(double val)
    {
        return (val * 100).ToString("F0");
    }

    void ShowBottomUI()
    {
        IsOpenBottomMenu = true;
        ButtonsContainer.Visibility = Visibility.Visible;
        ImageSelectorContainer.Visibility = Visibility.Visible;

        if (_focusHelper.IsRequireSetFocus())
        {
            ZoomInButton.Focus(FocusState.Keyboard);
        }            
    }

    void CloseBottomUI()
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

    [RelayCommand]
    void ToggleBottomMenu()
    {
        ToggleOpenCloseBottomUI();
    }

    public bool IsOpenBottomMenu
    {
        get { return (bool)GetValue(IsOpenBottomMenuProperty); }
        set { SetValue(IsOpenBottomMenuProperty, value); }
    }

    public static readonly DependencyProperty IsOpenBottomMenuProperty =
        DependencyProperty.Register("IsOpenBottomMenu", typeof(bool), typeof(ImageViewerPage), new PropertyMetadata(false));



    #endregion


    #region ZoomInOut


    const float _maxZoomFactor = 8.0f;
    const float _minZoomFactor = 0.5f;        

    static readonly float[] _zoomFactorList = Enumerable.Concat(
        new[] { 0.5f, .75f }, 
        new[] { 1.0f, 1.5f, 2.0f, 4.0f, 8f, 16f, 32f }
        ).ToArray();

    int _currentZoomFactorIndex;
    static readonly TimeSpan _defaultZoomingDuration = TimeSpan.FromMilliseconds(150);
    readonly AnimationBuilder _zoomCenterAb = AnimationBuilder.Create();

    const float _controlerZoomCenterMoveAmount = 100.0f;
    float GetZoomCenterMoveingFactorForMouseTouch()
    {
        return (_maxZoomFactor - (float)ZoomFactor) / (_maxZoomFactor) + 0.375f;
    }

    float GetZoomCenterMoveingFactorForController()
    {
        return (_maxZoomFactor - (float)ZoomFactor) / (_maxZoomFactor) + 0.1f;
    }


    Vector2 _canvasHalfSize;

    int GetDefaultZoomFactorListIndex()
    {
        return Array.IndexOf(_zoomFactorList, 1.0f);
    }

    IDisposable InitializeZoomReaction()
    {
        _currentZoomFactorIndex = GetDefaultZoomFactorListIndex();
        _canvasHalfSize = ImagesContainer.ActualSize * 0.5f;
        ElementCompositionPreview.GetElementVisual(ImagesContainer).CenterPoint = new Vector3(_canvasHalfSize, 0);

        var scheduler = CoreDispatcherScheduler.Current;

        var disposables = new R3.CompositeDisposable(new[]
        {
            System.Reactive.Linq.Observable.FromEventPattern<SizeChangedEventHandler, SizeChangedEventArgs>(
                h => ImagesContainer.SizeChanged += h,
                h => ImagesContainer.SizeChanged -= h
                )
            .Subscribe(x => 
            {
                _canvasHalfSize = x.EventArgs.NewSize.ToVector2() * 0.5f;
            }),
            _vm.ObservePropertyChanged(x => x.CurrentImageIndex)
            .Subscribe(_ =>
            {
                ZoomFactor = 1.0;
                _currentZoomFactorIndex = GetDefaultZoomFactorListIndex();
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
                    _zoomCenterAb.CenterPoint(center, duration: ZoomDuration, easingType: EasingType.Quartic, easingMode: EasingMode.EaseOut).Start(ImagesContainer);
                }
            }),
            this.ObserveDependencyProperty(IsZoomingEnabledProperty)
                .SubscribeAwait(async (isEnabledZomming, ct) =>
            {
                if (ZoomFactor > 1.0)
                {
                    await _vm.DisableImageDecodeWhenImageSmallerCanvasSize();
                }
            }),
        });

        ZoomCenter = _canvasHalfSize;

        return disposables;
    }

    void IntaractionWall_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
    {
        bool isMoveCenter = _nowZoomCenterMovingWithPointer;

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
            if (e.Cumulative.Translation.X > 30)
            {
                // 右スワイプ
                ReversableGoNext();
            }
            else if (e.Cumulative.Translation.X < -30)
            {
                // 左スワイプ
                ReversableGoPrev();
            }
            else
            {
                _nowZoomCenterMovingWithPointer = false;
            }
        }
    }

    bool _nowZoomCenterMovingWithPointer;

    void IntaractionWall_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
    {
        _startZoomFactor = (float)ZoomFactor;
        _nowZoomCenterMovingWithPointer = true;
    }

    float _startZoomFactor;
    float _sumScale;
    void ImagesContainer_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        var factor = GetZoomCenterMoveingFactorForMouseTouch();
        if (e.PointerDeviceType is PointerDeviceType.Touch)
        {
            // ズーム操作と移動操作は排他的に行う
            if (e.Delta.Scale is not 1.0f)
            {
                // 拡縮開始時との差分で計算する
                _sumScale += (e.Delta.Scale - (e.Delta.Scale * 0.01f) - 1.0f);
                var nextZoom = Math.Clamp(_startZoomFactor * (_sumScale + 1.0f), _minZoomFactor, _maxZoomFactor);
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


    [RelayCommand]
    void ZoomUp(PointerRoutedEventArgs args)
    {
        var targetUI = ImagesContainer;
        var lastZoom = (float)ZoomFactor;
        var nextCenter = args.GetCurrentPoint(targetUI).Position.ToVector2();
        var nextZoom = _zoomFactorList[_currentZoomFactorIndex + 1 < _zoomFactorList.Length ? ++_currentZoomFactorIndex : _currentZoomFactorIndex];
        if (lastZoom < 1.0f && nextZoom >= 1.0f)
        {
            nextZoom = 1.0f;
            nextCenter = _canvasHalfSize;
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
    }

    [RelayCommand]
    void ZoomDown(PointerRoutedEventArgs args)
    {
        var targetUI = ImagesContainer;
        var lastZoom = (float)ZoomFactor;
        var lastCenter = ZoomCenter;
        var nextCenter = Vector2.Zero;
        var nextZoom = _zoomFactorList[_currentZoomFactorIndex - 1 >= 0 ? --_currentZoomFactorIndex : _currentZoomFactorIndex];
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
            var imageCenterPos = _canvasHalfSize;
            nextCenter = (imageCenterPos - lastCenter) * 0.05f + lastCenter;
        }
        else
        {
            nextCenter = _canvasHalfSize;
        }

        ZoomFactor = nextZoom;
        IsZoomingEnabled = nextZoom != 1.0f;
        ZoomCenter = nextCenter;
    }

    [RelayCommand]
    void ZoomReset()
    {
        _currentZoomFactorIndex = GetDefaultZoomFactorListIndex();
        ZoomCenter = _canvasHalfSize;
        ZoomFactor = 1.0;
    }

    Vector2 ToZoomCenterInsideCanvas(Vector2 center)
    {
        var range = ImagesContainer.ActualSize;
        var x = Math.Clamp(center.X, -range.X, range.X);
        var y = Math.Clamp(center.Y, -range.Y, range.Y);
        return new Vector2(x, y);
    }

    [RelayCommand]
    void ZoomUpWithController()
    {
        var targetUI = ImagesContainer;
        var lastZoom = (float)ZoomFactor;
        var nextZoom = _zoomFactorList[_currentZoomFactorIndex + 1 < _zoomFactorList.Length ? ++_currentZoomFactorIndex : _currentZoomFactorIndex];
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
    }

    [RelayCommand]
    void ZoomDownWithController()
    {
        var targetUI = ImagesContainer;
        var lastZoom = (float)ZoomFactor;
        var lastCenter = ZoomCenter;
        var nextCenter = Vector2.Zero;
        var nextZoom = _zoomFactorList[_currentZoomFactorIndex - 1 >= 0 ? --_currentZoomFactorIndex : _currentZoomFactorIndex];
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
            var imageCenterPos = _canvasHalfSize;
            nextCenter = (imageCenterPos - lastCenter) * 0.05f + lastCenter;
        }
        else
        {
            nextCenter = _canvasHalfSize;
        }

        ZoomFactor = nextZoom;
        IsZoomingEnabled = nextZoom != 1.0f;
        ZoomCenter = nextCenter;
    }
    [RelayCommand]
    void ZoomCenterMoveRight()
    {
        var targetUI = ImagesContainer;
        if (ZoomFactor > 1.0f)
        {
            ZoomCenter += new Vector2(_controlerZoomCenterMoveAmount * GetZoomCenterMoveingFactorForController(), 0);
        }
    }
    [RelayCommand]
    void ZoomCenterMoveLeft()
    {
        var targetUI = ImagesContainer;
        if (ZoomFactor > 1.0f)
        {
            ZoomCenter += new Vector2(-_controlerZoomCenterMoveAmount * GetZoomCenterMoveingFactorForController(), 0);
        }
    }
    [RelayCommand]
    void ZoomCenterMoveUp()
    {
        var targetUI = ImagesContainer;
        if (ZoomFactor > 1.0f)
        {
            ZoomCenter += new Vector2(0, -_controlerZoomCenterMoveAmount * GetZoomCenterMoveingFactorForController());
        }
    }

    [RelayCommand]
    void ZoomCenterMoveDown()
    {
        var targetUI = ImagesContainer;
        if (ZoomFactor > 1.0f)
        {
            ZoomCenter += new Vector2(0, _controlerZoomCenterMoveAmount * GetZoomCenterMoveingFactorForController());
        }
    }

    public bool IsZoomingEnabled
    {
        get { return (bool)GetValue(IsZoomingEnabledProperty); }
        set { SetValue(IsZoomingEnabledProperty, value); }
    }

    public static readonly DependencyProperty IsZoomingEnabledProperty =
        DependencyProperty.Register("IsZoomingEnabled", typeof(bool), typeof(ImageViewerPage), new PropertyMetadata(false));



    public TimeSpan ZoomDuration
    {
        get { return (TimeSpan)GetValue(ZoomDurationProperty); }
        set { SetValue(ZoomDurationProperty, value); }
    }

    public static readonly DependencyProperty ZoomDurationProperty =
        DependencyProperty.Register("ZoomDuration", typeof(TimeSpan), typeof(ImageViewerPage), new PropertyMetadata(_defaultZoomingDuration));


    string ToDisplayString(double zoomFactor)
    {
        return zoomFactor.ToString("F1");
    }

    public double ZoomFactor
    {
        get { return (double)GetValue(ZoomFactorProperty); }
        set { SetValue(ZoomFactorProperty, value); }
    }

    public static readonly DependencyProperty ZoomFactorProperty =
        DependencyProperty.Register("ZoomFactor", typeof(double), typeof(ImageViewerPage), new PropertyMetadata(1.0));




    public Vector2 ZoomCenter
    {
        get { return (Vector2)GetValue(ZoomCenterProperty); }
        set { SetValue(ZoomCenterProperty, value); }
    }

    public static readonly DependencyProperty ZoomCenterProperty =
        DependencyProperty.Register("ZoomCenter", typeof(Vector2), typeof(ImageViewerPage), new PropertyMetadata(Vector2.Zero));



    #endregion

    void Page1MenuFlyout_Opening(object sender, object e)
    {
    }

    void Page2MenuFlyout_Opening(object sender, object e)
    {
    }


    [RelayCommand]
    void FavoriteToggle(object parameter)
    {
        _vm.FavoriteToggleCommand.Execute(parameter);
    }
}

public class SelectorSelectedChangedToStringConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
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
