using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using DryIoc.ImTools;
using FFmpegInteropX;
using I18NPortable;
using Microsoft.Graphics.Canvas;
using Microsoft.Toolkit.Uwp.UI.Animations;
using Microsoft.VisualBasic;
using PDFtoImage;
using R3;
using R3.Extensions;
using SharpCompress.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using TsubameViewer.Contracts.Notification;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.ViewModels;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.ViewModels.SourceFolders.Commands;
using TsubameViewer.Views.Converters;
using TsubameViewer.Views.Helpers;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.Media.Core;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;
using Windows.System.Display;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using ZLinq;

#nullable enable
namespace TsubameViewer.Views;

public sealed class ShortcutKeyInfo
{
    public string Label { get; set; } = "";
    public VirtualKey Key { get; set; }
    public VirtualKeyModifiers Modifier { get; set; }

    public string ToText(VirtualKey key, VirtualKeyModifiers mod)
    {
        if (mod != VirtualKeyModifiers.None)
        {
            return $"{Modifier} + {Key}";
        }
        else
        {
            return key.ToString();
        }
    }
}

[ObservableObject]
public sealed partial class MovieViewerPage : Page, ITitlebarContentAware
{
    public DataTemplate? GetContent()
    {
        return TitlebarContent;
    }

    public R3.Observable<string> ObserveTitleChanged()
    {
        return _vm.ObservePropertyChanged(x => x.MovieFile).Select(x => x?.Name ?? "");
    }

    internal readonly MovieViewerPageViewModel _vm;
    private readonly IMessenger _messenger;

    public MediaPlayer MediaPlayer => MyMediaPlayerElement.MediaPlayer;

    [ObservableProperty]
    bool _isDisplayControlUI;

    bool? _nextIsDisplayControlUI;
    DispatcherQueueTimer? _mouseCursorAutoHideTimer;

    private void ControlUIInteractionWall_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var pt = e.GetCurrentPoint(null);
        if (pt.Properties.IsMiddleButtonPressed)
        {
            _ = ToggleFullScreen();
        }

        if (pt.Properties.IsLeftButtonPressed)
        {
            if (ShortcutKeyGuideUIContainer.Visibility == Visibility.Visible)
            {
                ShortcutKeyGuideUIContainer.Visibility = Visibility.Collapsed;
                return;
            }
            if (PlayerState == MediaPlaybackState.Paused)
            {
                _nextIsDisplayControlUI = !IsDisplayControlUI;
            }
            else if (pt.PointerDevice.PointerDeviceType == PointerDeviceType.Touch)
            {
                _nextIsDisplayControlUI = !IsDisplayControlUI;
            }
            else
            {
                if (IsDisplayControlUI)
                {
                    _nextIsDisplayControlUI = false;
                }
                else
                {
                    _nextIsDisplayControlUI = true;
                }
                _lastTappedTime = TimeProvider.System.GetTimestamp();                
            }
        }
    }

    private void ControlUIInteractionWall_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _lastHideDisplayControlUIWithAutoHide = false;
        if (MySwipeDistanceBehavior.NowManipulation)
        {
            _nextIsDisplayControlUI = null;
        }

        var pt = e.GetCurrentPoint(null);        
        if (_nextIsDisplayControlUI is { } b)
        {
            IsDisplayControlUI = b;
            _nextIsDisplayControlUI = null;

            if (b)
            {
                _mouseCursorAutoHideTimer?.Start();
            }
            else
            {
                _mouseCursorAutoHideTimer?.Stop();                
            }
        }
    }

    long _lastTappedTime;


    private void ControlUIInteractionWall_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var pt = e.GetCurrentPoint(null);
        VolumeChange(pt.Properties.MouseWheelDelta > 0 ? 0.05 : -0.05);
    }

    public MovieViewerPage()
    {
        this.InitializeComponent();

        DataContext = _vm = Ioc.Default.GetRequiredService<MovieViewerPageViewModel>();
        _messenger = Ioc.Default.GetRequiredService<IMessenger>();
        Loaded += MovieViewerPage_Loaded;
        Unloaded += MovieViewerPage_Unloaded;

        _vm.ToggleFullScreenCommand = ToggleFullScreenCommand;
    }
    DirectConnectedAnimationConfiguration _animConfig = new();
    protected override async void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        MediaPlayer.Pause();

        bool isRotate = _vm.PageSettings.IsPlayerRotateEnabled && MediaPlayer.PlaybackSession.PlaybackRotation is MediaRotation.Clockwise90Degrees or MediaRotation.Clockwise270Degrees;
        bool isStretchAsFill = MyMediaPlayerElement.Stretch is Stretch.Fill or Stretch.UniformToFill;
        if (_vm.MovieFile?.Path is { } itemPath
            && !isRotate
            && !isStretchAsFill)
        {
            var imageContainer = PlayerContainer;
            RefreshPlayerContainerSize(MediaPlayer, PlayerContainer, PageRoot);
            var connectedAnimationService = ConnectedAnimationService.GetForCurrentView();            
            var anim = connectedAnimationService.PrepareToAnimate(PageTransitionHelper.BackToImageListConnectedAnimationName, imageContainer);
            try
            {
                var res = await _messenger.Send(new RequestConnectedAnimationMessage(nameof(FolderListupPage), itemPath));
                if (res is { } target)
                {
                    anim.Configuration = _animConfig;
                    anim.TryStart(target);
                }
                else { anim.Cancel(); }
            }
            catch
            {
                anim.Cancel();
            }
        }

        _mouseCursorAutoHideTimer?.Stop();
        _mouseCursorAutoHideTimer = null;
        ShowMouseCursor();
        MediaPlayer.Source = null;
        
        base.OnNavigatingFrom(e);
    }

    private async void MovieViewerPage_Unloaded(object sender, RoutedEventArgs e)
    {
        Window.Current.CoreWindow.PointerPressed -= CoreWindow_VideoPositionSlider_PointerPressed;
        Window.Current.CoreWindow.PointerReleased -= CoreWindow_VideoPositionSlider_PointerReleased;

        MediaPlayer.PlaybackSession.PlaybackStateChanged -= PlaybackSession_PlaybackStateChanged;
        MediaPlayer.PlaybackSession.NaturalDurationChanged -= PlaybackSession_NaturalDurationChanged;
        MediaPlayer.MediaFailed -= MediaPlayer_MediaFailed;

        _playbackResources?.Dispose();
        _playbackResources = null;

        PlayerContainer.Width = double.NaN;
        PlayerContainer.Height = double.NaN;
    }

    internal class DisplayRequestFacade : IDisposable
    {
        private readonly DisplayRequest _req;

        bool _isActive;
        public DisplayRequestFacade()
        {
            _req = new DisplayRequest();
        }

        public bool IsActive
        {
            get { return _isActive; }
            set
            {
                if (_isActive != value)
                {
                    if (value)
                    {
                        _req.RequestActive();
                    }
                    else
                    {
                        _req.RequestRelease();
                    }
                }
            }
        }

        bool _isDisposed;
        public void Dispose()
        {
            if (_isDisposed) { return; }
            _isDisposed = true;

            if (_isActive)
            {
                _req.RequestRelease();
            }
        }
    }

    IDisposable? _playbackResources;
    private void MovieViewerPage_Loaded(object sender, RoutedEventArgs e)
    {
        PlayerContainer.Width = double.NaN;
        PlayerContainer.Height = double.NaN;
        PlayerContainer.Opacity = 0.0001;　// FFmpeg利用時にゼロ位置の映像フレームが表示されないように
        MediaPlayer.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;
        MediaPlayer.PlaybackSession.NaturalDurationChanged += PlaybackSession_NaturalDurationChanged;        
        MediaPlayer.MediaFailed += MediaPlayer_MediaFailed;

        Window.Current.CoreWindow.PointerPressed += CoreWindow_VideoPositionSlider_PointerPressed;
        Window.Current.CoreWindow.PointerReleased += CoreWindow_VideoPositionSlider_PointerReleased;

        DisposableBuilder db = new();
        
        _lastHideDisplayControlUIWithAutoHide = false;

        var mediaPlayer = MyMediaPlayerElement.MediaPlayer;
        mediaPlayer.CommandManager.IsEnabled = true;

        var insideWindowRp = Observable.Merge(
                this.ObservePointerEntered().Select(x => x.Pointer.PointerDeviceType == PointerDeviceType.Mouse), 
                this.ObservePointerExited().Select(x => false))
#if DEBUG
            .Do(x => Debug.WriteLine($"inside window: {x}"))
#endif
            .ToReadOnlyReactiveProperty(false)
            .AddTo(ref db);

        var insideControlUIRp = Observable.Merge(
                ImageSelectorContainer.ObservePointerEntered().Select(x => true),
                ImageSelectorContainer.ObservePointerExited().Select(x => false))
#if DEBUG
            .Do(x => Debug.WriteLine($"inside ControlUI: {x}"))
#endif
            .ToReadOnlyReactiveProperty(false)
            .AddTo(ref db);        

        Window.Current.ObserveActivated()
            .Subscribe(this, static (e, s) => s._isWindowActive = e.WindowActivationState != CoreWindowActivationState.Deactivated)
            .AddTo(ref db);

        _mouseCursorAutoHideTimer?.Stop();
        _mouseCursorAutoHideTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _mouseCursorAutoHideTimer.Tick += MouseCursorMonitorTimer_Tick;
        _mouseCursorAutoHideTimer.Interval = TimeSpan.FromSeconds(2.25);
        _mouseCursorAutoHideTimer.IsRepeating = false;

        Disposable.Create(_mouseCursorAutoHideTimer, s => s.Stop());
        void MouseCursorMonitorTimer_Tick(DispatcherQueueTimer sender, object args)
        {
            if (insideWindowRp.CurrentValue
                && PlayerState == MediaPlaybackState.Playing                
                && !IsFlyoutOpen
                && ShortcutKeyGuideUIContainer.Visibility == Visibility.Collapsed)
            {
                HideMouseCursor();  
            }

            if (PlayerState == MediaPlaybackState.Playing
                && !insideControlUIRp.CurrentValue
                && !IsFlyoutOpen
                && ShortcutKeyGuideUIContainer.Visibility == Visibility.Collapsed )
            {
                IsDisplayControlUI = false;
            }
        }

        var bookmarkRp = _vm.ObservePropertyChanged(x => x.MovieFile)
            .Select(_vm, (x, vm) => x != null ? vm.BookmarkManager.GetBookmarkFacade(x.Path) : null)
            .ToReadOnlyReactiveProperty()
            .AddTo(ref db);

        _vm.ObservePropertyChanged(x => x.MovieFile)
            .SubscribeAwait((this, bookmarkRp, _mouseCursorAutoHideTimer), static async (x, state, ct) =>
            {
                var (s, bookmarkRp, mouseHideTimer) = state;
                s._playbackResources?.Dispose();

                var isLastPlaying = s.PlayerState == MediaPlaybackState.Playing;
                var lastPlayPosition = s.VideoPosition;
                s.MediaPlayer.Source = null;

                if (x == null) { return; }
                if (s.MediaPlayer == null) { return; }

                bool isFirstPlay = s._playbackResources == null;
                
                CompositeDisposable db = new();
                try
                {
                    bool firstTryFFmpeg = s._vm.PageSettings.IsFFmpegUseFirstToMediaSourceFactory 
                        || (!x.FileType.Equals(SupportedFileTypesHelper.Movie_Mp4FileType, StringComparison.Ordinal));
                    try
                    {
                        if (firstTryFFmpeg)
                        {
                            await s.OpenMediaWithFFmpegAsync(x, db, ct);
                            s.NowPlayingWithFFmpegMediaSource = true;
                        }
                        else
                        {
                            await s.OpenMediaWithDefaultAsync(x, db, ct);
                            s.NowPlayingWithFFmpegMediaSource = false;
                        }
                    }
                    catch
                    {
                        db.Dispose();
                        db = new CompositeDisposable();
                        if (firstTryFFmpeg)
                        {
                            await s.OpenMediaWithDefaultAsync(x, db, ct);
                            s.NowPlayingWithFFmpegMediaSource = false;
                        }
                        else
                        {
                            await s.OpenMediaWithFFmpegAsync(x, db, ct);
                            s.NowPlayingWithFFmpegMediaSource = true;
                        }
                    }
                }
                catch
                {
                    db.Dispose();
                    throw;
                }

                ObservableEventExtensions.FromTypedEvent<MediaPlaybackSession, object>(
                    h => s.MediaPlayer.PlaybackSession.PositionChanged += h,
                    h => s.MediaPlayer.PlaybackSession.PositionChanged -= h
                    )
                    .ObserveOnCurrentSynchronizationContext()
                    .Debounce(TimeSpan.FromSeconds(0.1))
                    .Subscribe((s, bookmarkRp), static (e, state) =>
                    {
                        var (s, bookmarkRp) = state;
                        if (s._videoPositionChangingFromCode)
                        {
                            s._videoPositionChangingFromCode = false;
                            return;
                        }

                        if (e.Sender == null) { return; }
                        var ts = e.Sender.Position;
                        s.SetVideoPositionFromCode(ts);

                        if (e.Sender.CanSeek
                            && e.Sender.NaturalDuration != TimeSpan.Zero)
                        {
                            var pos = e.Sender.Position;
                            var duration = e.Sender.NaturalDuration;
                            bookmarkRp.CurrentValue?.ReadPosition = new((float)(pos.TotalSeconds / duration.TotalSeconds));
                        }
                    })
                    .AddTo(db);

                s._playbackResources = db;

                if (x != null)
                {
                    mouseHideTimer.Start();                    
                }

                s._nowRequestPlayStart = isLastPlaying || isFirstPlay;
                s.ObservePropertyChanged(x => x.PlayerState)
                    .Where(x => x == MediaPlaybackState.Paused)
                    .Take(1)
                    .SubscribeAwait((s, bookmarkRp, lastPlayPosition, isFirstPlay), static async (x, s, ct) =>
                    {
                        var (_this, bookmarkRp, lastPlayPosition, isFirstPlay) = s;
                        if (bookmarkRp.CurrentValue is not { } bkmk) { return; }

                        if (_this.MediaPlayer.PlaybackSession.CanSeek)
                        {
                            TimeSpan ts;
                            if (isFirstPlay)
                            {
                                if (float.IsNaN(bkmk.ReadPosition.Value))
                                {
                                    bkmk.ReadPosition = new Core.Models.FolderItemListing.NormalizedPagePosition();
                                }
                                ts = _this.MediaPlayer.PlaybackSession.NaturalDuration * bkmk.ReadPosition.Value;
                                if (ts > _this.MediaPlayer.PlaybackSession.NaturalDuration - TimeSpan.FromSeconds(1))
                                {
                                    ts = TimeSpan.Zero;
                                }
                            }
                            else
                            {
                                ts = lastPlayPosition;
                            }
                            _this.MediaPlayer.PlaybackSession.Position = ts;
                        }

                        // Note: 再生後に速度変更する。そうしないと動き出し数フレームが２回再生される症状がでるため。
                        _this.MediaPlayer.PlaybackSession.PlaybackRate = _this._vm.PageSettings.PlaybackRate;

                        // FFmpeg利用時にゼロ位置の映像フレームが表示されないように
                        if (_this.NowPlayingWithFFmpegMediaSource)
                        {
                            await Task.Delay(200);
                        }
                        if (_this._nowRequestPlayStart)
                        {
                            _this._nowRequestPlayStart = false;
                            _this.MediaPlayer.Play();
                        }

                        // FFmpeg利用時にゼロ位置の映像フレームが表示されないように
                        _this.PlayerContainer.Opacity = 1;
                    });
            })
            .AddTo(ref db);

        _vm.PageSettings.ObservePropertyChanged(x => x.IsFFmpegUseFirstToMediaSourceFactory, false)
            .Subscribe(this, static (x, s) => 
            {
                var file = s._vm.MovieFile;
                s._vm.MovieFile = null;
                s._vm.MovieFile = file;
            })
            .AddTo(ref db);
        
        this.ObservePropertyChanged(x => x.PlayerState)
            .Subscribe(new DisplayRequestFacade(), static (state, s)  => 
            {
                s.IsActive = state == MediaPlaybackState.Playing;
            }, (result, s) => s.Dispose())
            .AddTo(ref db);

        InitializeZoomReaction(ref db);

        Observable.Merge(
            MouseDevice.GetForCurrentView().ObserveMouseMoved().AsUnitObservable(),
            insideWindowRp.AsUnitObservable(),
            Window.Current.ObserveActivated().AsUnitObservable()
            )
            .DebounceFrame(1)
            .Subscribe((this, insideWindowRp, insideControlUIRp, _mouseCursorAutoHideTimer), static (x, s) =>
            {
                var (_this, insideWindowRp, insideControlUIRp, timer) = s;                
                _this.ShowMouseCursor();
                timer.Stop();
                if (TimeProvider.System.GetElapsedTime(_this._lastTappedTime) < TimeSpan.FromMilliseconds(250))
                {
                    // タップ動作中はスキップ
                    Debug.WriteLine("skip");
                    if (_this.IsDisplayControlUI)
                    {
                        _this.ShowMouseCursor();
                    }
                    else
                    {
                        _this.HideMouseCursor();
                    }
                    return;
                }
                if (!s.insideWindowRp.CurrentValue
                    && !s.Item1.IsFlyoutOpen
                    && s.Item1.PlayerState == MediaPlaybackState.Playing)
                {
                    _this.IsDisplayControlUI = false;
                }
                else if (!insideControlUIRp.CurrentValue
                    && s.Item1.PlayerState == MediaPlaybackState.Playing
                    && !s.Item1.MySwipeDistanceBehavior.NowManipulation)
                {
                    _this.IsDisplayControlUI = true;
                    timer.Start();
                }                                
            })
            .AddTo(ref db);        


        _vm.PageSettings.ObservePropertyChanged(x => x.IsPlayerStretchEnabled)
            .SubscribeAwait(this, static async (x, s, ct) => 
            {
                await s.SetPlayerStretch_Internal(x ? s._vm.PageSettings.PlayerStretch : Stretch.Uniform);                
            })
            .AddTo(ref db);

        _vm.PageSettings.ObservePropertyChanged(x => x.IsPlayerRotateEnabled)
            .SubscribeAwait(this, static async (x, s, ct) =>
            {
                await s.SetPlayerRotate_Internal(x ? s._vm.PageSettings.PlayerRotate : MediaRotation.None);
            })
            .AddTo(ref db);

        HandleWindowDisplayState(ref db);
        HandleSoundVolumeChanged(ref db);
        HandleLoopingChanged(ref db);
        HandlePlaybackRateChanged(ref db);

        db.Build().RegisterTo(this.GetCancellationTokenOnUnloaded());
    }

    async Task OpenMediaWithDefaultAsync(StorageFile x, ICollection<IDisposable> db, CancellationToken ct)
    {
        var mediaSource = MediaSource.CreateFromStorageFile(x);
        db.Add(mediaSource);
        var playbackItem = new MediaPlaybackItem(mediaSource);
        MediaPlayer.Source = playbackItem;
        ct.ThrowIfCancellationRequested();

        var props = playbackItem.GetDisplayProperties();
        props.Type = Windows.Media.MediaPlaybackType.Video;
        props.VideoProperties.Title = x.DisplayName;

        try
        {
            using var stream = await _vm.ThumbnailManager.GetThumbnailImageFromPathAsync(x.Path, ct);
            var memoryStream = new MemoryStream((int)stream.Length);
            stream.CopyTo(memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin);
            props.Thumbnail = RandomAccessStreamReference.CreateFromStream(memoryStream.AsRandomAccessStream());
        }
        catch { }
        playbackItem.ApplyDisplayProperties(props);
    }

    [ObservableProperty]
    bool _nowPlayingWithFFmpegMediaSource;

    TimeSpan _oneFrameTime;

    async Task OpenMediaWithFFmpegAsync(StorageFile x, ICollection<IDisposable> db, CancellationToken ct)
    {
        var fileStream = await x.OpenReadAsync();
        var ms = await FFmpegMediaSource.CreateFromStreamAsync(fileStream);
        // Note: PlaybackSession 設定するとむしろ壊れる
        //ms.PlaybackSession = MediaPlayer.PlaybackSession;
        db.Add(ms);
        await ms.OpenWithMediaPlayerAsync(MediaPlayer);
        try
        {
            var mss = ms.GetMediaStreamSource();
            mss.MaxSupportedPlaybackRate = 4.0;
        }
        catch { }

        _oneFrameTime = TimeSpan.FromSeconds(1d / ms.CurrentVideoStream.FramesPerSecond);
    }

    private void MediaPlayer_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        Debug.WriteLine(args.Error);
        Debug.WriteLine(args.ErrorMessage);
        Debug.WriteLine(args.ExtendedErrorCode.ToString());
    }

    void RefreshPlayerContainerSizeWithCurrentState()
    {
        RefreshPlayerContainerSize(
            MediaPlayer, 
            PlayerContainer, 
            PageRoot);
    }

    void RefreshPlayerContainerSize(MediaPlayer mediaPlayer, 
        Grid container, 
        Grid pageRoot)
    {
        // 1. 動画の本来の解像度（幅と高さ）を取得
        double videoWidth = mediaPlayer.PlaybackSession.NaturalVideoWidth;
        double videoHeight = mediaPlayer.PlaybackSession.NaturalVideoHeight;

        if (videoWidth == 0 || videoHeight == 0) return;

        // 2. アプリ側の表示領域（最大で広げられるサイズ）の基準を決める
        // 例として、現在のウィンドウサイズ（または親コンテナのサイズ）を取得
        double maxAllowedWidth = pageRoot.ActualWidth;
        double maxAllowedHeight = pageRoot.ActualHeight;

        // 3. アスペクト比を維持したまま、最大のサイズを計算
        double aspectRatio = videoWidth / videoHeight;
        double targetWidth = maxAllowedWidth;
        double targetHeight = maxAllowedWidth / aspectRatio;

        if (targetHeight > maxAllowedHeight)
        {
            targetHeight = maxAllowedHeight;
            targetWidth = maxAllowedHeight * aspectRatio;
        }

        // 4. 親コンテナ（またはプレイヤー自体）のサイズをジャストサイズに変更
        container.Width = targetWidth;
        container.Height = targetHeight;
    }

    [RelayCommand]
    void ExitPlayer()
    {
        if (ShortcutKeyGuideUIContainer.Visibility == Visibility.Visible)
        {
            ShortcutKeyGuideUIContainer.Visibility = Visibility.Collapsed;
            return;
        }
        else
        {
            _messenger.Send(new BackNavigationRequestMessage());
        }
    }

    #region ShortcutKey

    [ObservableProperty]
    ArraySegment<ShortcutKeyInfo>? _shortcutKeys;

    [RelayCommand]
    void ToggleDisplayShortcutKeyGuideUI()
    {
        if (ShortcutKeys == null)
        {
            var shortcuts = ShortcutKeyButtonsContainer.Children
                .AsValueEnumerable()
                .Cast<Button>()
                .Where(x => x.Tag is string s && !string.IsNullOrEmpty(s))
                .Select(static x => new ShortcutKeyInfo
                {
                    Label = (string)x.Tag,
                    Key = x.KeyboardAccelerators[0].Key,
                    Modifier = x.KeyboardAccelerators[0].Modifiers
                })
                .ToArrayPool();
            shortcuts
                .RegisterTo(this.GetCancellationTokenOnUnloaded());
            ShortcutKeys = shortcuts.ArraySegment;
        }
        ShortcutKeyGuideUIContainer.Visibility = (ShortcutKeyGuideUIContainer.Visibility == Visibility.Collapsed).TrueToVisible();
    }

    private void CloseButton_ShortcutKeyGuideUIContainer_Tapped(object sender, TappedRoutedEventArgs e)
    {
        ShortcutKeyGuideUIContainer.Visibility = Visibility.Collapsed;
    }

    #endregion

    #region Display Style



    bool _isWindowActive = false;
    bool _lastHideDisplayControlUIWithAutoHide = false;

    // マウスカーソルを非表示にする
    private void HideMouseCursor()
    {
        // 現在のウィンドウのカーソルに null を設定
        Window.Current.CoreWindow.PointerCursor = null;
    }


    // マウスカーソルを再表示する（通常の矢印カーソル）
    private void ShowMouseCursor()
    {
        // Arrow（矢印）タイプを指定して再設定
        Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 0);
    }



    [ObservableProperty]
    bool _nowFullScreenMode;


    void HandleWindowDisplayState(ref DisposableBuilder db)
    {
        var observeWindowActivate = Observable.FromEvent<WindowSizeChangedEventHandler, WindowSizeChangedEventArgs>(
           conversion => (sender, args) => conversion(args),
           h => Window.Current.SizeChanged += h,
           h => Window.Current.SizeChanged -= h);

        var appView = ApplicationView.GetForCurrentView();
        observeWindowActivate.Debounce(TimeSpan.FromMilliseconds(50))
            .Subscribe((this, appView ), (args, s) => 
            {                
                s.Item1.NowFullScreenMode = s.appView.IsFullScreenMode;
            })
            .AddTo(ref db);

        NowFullScreenMode = appView.IsFullScreenMode;
    }

    [ObservableProperty]
    bool _isFlyoutOpen;

    // フライアウトが開いている間は自動非表示を止める
    private void MenuFlyout_Opened(object sender, object e)
    {
        IsFlyoutOpen = true;
    }

    private void MenuFlyout_Closed(object sender, object e)
    {
        IsFlyoutOpen = false;
        _mouseCursorAutoHideTimer?.Stop();
        _mouseCursorAutoHideTimer?.Start();
    }

    #endregion

    #region Playback

    bool _nowRequestPlayStart;

    private void PlaybackSession_PlaybackStateChanged(MediaPlaybackSession sender, object args)
    {
        Observable.NextFrame()
            .Subscribe((this, sender), (_, s) => s.Item1.PlayerState = s.sender.PlaybackState);
    }

    [ObservableProperty]
    MediaPlaybackState _playerState;

    public Visibility IsPalyerPreparing(MediaPlaybackState state)
    {
        return (state is MediaPlaybackState.Opening or MediaPlaybackState.Buffering).TrueToVisible();
    }

    public bool TrueToPlayerPlaying(MediaPlaybackState state)
    {
        return state is MediaPlaybackState.Playing;
    }


    [RelayCommand]
    void TogglePlayPause()
    {
        if (MediaPlayer == null) { return; }
        if (MediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Paused)
        {
            var ps = MediaPlayer.PlaybackSession;
            if (ps.Position > ps.NaturalDuration - TimeSpan.FromSeconds(1))
            {
                ps.Position = TimeSpan.Zero;
            }
            MediaPlayer.Play();
        }
        else if (MediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
        {
            MediaPlayer.Pause();
        }
    }

    void HandleLoopingChanged(ref DisposableBuilder db)
    {
        _vm.PageSettings.ObservePropertyChanged(x => x.IsRepeat)
            .Subscribe(this, (x, s) => s.MediaPlayer.IsLoopingEnabled = x)
            .AddTo(ref db);
    }


    #endregion


    #region Position and Duration

    [ObservableProperty]
    bool _isDurationAvairable = true;

    private void PlaybackSession_NaturalDurationChanged(MediaPlaybackSession sender, object args)
    {
        Observable.NextFrame()
            .Subscribe((this, sender), (_, s) =>
            {
                if (s.sender.NaturalDuration != default
                    && s.sender.NaturalDuration.TotalDays < 1)
                {
                    s.Item1.IsDurationAvairable = true;
                    VideoPositionSlider.Maximum = s.sender.NaturalDuration.TotalSeconds;
                }
                else
                {
                    s.Item1.IsDurationAvairable = false;
                    VideoPositionSlider.Maximum = TimeSpan.FromDays(1).TotalSeconds;
                }
                s.Item1.VideoDuration = s.sender.NaturalDuration;
            });
    }


    [ObservableProperty]
    TimeSpan _videoDuration;

    [ObservableProperty]
    TimeSpan _videoPosition;

    string ToHHMMSSString(TimeSpan t)
    {
        return t.ToString("hh\\:mm\\:ss");
    }

    double ToTotalSeconds(TimeSpan t)
    {
        var oneDay = TimeSpan.FromDays(1);
        if (t > oneDay)
        {
            return oneDay.TotalSeconds;
        }
        else
        {
            return t.TotalSeconds;
        }
    }

    bool _videoPositionChangingFromCode;
    void SetVideoPositionFromCode(TimeSpan ts)
    {
        if (_videoPositionChangingFromCode) { return; }
        _videoPositionChangingFromCode = true;
        try
        {
            VideoPosition = ts;
            if (IsDurationAvairable)
            {
                VideoPositionSlider.Value = ToTotalSeconds(ts);
            }
        }
        finally
        {
            _videoPositionChangingFromCode = false;
        }
    }

    private void VideoPositionSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (((FrameworkElement)sender).IsLoaded == false) { return; }
        if (_videoPositionChangingFromCode) 
        {
            return; 
        }

        var ts = TimeSpan.FromSeconds((double)e.NewValue);
        _videoPositionChangingFromCode = true;
        MediaPlayer.PlaybackSession.Position = ts;
    }

    bool _prevPlaying;
    private void CoreWindow_VideoPositionSlider_PointerPressed(CoreWindow sender, PointerEventArgs args)
    {
        if (args.IsContactUIElement(VideoPositionSlider, Window.Current.Content))
        {
            Debug.WriteLine("IsContactUIElement(PlaybackRateSlider)");
            _prevPlaying = PlayerState is MediaPlaybackState.Playing;
            MediaPlayer.Pause();
        }
    }

    private void CoreWindow_VideoPositionSlider_PointerReleased(CoreWindow sender, PointerEventArgs args)
    {
        if (args.IsContactUIElement(VideoPositionSlider, Window.Current.Content))
        {
            if (_prevPlaying)
            {
                _prevPlaying = false;
                MediaPlayer.Play();
            }
            else
            {
                // おまじない：一時停止中の再生位置移動後にフレームが更新されない問題への対処
                //MediaPlayer.StepBackwardOneFrame();
                MediaPlayer.StepForwardOneFrame();
            }
        }
    }

    [RelayCommand]
    void BackwardOneFrame()
    {
        if (MediaPlayer == null) { return; }
        MediaPlayer.StepBackwardOneFrame();
        // Note: FFmpeg利用時に前フレーム移動後に表示更新されないことがある。仕方なくスルーすることに。
    }

    [RelayCommand]
    void ForwardOneFrame()
    {
        MediaPlayer.StepForwardOneFrame();
    }


    void SeekPlaybackPosition(TimeSpan relativeTime)
    {
        MediaPlayer.PlaybackSession.Position = MediaPlayer.PlaybackSession.Position + relativeTime;
    }

    private void MySwipeDistanceBehavior_Invoked(Behaviors.SwipeDistanceBehavior sender, Behaviors.SwipeDistanceInvokedEventArgs args)
    {
        if (!IsDurationAvairable) { return; }

        _nextIsDisplayControlUI = null;

        if (args.X != 0)
        {
            bool isPlaying = PlayerState == MediaPlaybackState.Playing;
            var ts = TimeSpan.FromSeconds(args.X);
            MediaPlayer.PlaybackSession.Position = MediaPlayer.PlaybackSession.Position + ts;

            // おまじない：一時停止中の再生位置移動後にフレームが更新されない問題への対処
            if (!isPlaying)
            {
                MediaPlayer.StepForwardOneFrame();
            }
        }
        if (args.Y != 0)
        {
            VolumeChange(args.Y * -0.05);
        }
    }

    string ProgressXToTimeText(double progressX)
    {
        if (progressX != 0)
        {
            return TimeSpanHelper.FormatTimeSpan(TimeSpan.FromSeconds(progressX));
        }
        else { return ""; }
    }

    double EmptyProgressAsOpacity(double progressX, bool isEnabled)
    {
        if (!isEnabled) { return 0; }
        if (Math.Abs(progressX) < 1) { return 0; }
        
        return 1;
    }

    TimeSpan PlaybackPositionChangeBackward { get; } = TimeSpan.FromSeconds(-5);
    TimeSpan PlaybackPositionChangeForward { get; } = TimeSpan.FromSeconds(5);

    [RelayCommand]
    void PlaybackPositionChange(TimeSpan relativeTime)
    {
        SeekPlaybackPosition(relativeTime);
    }


    [RelayCommand]
    void SetPlaybackPositionWithPercent(double videoPositionInPercent)
    {
        if (!IsDurationAvairable) { return; }
        MediaPlayer.PlaybackSession.Position = (VideoDuration * (videoPositionInPercent * 0.01));        
    }


    #endregion

    #region Playback Rate



    void HandlePlaybackRateChanged(ref DisposableBuilder db)
    {
    }

    string ToPlaybackRateString(double rate)
    {
        return $"x{rate:F2}";
    }

    readonly double MinPlaybackRate = 0.1;
    readonly double MaxPlaybackRate = 4;

    bool _nowPlaybackRateChangingFromCode;
    void SetPlaybackRateFromCode(double playbackRate)
    {
        _nowPlaybackRateChangingFromCode = true;
        try
        {
            _vm.PageSettings.PlaybackRate = Math.Clamp(playbackRate, MinPlaybackRate, MaxPlaybackRate);
        }
        finally
        {
            _nowPlaybackRateChangingFromCode = false;
        }
    }
    
    private void PlaybackRateSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (((FrameworkElement)sender).IsLoaded is false) { return; }
        if (_nowPlaybackRateChangingFromCode) { return; }

        SetPlaybackRateFromCode((double)e.NewValue);
        MediaPlayer.PlaybackSession.PlaybackRate = _vm.PageSettings.PlaybackRate;
    }


    [RelayCommand]
    void SetPlaybackRate(double d)
    {
        SetPlaybackRateFromCode(d);
        MediaPlayer.PlaybackSession.PlaybackRate = _vm.PageSettings.PlaybackRate;
    }

    [RelayCommand]
    void SetPlaybackRateToNext()
    {
        var rate = MediaPlayer.PlaybackSession.PlaybackRate;
        float roundedRate = Math.DivRem((int)(rate*100), 25, out var _) * 0.25f;
        var nextRate = Math.Min(roundedRate + 0.25f, MaxPlaybackRate);
        SetPlaybackRateFromCode(nextRate);
        MediaPlayer.PlaybackSession.PlaybackRate = nextRate;
    }

    private void Button_Tapped(object sender, TappedRoutedEventArgs e)
    {
        SetPlaybackRateToNext();
    }


    [RelayCommand]
    void SetPlaybackRateToPrev()
    {
        var rate = MediaPlayer.PlaybackSession.PlaybackRate;
        float roundedRate = Math.DivRem((int)(rate * 100), 25, out var _) * 0.25f;
        var prevRate = Math.Max(roundedRate - 0.25f, MinPlaybackRate);
        SetPlaybackRateFromCode(prevRate);
        MediaPlayer.PlaybackSession.PlaybackRate = prevRate;
    }

    #endregion


    #region Sound Volume

    AnimationBuilder _soundVolumeNotificationAnimation = AnimationBuilder.Create()
        .TimedKeyFrames<double>("Opacity",
            d => d.KeyFrame(TimeSpan.FromSeconds(0.125), 1)
            .KeyFrame(TimeSpan.FromSeconds(1.80), 1)
            .KeyFrame(TimeSpan.FromSeconds(1.925), 0));

    void HandleSoundVolumeChanged(ref DisposableBuilder db)
    {
        SetSoundVolume(_vm.PageSettings.SoundVolume, _vm.PageSettings.IsMuted);
        MediaPlayer.Volume = 0;
        float increaseVolumeUnit = (float)_vm.PageSettings.SoundVolume / 30f; // 0.5秒
        var timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(16);
        timer.Tick += (s, e) => 
        {
            var nextVolume = MediaPlayer.Volume + increaseVolumeUnit;
            if (nextVolume > _vm.PageSettings.SoundVolume)
            {
                MediaPlayer.Volume = _vm.PageSettings.SoundVolume;
                s.Stop();                
            }
            else
            {                
                MediaPlayer.Volume += increaseVolumeUnit;
            }

            //Debug.WriteLine($"volume smoothing: {MediaPlayer.Volume*100:F0}%");
        };
        timer.Start();
        Disposable.Create(timer, s => s.Stop())
            .AddTo(ref db);
        ControlUI_SoundVolumeSlider.ValueChanged -= ControlUI_SoundVolumeSlider_ValueChanged;
        ControlUI_SoundVolumeSlider.ValueChanged += ControlUI_SoundVolumeSlider_ValueChanged;
        Disposable.Create(this, s => s.ControlUI_SoundVolumeSlider.ValueChanged -= s.ControlUI_SoundVolumeSlider_ValueChanged)
            .AddTo(ref db);

        this.ObservePropertyChanged(x => x.SoundVolume_Display, false)
            .Subscribe((this), (x, s) =>
            {
                s.MediaPlayer.Volume = x;
                s._soundVolumeNotificationAnimation.Start(s.SoundVolumeNotifier);
            })
            .AddTo(ref db);
    }


    [ObservableProperty]
    double _soundVolume_Display = 0.5;

    [RelayCommand]
    void ToggleIsMuted()
    {
        _vm.PageSettings.IsMuted = !_vm.PageSettings.IsMuted;
        MediaPlayer.IsMuted = _vm.PageSettings.IsMuted;
        if (_vm.PageSettings.IsMuted)
        {
            SetSoundVolume(0, true);
        }
        else
        {
            SetSoundVolume(_vm.PageSettings.SoundVolume, false);
        }
        
    }


    void SetSoundVolume(double vol, bool isMute)
    {
        _nowSoundVolumeChanging = true;
        try
        {
            if (isMute)
            {
                SoundVolume_Display = 0;
            }
            else
            {
                SoundVolume_Display = vol;
            }

            MediaPlayer.IsMuted = isMute;
        }
        finally
        {
            _nowSoundVolumeChanging = false;
        }
    }

    bool _nowSoundVolumeChanging;
    private void ControlUI_SoundVolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (((FrameworkElement)sender).IsLoaded is false) { return; }
        if (_nowSoundVolumeChanging) { return; }

        double volume = Math.Clamp((double)e.NewValue, 0.0, 1.0);
        SetSoundVolumeFromCode(volume);
        if (SoundVolume_Display != 0)
        {
            _vm.PageSettings.SoundVolume = SoundVolume_Display;
        }

    }

    void SetSoundVolumeFromCode(double volume)
    {
        _nowSoundVolumeChanging = true;
        try
        {
            bool isMute = volume == 0;
            SoundVolume_Display = volume;            
            MediaPlayer.Volume = volume;
            MediaPlayer.IsMuted = isMute;
            _vm.PageSettings.IsMuted = isMute;
        }
        finally
        {
            _nowSoundVolumeChanging = false;
        }
    }

    private void ControlUI_SoundVolumeSlider_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (SoundVolume_Display != 0)
        {
            _vm.PageSettings.SoundVolume = SoundVolume_Display;
        }
    }

    [RelayCommand]
    void VolumeChange(double normalizedRelativeValue)
    {
        double vol = SoundVolume_Display == 0 ? _vm.PageSettings.SoundVolume : SoundVolume_Display + normalizedRelativeValue;
        SetSoundVolumeFromCode(Math.Clamp(vol, 0.0, 1.0));
    }



    #endregion




    private void Page2MenuFlyout_Opening(object sender, object e)
    {

    }

    private void ForceClosePage(object sender, RoutedEventArgs e)
    {        
        _messenger.Send(new BackNavigationRequestMessage());
    }

    [RelayCommand]
    void BackNavigationRequest()
    {
        MediaPlayer.Pause();
        _messenger.Send(new BackNavigationRequestMessage());
    }


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

    private void InitializeZoomReaction(ref DisposableBuilder db)
    {
        CurrentZoomFactorIndex = GetDefaultZoomFactorListIndex();
        _CanvasHalfSize = MyMediaPlayerElement.ActualSize * 0.5f;
        ElementCompositionPreview.GetElementVisual(MyMediaPlayerElement).CenterPoint = new Vector3(_CanvasHalfSize, 0);

        MyMediaPlayerElement.ObserveSizeChanged()
            .Subscribe(x =>
            {
                _CanvasHalfSize = x.NewSize.ToVector2() * 0.5f;
            })
            .AddTo(ref db);
        _vm.ObservePropertyChanged(x => x.MovieFile)
            .Subscribe(_ =>
            {
                ZoomFactor = 1.0;
                CurrentZoomFactorIndex = GetDefaultZoomFactorListIndex();
            })
            .AddTo(ref db);
        this.ObserveDependencyProperty(ZoomFactorProperty)
            .Select(x => this.ZoomFactor)
            .Subscribe(zoom =>
            {
                IsZoomingEnabled = zoom != 1.0;
                AnimationBuilder.Create().Scale(zoom, duration: ZoomDuration).Start(MyMediaPlayerElement);
            })
            .AddTo(ref db);
        this.ObserveDependencyProperty(ZoomCenterProperty)
            .Select(x => this.ZoomCenter)
            .Subscribe(center =>
            {
                if (_nowZoomCenterMovingWithPointer is false)
                {
                    ZoomCenterAb.CenterPoint(center, duration: ZoomDuration, easingType: EasingType.Quartic, easingMode: EasingMode.EaseOut).Start(MyMediaPlayerElement);
                }
            })
            .AddTo(ref db);

        ZoomCenter = _CanvasHalfSize;        
    }

    private void IntaractionWall_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
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

    private void ReversableGoPrev()
    {
    }

    private void ReversableGoNext()
    {
    }

    private void ToggleOpenCloseBottomUI()
    {
    }

    bool _nowZoomCenterMovingWithPointer;

    private void IntaractionWall_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
    {
        _startZoomFactor = (float)ZoomFactor;
        _nowZoomCenterMovingWithPointer = true;
    }

    float _startZoomFactor;
    float _sumScale;
    private void MyMediaPlayerElement_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
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
                    var visual = ElementCompositionPreview.GetElementVisual(MyMediaPlayerElement);
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
                var visual = ElementCompositionPreview.GetElementVisual(MyMediaPlayerElement);
                visual.CenterPoint = new Vector3(ZoomCenter, 0);
            }
        }
    }


    RelayCommand<PointerRoutedEventArgs> _ZoomUpCommand;
    public RelayCommand<PointerRoutedEventArgs> ZoomUpCommand => _ZoomUpCommand
        ??= new RelayCommand<PointerRoutedEventArgs>(args =>
        {
            var targetUI = MyMediaPlayerElement;
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
            var targetUI = MyMediaPlayerElement;
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
        var range = MyMediaPlayerElement.ActualSize;
        var x = Math.Clamp(center.X, -range.X, range.X);
        var y = Math.Clamp(center.Y, -range.Y, range.Y);
        return new Vector2(x, y);
    }

    RelayCommand _ZoomUpWithControllerCommand;
    public RelayCommand ZoomUpWithControllerCommand => _ZoomUpWithControllerCommand
        ??= new RelayCommand(() =>
        {
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
            if (ZoomFactor > 1.0f)
            {
                ZoomCenter += new Vector2(ControlerZoomCenterMoveAmount * GetZoomCenterMoveingFactorForController(), 0);
            }
        });

    RelayCommand _ZoomCenterMoveLeftCommand;
    public RelayCommand ZoomCenterMoveLeftCommand => _ZoomCenterMoveLeftCommand
        ??= new RelayCommand(() =>
        {
            if (ZoomFactor > 1.0f)
            {
                ZoomCenter += new Vector2(-ControlerZoomCenterMoveAmount * GetZoomCenterMoveingFactorForController(), 0);
            }
        });

    RelayCommand _ZoomCenterMoveUpCommand;
    public RelayCommand ZoomCenterMoveUpCommand => _ZoomCenterMoveUpCommand
        ??= new RelayCommand(() =>
        {
            if (ZoomFactor > 1.0f)
            {
                ZoomCenter += new Vector2(0, -ControlerZoomCenterMoveAmount * GetZoomCenterMoveingFactorForController());
            }
        });

    RelayCommand _ZoomCenterMoveDownCommand;

    public RelayCommand ZoomCenterMoveDownCommand => _ZoomCenterMoveDownCommand
        ??= new RelayCommand(() =>
        {
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


    [RelayCommand]
    async Task ToggleFullScreen()
    {
        bool isPlaying = PlayerState == MediaPlaybackState.Playing;
        var appView = ApplicationView.GetForCurrentView();
        if (appView.IsFullScreenMode)
        {
            appView.ExitFullScreenMode();
        }
        else
        {
            appView.TryEnterFullScreenMode();
        }
    }

    [RelayCommand]
    void TogglePlayerMirror()
    {
        _vm.PageSettings.IsHorizontalMirror = !_vm.PageSettings.IsHorizontalMirror;
    }

    async Task SetPlayerStretch_Internal(Stretch stretch)
    {
        if (MediaPlayer == null) { return; }
        if (MyMediaPlayerElement.Stretch == stretch) { return; }

        MyMediaPlayerElement.Stretch = stretch;
    }

    [RelayCommand]
    async Task SetPlayerStretch(Stretch stretch)
    {
        if (stretch != Stretch.Uniform)
        {
            _vm.PageSettings.PlayerStretch = stretch;
            _vm.PageSettings.IsPlayerStretchEnabled = true;
        }
        else
        {
            _vm.PageSettings.IsPlayerStretchEnabled = false;
        }
        await SetPlayerStretch_Internal(stretch);
    }


    [RelayCommand]
    void TogglePlayerStretch()
    {
        _vm.PageSettings.IsPlayerStretchEnabled = !_vm.PageSettings.IsPlayerStretchEnabled;
    }

    Windows.Media.MediaProperties.MediaRotation GetNextRotate(Windows.Media.MediaProperties.MediaRotation rotate)
    {
        return rotate switch
        {
            Windows.Media.MediaProperties.MediaRotation.None => Windows.Media.MediaProperties.MediaRotation.Clockwise90Degrees,
            Windows.Media.MediaProperties.MediaRotation.Clockwise90Degrees => Windows.Media.MediaProperties.MediaRotation.Clockwise180Degrees,
            Windows.Media.MediaProperties.MediaRotation.Clockwise180Degrees => Windows.Media.MediaProperties.MediaRotation.Clockwise270Degrees,
            Windows.Media.MediaProperties.MediaRotation.Clockwise270Degrees => Windows.Media.MediaProperties.MediaRotation.None,
            _ => throw new NotSupportedException(),
        };
    }

    async Task SetPlayerRotate_Internal(MediaRotation rotate)
    {
        if (MediaPlayer == null) { return; }
        if (MediaPlayer.PlaybackSession.PlaybackRotation == rotate) { return; }

        MediaPlayer.PlaybackSession.PlaybackRotation = rotate;
    }

    [RelayCommand]
    async Task SetPlayerRotate(MediaRotation rotate)
    {
        if (rotate != Windows.Media.MediaProperties.MediaRotation.None)
        {
            _vm.PageSettings.PlayerRotate = rotate;
            _vm.PageSettings.IsPlayerRotateEnabled = true;
        }        
        else
        {
            _vm.PageSettings.IsPlayerRotateEnabled = false;
        }
        await SetPlayerRotate_Internal(rotate);
    }

    [RelayCommand]
    void TogglePlayerRotate()
    {
        _vm.PageSettings.IsPlayerRotateEnabled = !_vm.PageSettings.IsPlayerRotateEnabled;
    }


    [RelayCommand]
    async Task SetThumbnailImageAsync()
    {
        if (MediaPlayer == null) { return; }
        if (_vm.MovieFile is not { } movieFile) { return; }

        var ct = this.GetCancellationTokenOnUnloaded();
        try
        {
            var videoPosition = MediaPlayer.PlaybackSession.Position;
            using (var stream = _vm.RecyclableMemoryStreamManager.GetStream())
            {
                if (!IsDurationAvairable)
                {
                    var videoSize = new Size(MediaPlayer.PlaybackSession.NaturalVideoWidth, MediaPlayer.PlaybackSession.NaturalVideoHeight);
                    var renderSize = MyMediaPlayerElement.RenderSize;
                    double videoAspect = videoSize.Width / videoSize.Height;
                    double renderAspect = renderSize.Width / renderSize.Height;
                    Rect sourceRect;
                    double scaledWidth = renderSize.Height * videoAspect;
                    double scaledHeight = renderSize.Width / videoAspect;
                    sourceRect = videoAspect < renderAspect
                        ? new Rect(0, 0, scaledWidth, renderSize.Height)
                        : new Rect(0, 0, renderSize.Width, scaledHeight);
                    using CanvasRenderTarget crt = new(CanvasDevice.GetSharedDevice(), (float)sourceRect.Width, (float)sourceRect.Height, DisplayInformation.GetForCurrentView().LogicalDpi);
                    MediaPlayer.CopyFrameToVideoSurface(crt, sourceRect);
                    await crt.SaveAsync(stream.AsRandomAccessStream(), CanvasBitmapFileFormat.Jpeg); // JpegXR is can not decode skiasharp
                }
                else
                {
                    if (NowPlayingWithFFmpegMediaSource)
                    {
                        await Task.Run(async () =>
                        {
                            using var movieStream = await movieFile.OpenReadAsync().AsTask(ct);
                            using var fg = await FrameGrabber.CreateFromStreamAsync(movieStream).AsTask(ct);
                            fg.DecodePixelHeight = 200;
                            using var frame = await fg.ExtractVideoFrameAsync(videoPosition, true, 0).AsTask(ct);
                            await frame.EncodeAsJpegAsync(stream.AsRandomAccessStream()).AsTask(ct);
                        }, ct);
                    }
                    else
                    {
                        var clip = await MediaClip.CreateFromFileAsync(movieFile);
                        var mc = new MediaComposition()
                        {
                            Clips = { clip },
                        };
                        using var frame = await mc.GetThumbnailAsync(videoPosition, 0, 200, VideoFramePrecision.NearestFrame);
                        using var bitmap = await CanvasBitmap.LoadAsync(CanvasDevice.GetSharedDevice(), frame);
                        await bitmap.SaveAsync(stream.AsRandomAccessStream(), CanvasBitmapFileFormat.Jpeg);  // JpegXR is can not decode skiasharp
                    }
                }
                stream.Seek(0, SeekOrigin.Begin);
                await _vm.ThumbnailManager.SetThumbnailAsync(_vm.MovieFile, stream, true, ct);
            }
            
            
            _messenger.Send(new ThumbnailImageUpdateRequestMessage(_vm.MovieFile.Path));
            _messenger.SendShowTextNotificationMessage("ThumbnailImageChanged".Translate());
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    [RelayCommand]
    async Task LaunchMovieFileAsync()
    {
        if (_vm.MovieFile == null) { return; }
        bool isPlaying = PlayerState == MediaPlaybackState.Playing;
        MediaPlayer.Pause();
        await Launcher.LaunchFileAsync(_vm.MovieFile);
    }

    [RelayCommand]
    async Task OpenMovieFileWithExplorerAsync()
    {
        if (_vm.MovieFile == null) { return; }
        MediaPlayer.Pause();
        await Launcher.LaunchFolderPathAsync(
            Path.GetDirectoryName(_vm.MovieFile.Path),
            new() { ItemsToSelect = { _vm.MovieFile } });
    }


    [RelayCommand]
    async Task SaveCurrentFrameAsync()
    {
        if (MediaPlayer == null) { return; }
        if (_vm.MovieFile is not { } movieFile) { return; }
        bool prevPlaying = false;
        var ct = this.GetCancellationTokenOnUnloaded();
        try
        {
            if (MediaPlayer.PlaybackSession.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Playing)
            {
                MediaPlayer.Pause();
                prevPlaying = true;
            }
            
            var picker = new FileSavePicker()
            {                
                FileTypeChoices = {
                    { ".jpg", [".jpg"] },
                    { ".png", [".png"] },
                },
                SuggestedFileName = $"tv_{Path.GetFileNameWithoutExtension(_vm.MovieFile?.Name)}_{MediaPlayer.PlaybackSession.Position.TotalMilliseconds:F0}",
                DefaultFileExtension = ".jpg",
            };

            if (await picker.PickSaveFileAsync() is not { } file) { return; }


            CanvasBitmapFileFormat outputFormat = file.FileType switch
            {
                ".jpg" => CanvasBitmapFileFormat.Jpeg,
                ".png" => CanvasBitmapFileFormat.Png,
                _ => CanvasBitmapFileFormat.Jpeg,
            };

            var videoPosition = MediaPlayer.PlaybackSession.Position;
            {
                using (var fileStream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite))
                {
                    if (!IsDurationAvairable)
                    {
                        var videoSize = new Size(MediaPlayer.PlaybackSession.NaturalVideoWidth, MediaPlayer.PlaybackSession.NaturalVideoHeight);
                        var renderSize = MyMediaPlayerElement.RenderSize;
                        double videoAspect = videoSize.Width / videoSize.Height;
                        double renderAspect = renderSize.Width / renderSize.Height;
                        Rect sourceRect;
                        double scaledWidth = renderSize.Height * videoAspect;
                        double scaledHeight = renderSize.Width / videoAspect;
                        sourceRect = videoAspect < renderAspect
                            ? new Rect(0, 0, scaledWidth, renderSize.Height)
                            : new Rect(0, 0, renderSize.Width, scaledHeight);
                        using CanvasRenderTarget crt = new(CanvasDevice.GetSharedDevice(), (float)sourceRect.Width, (float)sourceRect.Height, DisplayInformation.GetForCurrentView().LogicalDpi);
                        MediaPlayer.CopyFrameToVideoSurface(crt, sourceRect);
                        await crt.SaveAsync(fileStream, CanvasBitmapFileFormat.Jpeg);
                    }
                    else
                    {
                        if (NowPlayingWithFFmpegMediaSource)
                        {
                            await Task.Run(async () =>
                            {
                                using var movieStream = await movieFile.OpenReadAsync().AsTask(ct);
                                using var fg = await FrameGrabber.CreateFromStreamAsync(movieStream).AsTask(ct);
                                using var frame = await fg.ExtractVideoFrameAsync(videoPosition, true, 0).AsTask(ct);
                                if (outputFormat == CanvasBitmapFileFormat.Png)
                                {
                                    await frame.EncodeAsPngAsync(fileStream).AsTask(ct);
                                }
                                else
                                {
                                    await frame.EncodeAsJpegAsync(fileStream).AsTask(ct);
                                }
                            }, ct);
                        }
                        else
                        {
                            var clip = await MediaClip.CreateFromFileAsync(movieFile);
                            var mc = new MediaComposition()
                            {
                                Clips = { clip },
                            };
                            using var frame = await mc.GetThumbnailAsync(videoPosition, 0, 0, VideoFramePrecision.NearestFrame);
                            using var bitmap = await CanvasBitmap.LoadAsync(CanvasDevice.GetSharedDevice(), frame);
                            await bitmap.SaveAsync(fileStream, outputFormat);
                        }
                    }
                }
            }

            SavedVideoFrameFile = file;
            FrameSavedNotification.ShowDismissButton = true;
            FrameSavedNotification.Show();
        }
        catch (Exception ex)
        {
            throw;
        }
        finally
        {
            if (prevPlaying)
            {
                MediaPlayer.Play();
            }
        }
    }
    

    [ObservableProperty]
    StorageFile? _savedVideoFrameFile;


    [RelayCommand]
    async Task OpenSavedFrameImageFileAsync()
    {
        if (SavedVideoFrameFile == null) { return; }
        await Launcher.LaunchFileAsync(SavedVideoFrameFile, new LauncherOptions { DisplayApplicationPicker = true });
    }

    [RelayCommand]
    async Task OpenSavedFrameImageFileWithExplorerAsync()
    {
        if (SavedVideoFrameFile == null) { return; }
        await Launcher.LaunchFolderPathAsync(
            Path.GetDirectoryName(SavedVideoFrameFile.Path),
            new () { ItemsToSelect = { SavedVideoFrameFile } });
    }
}

public class SecondsToVideoTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double d)
        {
            var timeSpan = TimeSpan.FromSeconds(d);
            int hours = (int)timeSpan.TotalHours;
            int minutes = timeSpan.Minutes;
            int seconds = timeSpan.Seconds;

            if (hours > 0)
            {
                return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
            }
            else
            {
                return $"{minutes:D2}:{seconds:D2}";
            }
        }

        ThrowHelper.ThrowInvalidOperationException();
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}


public class ToPlaybackRateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double d)
        {
            return $"x{d:F1}";
        }

        ThrowHelper.ThrowInvalidOperationException();
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}