using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Animations;
using CommunityToolkit.WinUI.Controls;
using DryIoc;
using DryIoc.FastExpressionCompiler.LightExpression;
using DryIoc.ImTools;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using R3;
using R3.Extensions;
using SkiaSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using TsubameViewer.Core;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using TsubameViewer.Services;
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
    readonly SecondaryWindowService _secondaryWindowService;
    readonly IWindowManagementAware _windowContext;

    public ImageViewerPage()
    {
        this.InitializeComponent();

        DataContext = _vm = Ioc.Default.GetRequiredService<ImageViewerPageViewModel>();
        _messenger = Ioc.Default.GetRequiredService<IMessenger>();
        _focusHelper = Ioc.Default.GetRequiredService<FocusHelper>();
        _secondaryWindowService = Ioc.Default.GetRequiredService<SecondaryWindowService>();
        _windowContext = _secondaryWindowService.GetCurentFocusWindow();

        _image1Source = new CanvasVirtualImageSource(CanvasDevice.GetSharedDevice(), 1, 1, 96);
        _image2Source = new CanvasVirtualImageSource(CanvasDevice.GetSharedDevice(), 1, 1, 96);
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


    [ObservableProperty]
    int _pageSelectorCandidateImageIndex;

    [ObservableProperty]
    CanvasImageSource? _seekbarFrameImageSource;

    PointerDeviceType _lastPointerDeviceType;
    bool _nowPressedOnPageSlider;
    Vector2 _lastPointerPosition;
    int _lastPageChangeRequestImageIndex;    
    void RefreshPageSelectorTooltipContainerTranslation()
    {
        bool isRightToLeft = PageSelector.FlowDirection == FlowDirection.RightToLeft;
        var pos = _lastPointerPosition;
        var ts = RootGrid.TransformToVisual(VideoPositionSliderWall);
        var offset = ts.TransformPoint(new Point()).ToVector2();
        var posRatio = Math.Clamp(pos.X / (VideoPositionSliderWall.ActualWidth), 0, 1);
        var pagePos = (int)Math.Round((_vm.ImageCount - 1) * posRatio);
        PageSelectorTooltipText.Text = (pagePos + 1).ToString();
        var halfContainerWidth = (float)PageSelectorTooltipContainer.ActualWidth * 0.5f;
        float clampedPosX = (float)Math.Clamp(isRightToLeft ? - pos.X + offset.X : pos.X - offset.X,
            halfContainerWidth  + 8,
            (float)UIContainer.ActualWidth - (halfContainerWidth)  - 8);        
        PageSelectorTooltipContainer.Translation = new Vector3(
            clampedPosX - halfContainerWidth,
            -offset.Y - (_windowContext.IsPrimary && _windowContext.NowDisplayTitleBar ? 0 : 0)  - (float)PageSelectorTooltipContainer.ActualHeight,
            0);

        PageSelectorCandidateImageIndex = pagePos;
    }

    private void PageSelectorSliderWall_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (e.IsContactUIElement(VideoPositionSliderWall, out Vector2 pos)
            && ImageSelectorContainer.Visibility == Visibility.Visible)
        {
            _lastPointerDeviceType = e.Pointer.PointerDeviceType;
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

    private void PageSelectorSliderWall_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _nowPressedOnPageSlider = e.IsContactUIElement(VideoPositionSliderWall, out Vector2 pos)
            && ImageSelectorContainer.Visibility == Visibility.Visible;
        if (_nowPressedOnPageSlider)
        {
            VideoPositionSliderWall.CapturePointer(e.Pointer);
            _lastPointerDeviceType = e.Pointer.PointerDeviceType;
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

    private void PageSelectorSliderWall_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        PageSelectorTooltipContainer.Visibility = Visibility.Collapsed;
        _nowPressedOnPageSlider = false;
        VideoPositionSliderWall.ReleasePointerCapture(e.Pointer);
        if (_lastPointerDeviceType == PointerDeviceType.Touch)
        {
            if (e.IsContactUIElement(VideoPositionSliderWall, out Vector2 pos))
            {
                _vm.ChangePageCommand.Execute(PageSelectorCandidateImageIndex);
            }
            else
            {
                PageSelector.Value = _vm.CurrentImageIndex;
            }
        }
        else
        {
            PageSelector.Value = _vm.CurrentImageIndex;
        }
    }

    private void PageSelectorSliderWall_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        PageSelectorTooltipContainer.Visibility = Visibility.Collapsed;
    }


    private void MovieSeekbarTooltipImage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RefreshPageSelectorTooltipContainerTranslation();
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
        if (_windowContext.IsPrimary)
        {
            _messenger.Unregister<BackNavigationRequestingMessage>(this);
            (_vm.BackNavigationCommand as ICommand).Execute(null);
        }
        else
        {
            _ = _secondaryWindowService.CloseAsync(_windowContext);
        }
    }

    class PrefetchImageInfo(IImageSource Item, CanvasBitmap Bitmap)
    {
        public IImageSource Item { get; } = Item;
        public CanvasBitmap Bitmap { get; } = Bitmap;
    }

    #region Display Image Cache
    readonly List<PrefetchImageInfo> _cachedBitmap = new ();
    readonly AsyncLock _cacheBitmapLock = new();

    static CanvasBitmap ToCanvasBitmap(SKBitmap skBitmap)
    {
        if (skBitmap.Info.ColorType != SKImageInfo.PlatformColorType)
        {
            var bitmap = skBitmap.Copy(SKImageInfo.PlatformColorType);
            skBitmap.Dispose();
            skBitmap = bitmap;
        }
        return CanvasBitmap.CreateFromBytes(
            CanvasDevice.GetSharedDevice(),
            skBitmap.Bytes,
            skBitmap.Width,
            skBitmap.Height,
            DirectXPixelFormat.B8G8R8A8UIntNormalized);
    }

    async Task<Stream> GetImageStreamAsync(IImageSource item, CancellationToken ct)
    {
        using (await _vm._imageLoadingLock.LockAsync(ct))
        {
            return await item.GetImageStreamAsync(ct);
        }
    }

    async Task<IImageSource> GetImageSourceAsync(int requestIndex, CancellationToken ct)
    {
        using (await _vm._imageLoadingLock.LockAsync(ct))
        {
            return await _vm.GetImageSourceWithCacheAsync(requestIndex, ct);
        }
    }

    async Task<CanvasBitmap?> TryCreateCanvasBitmapDecodeWithSkia(IImageSource item, double? requestHeight, CancellationToken ct)
    {
        try
        {
            using (var stream = await GetImageStreamAsync(item, ct))
            using (var skData = SKData.Create(stream))
            {
                if (requestHeight == null)
                {
                    using (var skBitmap = SKBitmap.Decode(skData))
                    {
                        ct.ThrowIfCancellationRequested();
                        if (skBitmap == null) { return null; }
                        else return ToCanvasBitmap(skBitmap);
                    }
                }
                else
                {
                    var info = SKBitmap.DecodeBounds(skData);
                    float scaledWidth = info.Width * (float)requestHeight.Value / info.Height;
                    using (var skBitmap = SKBitmap.Decode(skData, new SKImageInfo((int)scaledWidth, (int)requestHeight.Value)))
                    {
                        ct.ThrowIfCancellationRequested();
                        if (skBitmap == null) { return null; }
                        else return ToCanvasBitmap(skBitmap);
                    }
                }
            }
        }
        catch { return null; }
    }

    async Task<CanvasBitmap?> TryCreateCanvasBitmapDecodeWithWin2d(IImageSource item, double? requestHeight, CancellationToken ct)
    {
        try
        {
            using (var stream = await GetImageStreamAsync(item, ct))
            {
                if (requestHeight == null)
                {
                    return await CanvasBitmap.LoadAsync(CanvasDevice.GetSharedDevice(), stream.AsRandomAccessStream(), 96).AsTask(ct);
                }
                else 
                {
                    using (var bitmap = await CanvasBitmap.LoadAsync(CanvasDevice.GetSharedDevice(), stream.AsRandomAccessStream(), 96).AsTask(ct))
                    {
                        var scale = requestHeight.Value / bitmap.Size.Height;
                        var scaledWidth = bitmap.Size.Width * scale;
                        var rtb = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), (int)scaledWidth, (int)requestHeight.Value, 96);
                        try
                        {
                            using (var ds = rtb.CreateDrawingSession())
                            {
                                ds.Blend = CanvasBlend.Copy;
                                ds.Antialiasing = CanvasAntialiasing.Antialiased;
                                ds.Transform = Matrix3x2.CreateScale((float)scale);
                                ds.DrawImage(bitmap);
                            }
                            return rtb;
                        }
                        catch
                        {
                            rtb.Dispose();
                            throw;
                        }
                    }
                }
            }
        }
        catch { return null; }
    }

    bool TryGetCachedCanvasBitmap(IImageSource item, out CanvasBitmap? bitmap)
    {
        if (!_vm.ImageViewerSettings.IsEnablePrefetch) 
        {
            bitmap = null;
            return false;
        }

        if (_cachedBitmap.FirstOrDefault(x => IImageSourceEqualityComparer.Default.Equals(x.Item,item)) is { } cached)
        {
            bitmap = cached.Bitmap;
            _cachedBitmap.Remove(cached);            
            _cachedBitmap.Insert(0, cached);
            return true;
        }
        else
        {
            bitmap = null;
            return false;
        }
    }
    void ClearCachedCanvasBitmap()
    {
        var items = _cachedBitmap.ToArray();
        _cachedBitmap.Clear();
        foreach (var prefetchInfoItem in items)
        {
            prefetchInfoItem.Bitmap.Dispose();
        }
    }

    async Task<CanvasBitmap> EnsureGetBitmapWithCacheAsync(IImageSource item, double? requestHeight, CancellationToken ct)
    {
        CanvasBitmap? bitmap = null;

        if (TryGetCachedCanvasBitmap(item, out bitmap) && bitmap != null)
        {
            if (requestHeight == bitmap.Size.Height)
            {
                return bitmap!;
            }   
            else
            {
                bitmap = null;
                var info = _cachedBitmap.FirstOrDefault(x => x.Item == item);
                if (info != null)
                {
                    _cachedBitmap.Remove(info);
                    info.Bitmap.Dispose();                    
                }
            }
        }

        bitmap ??= await TryCreateCanvasBitmapDecodeWithSkia(item, requestHeight, ct);
        bitmap ??= await TryCreateCanvasBitmapDecodeWithWin2d(item, requestHeight, ct);
        Guard.IsNotNull(bitmap);
        if (_vm.ImageViewerSettings.IsEnablePrefetch)
        {
            _cachedBitmap.Insert(0, new(item, bitmap));
            Debug.WriteLine($"PushedCache: {item.Name}");
        }
        return bitmap;
    }

    async Task PrefetchBitmapAsync( int currentIndex, double? requestHeight, CancellationToken ct)
    {
        using var _ = await _cacheBitmapLock.LockAsync(ct);

        int[] prefetchTargets = [currentIndex + 1, currentIndex + 2, currentIndex + 3, currentIndex - 1, currentIndex - 2];
        HashSet<IImageSource> liveImages = new([.. _vm.SourceImages], IImageSourceEqualityComparer.Default);
        try
        {
            foreach (var index in prefetchTargets)
            {
                if (index < 0) { continue; }
                if (index > _vm.ImageCount - 1) { return; }

                var item = await GetImageSourceAsync(index, ct);
                liveImages.Add(item);
                if (_cachedBitmap.FirstOrDefault(x => IImageSourceEqualityComparer.Default.Equals(x.Item, item)) is { } cached)
                {
                    continue;
                }

                CanvasBitmap? bitmap = null;
                bitmap ??= await TryCreateCanvasBitmapDecodeWithSkia(item, requestHeight, ct);
                bitmap ??= await TryCreateCanvasBitmapDecodeWithWin2d(item, requestHeight, ct);
                if (bitmap == null) 
                {
                    Debug.WriteLine($"Failed Cache: {item.Name}");
                    continue; 
                }
                _cachedBitmap.Add(new(item, bitmap));
                Debug.WriteLine($"PushedCache: {item.Name}");
            }
        }
        finally
        {
            foreach (var item in _cachedBitmap.Where(x => !liveImages.Contains(x.Item, IImageSourceEqualityComparer.Default)).ToList())
            {
                _cachedBitmap.Remove(item);
                item.Bitmap.Dispose();
                Debug.WriteLine($"RemovedCache: {item.Item.Name}");
            }
        }
    }
#endregion


    readonly CanvasVirtualImageSource _image1Source;
    readonly CanvasVirtualImageSource _image2Source;
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

        _messenger.Register<BackNavigationRequestingMessage>(this, (r, m) => 
        {
            if (IsOpenBottomMenu)
            {
                m.Value.IsHandled = true;
                ToggleOpenCloseBottomUI();
            }            
        });
        DisposableBuilder db = new();


        this.ObserveSizeChanged()
            .Subscribe(size => 
            {
                ClearCachedCanvasBitmap();
                _vm.CanvasWidth = size.NewSize.Width;
                _vm.CanvasHeight = size.NewSize.Height;
                _vm.SizeChangedCommand.Execute(null);
            })
            .AddTo(ref db);

        Observable.Merge(
            _vm.ObservePropertyChanged(x => x.DisplayCurrentImageIndex, false).AsUnitObservable(),
            _vm.ObservePropertyChanged(x => x.SourceImages, false).AsUnitObservable(),
            this.ObserveSizeChanged().AsUnitObservable().ThrottleLast(TimeSpan.FromMilliseconds(250)),
            _vm.ObservePropertyChanged(x => x.IsDoubleViewEnabled, false).AsUnitObservable(),
            _vm.ObservePropertyChanged(x => x.IsLeftBindingEnabled, false).AsUnitObservable()
            )
            .ThrottleFirstLast(TimeSpan.FromMilliseconds(32))
            .SubscribeAwait(async (u, ct) =>
            {
                static void  DrawImage(double canvasHeight, CanvasBitmap bitmap, CanvasVirtualImageSource imageSource, Image imageControl)
                {
                    float scale = (float)canvasHeight / (float)bitmap.Size.Height;
                    var scaledSize = new Size((int)(bitmap.Size.Width * scale), (int)(bitmap.Size.Height * scale));
                    imageSource.Resize(scaledSize);
                    using (var ds = imageSource.CreateDrawingSession(Colors.Transparent, scaledSize.ToRect()))
                    {
                        ds.Blend = CanvasBlend.Copy;
                        ds.Antialiasing = CanvasAntialiasing.Antialiased;
                        ds.Transform = Matrix3x2.CreateScale(scale);
                        ds.DrawImage(bitmap);
                    }
                    imageControl.Width = scaledSize.Width;
                }

                IImageSource? firstImage = _vm.SourceImages.ElementAtOrDefault(0);
                IImageSource? secondImage = _vm.SourceImages.ElementAtOrDefault(1);
                using var _ = await _cacheBitmapLock.LockAsync(ct);
                long time = TimeProvider.System.GetTimestamp();
                _vm.NowImageLoadingLongRunning = true;
                int currentIndex = _vm.CurrentImageIndex;
                var canvasHeight = ImagesContainer.ActualHeight;
                var canvasWidth = ImagesContainer.ActualWidth;                                
                if (firstImage is IImageSource src1
                   && secondImage is IImageSource src2)
                {
                    if (currentIndex != _vm.CurrentImageIndex) { return; }
                    ct.ThrowIfCancellationRequested();
                    Image1.Height = canvasHeight;
                    Image2.Height = canvasHeight;
                    try
                    {                        
                        var bitmap1 = await EnsureGetBitmapWithCacheAsync(src1, canvasHeight, ct);
                        var bitmap2 = await EnsureGetBitmapWithCacheAsync(src2, canvasHeight, ct);
                        DrawImage(canvasHeight, bitmap1, _image1Source, Image1);
                        DrawImage(canvasHeight, bitmap2, _image2Source, Image2);
                    }
                    catch (OperationCanceledException) { return; }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.ToString());
                        return;
                    }

                    try
                    {
                        await _messenger.Send(new ImageLoadedMessage());
                    }
                    catch { }
                }
                else if (firstImage is IImageSource source1)
                {
                    Image2.Width = 0;
                    if (currentIndex != _vm.CurrentImageIndex) { return; }
                    ct.ThrowIfCancellationRequested();
                    
                    try
                    {
                        var bitmap1 = await EnsureGetBitmapWithCacheAsync(source1, canvasHeight, ct);
                        DrawImage(canvasHeight, bitmap1, _image1Source, Image1);
                    }
                    catch (OperationCanceledException) { return; }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.ToString());
                        return;
                    }
                    Image1.Height = double.NaN;
                    //Image1.Width = canvasWidth;
                    try
                    {
                        await _messenger.Send(new ImageLoadedMessage());
                    }
                    catch { }
                }
                else
                {
                    Image1.Width = double.NaN;
                    Image1.Height = double.NaN;
                    Image2.Width = 0;
                    Image2.Height = double.NaN;
                }

                ct.ThrowIfCancellationRequested();
                if (currentIndex != _vm.CurrentImageIndex) { return; }
                _vm.NowImageLoadingLongRunning = false;
                Debug.WriteLine($"Render time: {TimeProvider.System.GetElapsedTime(time)}");
                await Task.Delay(1);

                if (!_vm.ImageViewerSettings.IsEnablePrefetch) { return; }
                
                PrefetchBitmapAsync(currentIndex, canvasHeight, ct).FireAndForgetSafe();
                
            }, AwaitOperation.Switch)
            .AddTo(ref db);

        _messenger.CreateObservable<ImageLoadedMessage>()
            .ToObservable()
            .Index()
            .Subscribe(async m =>
            {
                async Task<Unit> db()
                {
                    await StartNavigatedAnimationAsync(_navigationCt);
                    return Unit.Default;
                }
                if (m.Index == 0)
                {
                    m.Item.Reply(db());
                }
                else
                {
                    m.Item.Reply(Task.FromResult<Unit>(Unit.Default));
                }
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

                var imageSource = await s.GetImageSourceAsync(s.PageSelectorCandidateImageIndex, ct);
                using (var imageStream = await thumbnailManager.EnsureGetImageStreamAsync(imageSource, imageQuality: 0.5f, ct: ct))
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

        var uiSettings = new UISettings();
        if (uiSettings.AnimationsEnabled)
        {
            AnimationBuilder.Create()
                .Opacity(0, duration: TimeSpan.FromMilliseconds(1))
                .Translation(new Vector2(0, -24), duration: TimeSpan.FromMilliseconds(1))
                .Start(ButtonsContainer);
            AnimationBuilder.Create()
                .Opacity(0, duration: TimeSpan.FromMilliseconds(1))
                .Translation(new Vector2(0, 24), duration: TimeSpan.FromMilliseconds(1))
                .Start(ImageSelectorContainer);
        }

        AnimationBuilder.Create()
            .Opacity(0.001, duration: TimeSpan.FromMilliseconds(1))
            .Start(Image1);

        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        IntaractionWall.PointerPressed -= IntaractionWall_PointerPressed;
        IntaractionWall.PointerReleased -= IntaractionWall_PointerReleased;
        KeyDown -= ImageViewerPage_KeyDown;
        _messenger.Unregister<BackNavigationRequestingMessage>(this);
        
        if (_windowContext.IsPrimary)
        {
            d().FireAndForgetSafe();
        }
        async Task d()
        {
            using (await _cacheBitmapLock.LockAsync(default))
            {
                ClearCachedCanvasBitmap();
            }
            if (!_vm.NowDoubleImageView
                && _vm.CurrentDisplayImageSources.ElementAtOrDefault(0) is { } imageSource)
            {
                var connectedAnimationService = ConnectedAnimationService.GetForCurrentView();
                var anim = connectedAnimationService.PrepareToAnimate(PageTransitionHelper.BackToImageListConnectedAnimationName, Image1);
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
            if (_windowContext.IsSecondary)
            {
                animation.Cancel();
            }
            else
            {
                try
                {
                    isConnectedAnimationDone = await TryStartSingleImageAnimationAsync(animation, navigationCt);
                }
                catch (OperationCanceledException) { }
            }
        }

        try
        {
            if (isConnectedAnimationDone is false)
            {
                Image1.UpdateLayout();
                await AnimationBuilder.Create()
                   .CenterPoint(Image1.ActualSize * 0.5f, duration: TimeSpan.FromMilliseconds(1))
                   .Scale()
                       .TimedKeyFrames(ke =>
                       {
                           ke.KeyFrame(TimeSpan.FromMilliseconds(0), new(0.95f));
                           ke.KeyFrame(TimeSpan.FromMilliseconds(150), new(1.0f));
                       })
                   .Opacity(1.0, delay: TimeSpan.FromMilliseconds(10), duration: TimeSpan.FromMilliseconds(250))
                   .StartAsync(Image1, navigationCt);
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
            if (!_vm.NowDoubleImageView)
            {
                // ConnectedAnimation.Start後にタイムアウトでフォールバックのアニメーションが起動する可能性に配慮が必要
                isConnectedAnimationDone = true;
                animation.TryStart(Image1);
                AnimationBuilder.Create()
                    .Opacity(1.0, duration: TimeSpan.FromMilliseconds(1))
                    .Start(Image1);
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
            if (pt.IsContactUIElement(ButtonsContainer)) { return; }
            if (pt.IsContactUIElement(ImageSelectorContainer)) { return; }
            
            if (!IsOpenBottomMenu)
            {
                if (RightPageMoveButton.Visibility == Visibility.Visible
                    && pt.IsContactUIElementRelativeFrom(RootGrid, RightPageMoveButton)
                    && (RightPageMoveButton.Command?.CanExecute(null) ?? false))
                {
                    RightPageMoveButton.Command.Execute(null);
                    e.Handled = true;
                }
                else if (LeftPageMoveButton.Visibility == Visibility.Visible
                    && pt.IsContactUIElementRelativeFrom(RootGrid, LeftPageMoveButton)
                    && (LeftPageMoveButton.Command?.CanExecute(null) ?? false))
                {
                    LeftPageMoveButton.Command.Execute(null);
                    e.Handled = true;
                }
                else if (ToggleMenuButton.Visibility == Visibility.Visible
                    && pt.IsContactUIElementRelativeFrom(RootGrid, ToggleMenuButton)
                    && ToggleBottomMenuCommand is IRelayCommand command && command.CanExecute(null))
                {
                    command.Execute(null);
                    e.Handled = true;
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
                    //await s._vm.DisableImageDecodeWhenImageSmallerCanvasSize();
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
