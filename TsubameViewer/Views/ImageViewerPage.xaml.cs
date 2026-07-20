using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using CommunityToolkit.WinUI.Animations;
using CommunityToolkit.WinUI.Controls;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using R3;
using R3.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
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
using ZLinq;
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
        _coreAppView = CoreApplication.GetCurrentView();
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

    private void PageSelector_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        //_vm.CurrentImageIndex = (int)e.NewValue;
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
            if (_nowPressedOnPageSlider && _lastPointerDeviceType != PointerDeviceType.Touch)
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
        PageSelectorTooltipContainer.Visibility = Visibility.Collapsed;
        _nowPressedOnPageSlider = false;

        if (_lastPointerDeviceType == PointerDeviceType.Touch)
        {
            if (args.IsContactUIElement(PageSelector, Window.Current.Content, out Vector2 pos))
            {
                _vm.ChangePageCommand.Execute(PageSelectorCandidateImageIndex);
            }
            else
            {
                PageSelector.Value = _vm.CurrentImageIndex;
            }
        }
    }    

    readonly CoreApplicationView _coreAppView;
    Vector2 _lastPointerPosition;
    int _lastPageChangeRequestImageIndex;    
    void RefreshPageSelectorTooltipContainerTranslation()
    {
        bool isRightToLeft = PageSelector.FlowDirection == FlowDirection.RightToLeft;
        var pos = _lastPointerPosition;
        var ts = Window.Current.Content.TransformToVisual(PageSelector);
        var offset = ts.TransformPoint(new Point()).ToVector2();
        var posRatio = pos.X / (PageSelector.ActualWidth);
        var pagePos = (int)Math.Round((_vm.ImageCount - 1) * posRatio);
        PageSelectorTooltipText.Text = (pagePos + 1).ToString();
        var halfContainerWidth = (float)PageSelectorTooltipContainer.ActualWidth * 0.5f;
        float clampedPosX = (float)Math.Clamp(isRightToLeft ? - pos.X + offset.X : pos.X - offset.X,
            halfContainerWidth  + 8,
            (float)UIContainer.ActualWidth - (halfContainerWidth)  - 8);        
        PageSelectorTooltipContainer.Translation = new Vector3(
            clampedPosX - halfContainerWidth,
            -offset.Y - (_coreAppView.TitleBar.IsVisible ? 48 : 0)  - (float)PageSelectorTooltipContainer.ActualHeight,
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
            if (_nowPressedOnPageSlider && _lastPointerDeviceType != PointerDeviceType.Touch)
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

    CancellationToken _navigationCt;
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        IsReadyToImageDisplay = false;

        _vm.NowEditTransformMode = false;
        _vm.TransformScale = 1;
        _navigationCt = this.GetCancellationTokenOnNavigatingFrom();
        CloseBottomUI();
        
        IntaractionWall.PointerPressed += IntaractionWall_PointerPressed;
        IntaractionWall.PointerReleased += IntaractionWall_PointerReleased;

        KeyDown += ImageViewerPage_KeyDown;

        Window.Current.CoreWindow.PointerPressed += CoreWindow_PageSlider_PointerPressed;
        Window.Current.CoreWindow.PointerReleased += CoreWindow_PageSlider_PointerReleased;
        Window.Current.CoreWindow.PointerMoved += CoreWindow_PageSlider_PointerMoved;

        _messenger.Register<BackNavigationRequestingMessage>(this, (r, m) => 
        {
            if (IsOpenBottomMenu)
            {
                m.Value.IsHandled = true;
                ToggleOpenCloseBottomUI();
            }            
        });

        DisposableBuilder db = new();
        _messenger.CreateObservable<ImageLoadedMessage>()
            .ToObservable()
            .Take(1)
            .Subscribe(m =>
            {
                _ = StartNavigatedAnimationAsync(_navigationCt);
            })
            .AddTo(ref db);

        _vm.ObservePropertyChanged(x => x.CurrentImageIndex)
            .Subscribe(x =>
            {
                if (!_nowPressedOnPageSlider)
                {
                    PageSelector.Value = x;
                }
            })
            .AddTo(ref db);

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
                        s.MovieSeekbarTooltipImage.Source = image = new BitmapImage();
                    }

                    await image.SetSourceAsync(imageStream.AsRandomAccessStream());

                    s.MovieSeekbarTooltipImage.Source = image;
                }

                s.MovieSeekbarTooltipImage.Visibility = Visibility.Visible;
                Debug.WriteLine($"SeekBarFrameRenderTime: {TimeProvider.System.GetElapsedTime(ts)}");
            }, AwaitOperation.Drop)
            .AddTo(ref db);

        SubscribeTransformEdit(ref db);

        db.Build().RegisterTo(_navigationCt);

        AnimationBuilder.Create()
            .Opacity(0, duration: TimeSpan.FromMilliseconds(1))
            .Translation(new Vector2(0, -24), duration: TimeSpan.FromMilliseconds(1))
            .Start(ButtonsContainer);
        AnimationBuilder.Create()
            .Opacity(0, duration: TimeSpan.FromMilliseconds(1))
            .Translation(new Vector2(0, 24), duration: TimeSpan.FromMilliseconds(1))
            .Start(ImageSelectorContainer);

        AnimationBuilder.Create()
            .Opacity(0.001, duration: TimeSpan.FromMilliseconds(1))
            .Start(ImageItemsControl_0);

        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        IntaractionWall.PointerPressed -= IntaractionWall_PointerPressed;
        IntaractionWall.PointerReleased -= IntaractionWall_PointerReleased;
        KeyDown -= ImageViewerPage_KeyDown;
        Window.Current.CoreWindow.PointerMoved -= CoreWindow_PageSlider_PointerMoved;
        _messenger.Unregister<BackNavigationRequestingMessage>(this);        

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
        //InitializeZoomReaction()
        //    .RegisterTo(navigationCt);

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
        if (e.Handled) { return; }
        if (!_vm.NowEditTransformMode)
        {
            var pointer = e.GetCurrentPoint(RootGrid);
            if (!_isLastPointerPressedLeft) { return; }

            var pt = pointer.Position;
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

        if (_focusHelper.IsRequireSetFocus())
        {
            TransformEditModeButton.Focus(FocusState.Keyboard);
        }            
    }

    void CloseBottomUI()
    {
        IsOpenBottomMenu = false;
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



    [RelayCommand]
    void ToggleEditTransformMode()
    {
        _vm.NowEditTransformMode = !_vm.NowEditTransformMode;
    }

    [RelayCommand]
    void ResetPlayerTransform()
    {
        _vm.TransformScale = 1;
        PlayerTranslate.X = 0;
        PlayerTranslate.Y = 0;
    }

    void SubscribeTransformEdit(ref DisposableBuilder db)
    {
        _vm.ObservePropertyChanged(x => x.NowEditTransformMode, false)
            .Subscribe(this, (isEnabled, s) => 
            {
                if (isEnabled)
                {
                    s.CloseBottomUI();
                }
            })
            .AddTo(ref db);

        R3.Observable.Merge(
            Window.Current.CoreWindow.ObserveKeyDown().Where(x => x.EventArgs.VirtualKey == VirtualKey.Control).Select(_ => true),
            Window.Current.CoreWindow.ObserveKeyUp().Where(x => x.EventArgs.VirtualKey == VirtualKey.Control).Select(_ => false)
            )
            .Subscribe(this, static (isControlDown, s) =>
            {
                if (s._vm.NowEditTransformMode != isControlDown)
                {
                    s._vm.NowEditTransformMode = isControlDown;
                    Debug.WriteLine($"NowEditTransformMode: {s._vm.NowEditTransformMode}");
                }
            })
            .AddTo(ref db);

        this.ObservePointerWheelChanged()
            .Where(this, (x, s) => s._vm.NowEditTransformMode)
            .Subscribe(this, static (e, s) =>
            {
                var halfSize = s.ContentContainer.ActualSize * 0.5f;
                // ポインタ位置（PlayerContainer座標系）
                var pt = e.GetCurrentPoint(s.ContentContainer).Position.ToVector2() - halfSize;

                // 現在のスケール（X/Yは同じ前提）
                var oldScale = s._vm.TransformScale;
                if (oldScale <= 0) oldScale = 1.0;

                // ホイール方向でスケールを決定
                var wheel = e.GetCurrentPoint(null).Properties.MouseWheelDelta;
                var newScale = Math.Clamp((wheel > 0 ? s.GetNextScale(oldScale) : s.GetPrevScale(oldScale)), 0.5, 8.0);

                if (Math.Abs(newScale - oldScale) < double.Epsilon) return;

                if (newScale == 1d)
                {
                    s.PlayerTranslate.X = 0;
                    s.PlayerTranslate.Y = 0;
                }
                else if (newScale < 1d && newScale > oldScale)
                {
                    var factor = (oldScale - newScale) / (1 - s._playerScaleItems[0]);
                    // 1に近づく場合に
                    s.PlayerTranslate.X = Math.Round(Math.Clamp(s.PlayerTranslate.X + s.PlayerTranslate.X * factor, -halfSize.X, halfSize.X));
                    s.PlayerTranslate.Y = Math.Round(Math.Clamp(s.PlayerTranslate.Y + s.PlayerTranslate.Y * factor, -halfSize.Y, halfSize.Y));
                }
                else if (newScale > 1d && newScale < oldScale)
                {
                    // ポインタ位置を固定するための平行移動を計算
                    // T_new = T_old + P * (1/S_new - 1/S_old)
                    var invOld = 1.0 / oldScale;
                    var invNew = 1.0 / newScale;
                    var dx = s._lastZoomUpPos.X * (invNew - invOld);
                    var dy = s._lastZoomUpPos.Y * (invNew - invOld);

                    s.PlayerTranslate.X = Math.Round(Math.Clamp(s.PlayerTranslate.X + dx, -halfSize.X, halfSize.X));
                    s.PlayerTranslate.Y = Math.Round(Math.Clamp(s.PlayerTranslate.Y + dy, -halfSize.Y, halfSize.Y));
                }
                else
                {
                    // ポインタ位置を固定するための平行移動を計算
                    // T_new = T_old + P * (1/S_new - 1/S_old)
                    var invOld = 1.0 / oldScale;
                    var invNew = 1.0 / newScale;
                    var dx = pt.X * (invNew - invOld);
                    var dy = pt.Y * (invNew - invOld);

                    s.PlayerTranslate.X = Math.Round(Math.Clamp(s.PlayerTranslate.X + dx, -halfSize.X, halfSize.X));
                    s.PlayerTranslate.Y = Math.Round(Math.Clamp(s.PlayerTranslate.Y + dy, -halfSize.Y, halfSize.Y));
                    s._lastZoomUpPos = new(pt.X, pt.Y);
                }
                s._vm.TransformScale = newScale;
                //s.PlayerScale.ScaleY = newScale;

                //Debug.WriteLine($"Scale: {oldScale:F2} -> {newScale:F2}");
                Debug.WriteLine($"Pos: {s.PlayerTranslate.X:F2} -> {s.PlayerTranslate.Y:F2}");
            })
            .AddTo(ref db);
        _vm.ObservePropertyChanged(x => x.TransformScale, false)
            .SubscribeAwait(this, static async (scale, s, ct) =>
            {
                if (scale > 1.0)
                {
                    await s._vm.DisableImageDecodeWhenImageSmallerCanvasSize();
                }
            })
            .AddTo(ref db);
    }

    Vector2 _lastZoomUpPos;

    double[] _playerScaleItems { get; } =
        [0.5, 0.75, 1, 1.125, 1.25, 1.5, 2, 4, 8, 16, 32];

    double GetNextScale(double current)
    {
        foreach (var f in _playerScaleItems)
        {
            if (f > current)
            {
                return f;
            }
        }

        return _playerScaleItems.Last();
    }

    double GetPrevScale(double current)
    {
        foreach (var f in _playerScaleItems.AsValueEnumerable().Reverse())
        {
            if (f < current)
            {
                return f;
            }
        }

        return _playerScaleItems.First();
    }

    double HalfDouble(double d) => d * 0.5d;
    double HalfDoubleNegation(double d) => d * -0.5d;
    double InverseDouble(double d) => 1 / d;


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
