using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using I18NPortable;
using Microsoft.Graphics.Canvas;
using Microsoft.Toolkit.Uwp.UI.Animations;
using PDFtoImage;
using R3;
using R3.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using TsubameViewer.Contracts.Notification;
using TsubameViewer.ViewModels;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.Views.Helpers;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
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

[ObservableObject]
public sealed partial class MovieViewerPage : Page, ITitlebarContentAware
{
    public DataTemplate? GetContent()
    {
        return TitlebarContent;
    }


    internal readonly MovieViewerPageViewModel _vm;
    private readonly IMessenger _messenger;

    public MediaPlayer MediaPlayer => MyMediaPlayerElement.MediaPlayer;

    [ObservableProperty]
    bool _isDisplayControlUI;

    private void ControlUIInteractionWall_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var pt = e.GetCurrentPoint(null);
        if (pt.Properties.IsMiddleButtonPressed)
        {
            _ = ToggleFullScreen();
        }
        if (pt.Properties.IsLeftButtonPressed)
        {
            IsDisplayControlUI = true;
        }
    }

    private void ControlUIInteractionWall_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _lastHideDisplayControlUIWithAutoHide = false; ;
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

    private void MovieViewerPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _mouseCursorAutoHideTimer.Stop();
        ShowMouseCursor();

        Window.Current.CoreWindow.PointerPressed -= CoreWindow_VideoPositionSlider_PointerPressed;
        Window.Current.CoreWindow.PointerReleased -= CoreWindow_VideoPositionSlider_PointerReleased;
        
        MediaPlayer.PlaybackSession.PlaybackStateChanged -= PlaybackSession_PlaybackStateChanged;
        MediaPlayer.PlaybackSession.NaturalDurationChanged -= PlaybackSession_NaturalDurationChanged;
        
        MediaPlayer.Source = null;
        _playbackResources?.Dispose();
        _playbackResources = null;
    }

    IDisposable? _playbackResources;
    private void MovieViewerPage_Loaded(object sender, RoutedEventArgs e)
    {
        //MediaPlayer = new MediaPlayer();
        //MyMediaPlayerElement.SetMediaPlayer(MediaPlayer);
        
        MediaPlayer.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;
        MediaPlayer.PlaybackSession.NaturalDurationChanged += PlaybackSession_NaturalDurationChanged;                

        Window.Current.CoreWindow.PointerPressed += CoreWindow_VideoPositionSlider_PointerPressed;
        Window.Current.CoreWindow.PointerReleased += CoreWindow_VideoPositionSlider_PointerReleased;

        DisposableBuilder db = new();

        _lastHideDisplayControlUIWithAutoHide = false;

        var mediaPlayer = MyMediaPlayerElement.MediaPlayer;
        var playerPositionChanged = ObservableEventExtensions.FromTypedEvent<MediaPlaybackSession, object>(
            h => mediaPlayer.PlaybackSession.PositionChanged += h,
            h => mediaPlayer.PlaybackSession.PositionChanged -= h
            );

        var insideWindowRp = Observable.Merge(
                this.ObservePointerEntered().Select(x => true), 
                this.ObservePointerExited().Select(x => false))
            .Do(x => Debug.WriteLine($"inside window: {x}"))
            .ToReadOnlyReactiveProperty(false)
            .AddTo(ref db);

        var insideControlUIRp = Observable.Merge(
                ImageSelectorContainer.ObservePointerEntered().Select(x => true),
                ImageSelectorContainer.ObservePointerExited().Select(x => false))
            .Do(x => Debug.WriteLine($"inside ControlUI: {x}"))
            .ToReadOnlyReactiveProperty(false)
            .AddTo(ref db);

        Window.Current.ObserveActivated()
            .Subscribe(this, static (e, s) => s._isWindowActive = e.WindowActivationState != CoreWindowActivationState.Deactivated)
            .AddTo(ref db);

        _mouseCursorAutoHideTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _mouseCursorAutoHideTimer.Tick += MouseCursorMonitorTimer_Tick;
        _mouseCursorAutoHideTimer.Interval = TimeSpan.FromSeconds(1.75);
        _mouseCursorAutoHideTimer.IsRepeating = false;
        void MouseCursorMonitorTimer_Tick(DispatcherQueueTimer sender, object args)
        {
            if (insideWindowRp.CurrentValue
                && PlayerState == MediaPlaybackState.Playing
                && !insideControlUIRp.CurrentValue)
            {
                HideMouseCursor();                
            }

            if (PlayerState == MediaPlaybackState.Playing
                && !insideControlUIRp.CurrentValue)
            {
                IsDisplayControlUI = false;
            }
        }

        _vm.ObservePropertyChanged(x => x.MovieFile)
            .SubscribeAwait(this, static async (x, s, ct) =>
            {
                s._playbackResources?.Dispose();

                s.MediaPlayer?.Source = null;

                if (x == null) { return; }
                if (s.MediaPlayer == null) { return; }

                CompositeDisposable db = new();
                try
                {
                    s._nowRequestPlayStart = true;
                    var mediaSource = MediaSource.CreateFromStorageFile(x);                    
                    db.Add(mediaSource);
                    s.MediaPlayer?.Source = mediaSource;
                    ct.ThrowIfCancellationRequested();
                }
                catch
                {
                    s._nowRequestPlayStart = false;
                    db.Dispose();
                    throw;
                }
                s._playbackResources = db;

                if (x != null)
                {
                    s._mouseCursorAutoHideTimer.Start();                    
                }
            })
            .AddTo(ref db);

        playerPositionChanged
            .ObserveOnCurrentSynchronizationContext()
            .Debounce(TimeSpan.FromSeconds(0.1))
            .Subscribe(this, (e, s) => 
            {
                if (_videoPositionChangingFromCode)
                {
                    _videoPositionChangingFromCode = false;
                    return;
                }
                s.SetVideoPositionFromCode(e.Sender?.Position ?? default);                
            });

        var bookmarkRp = _vm.ObservePropertyChanged(x => x.MovieFile)
            .Select(_vm, (x, vm) => x != null ? vm.BookmarkManager.GetBookmarkFacade(x.Path) : null)
            .ToReadOnlyReactiveProperty()
            .AddTo(ref db);

        InitializeZoomReaction(ref db);

        Observable.Merge(
            MouseDevice.GetForCurrentView().ObserveMouseMoved().AsUnitObservable(),
            insideWindowRp.Where(x => x).AsUnitObservable(),
            Window.Current.ObserveActivated().AsUnitObservable()
            )
            .Subscribe((this, insideWindowRp), static (x, s) =>
            {
                var _this = s.Item1;
                _this.ShowMouseCursor();
                if (s.insideWindowRp.CurrentValue)
                {
                    _this.IsDisplayControlUI = true;

                    _this._mouseCursorAutoHideTimer.Stop();
                    _this._mouseCursorAutoHideTimer.Start();
                }
            })
            .AddTo(ref db);        

        this.ObservePropertyChanged(x => x.PlayerState)
            .Where(x => x == MediaPlaybackState.Paused)
            .Take(1)
            .SubscribeAwait((this, bookmarkRp), static async (x, s, ct) =>
            {
                var _this = s.Item1;
                if (s.bookmarkRp.CurrentValue is not { } bkmk) { return; }
                _this.VideoPosition = _this.VideoDuration * bkmk.ReadPosition.Value;
                if (_this._nowRequestPlayStart)
                {
                    _this._nowRequestPlayStart = false;
                    await Observable.TimerFrame(20).WaitAsync();
                    _this.MediaPlayer?.Play();
                    await Observable.NextFrame().WaitAsync();
                }
                // Note: 再生後に速度変更する。そうしないと動き出し数フレームが２回再生される症状がでるため。
                _this.MediaPlayer?.PlaybackSession.PlaybackRate = _this._vm.PageSettings.PlaybackRate;
            });

        this.ObservePropertyChanged(x => x.VideoPosition)
            .Debounce(TimeSpan.FromSeconds(1))
            .Where((this, bookmarkRp), (x, s) => !s.Item1._nowRequestPlayStart && s.Item1.VideoDuration > TimeSpan.FromSeconds(5) && s.Item1._vm.MovieFile != null && s.bookmarkRp.CurrentValue != null)
            .Subscribe((this, bookmarkRp), (x, s) =>   s.bookmarkRp.CurrentValue?.ReadPosition = new ((float)(s.Item1.VideoPosition.TotalSeconds / s.Item1.VideoDuration.TotalSeconds)))
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

    DispatcherQueueTimer _mouseCursorAutoHideTimer;

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
            .Subscribe(this, (x, s) => s.MediaPlayer?.IsLoopingEnabled = x)
            .AddTo(ref db);
    }


    #endregion


    #region Position and Duration
    
    private void PlaybackSession_NaturalDurationChanged(MediaPlaybackSession sender, object args)
    {
        Observable.NextFrame()
            .Subscribe((this, sender), (_, s) => s.Item1.VideoDuration = s.sender.NaturalDuration);
    }


    [ObservableProperty]
    TimeSpan _videoDuration;

    [ObservableProperty]
    TimeSpan _videoPosition;

    string ToHHMMSSString(TimeSpan t)
    {
        return t.ToString("hh\\:mm\\:ss");
    }

    double ToTotalSeconds(TimeSpan t) => t.TotalSeconds;

    bool _videoPositionChangingFromCode;
    void SetVideoPositionFromCode(TimeSpan ts)
    {
        if (_videoPositionChangingFromCode) { return; }
        _videoPositionChangingFromCode = true;
        try
        {
            VideoPosition = ts;
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
        VideoPosition = ts;
        MediaPlayer?.PlaybackSession.Position = ts;
    }

    bool _prevPlaying;
    private void CoreWindow_VideoPositionSlider_PointerPressed(CoreWindow sender, PointerEventArgs args)
    {
        if (args.IsContactUIElement(PageSelector, Window.Current.Content))
        {
            Debug.WriteLine("IsContactUIElement(PlaybackRateSlider)");
            _prevPlaying = PlayerState is MediaPlaybackState.Playing;
            MediaPlayer?.Pause();
        }
    }

    private void CoreWindow_VideoPositionSlider_PointerReleased(CoreWindow sender, PointerEventArgs args)
    {
        if (_prevPlaying)
        {
            _prevPlaying = false;
            MediaPlayer?.Play();
        }
    }

    #endregion

    #region Playback Rate



    void HandlePlaybackRateChanged(ref DisposableBuilder db)
    {
    }

    string ToPlaybackRateString(double rate)
    {
        return $"x{rate:F1}";
    }

    readonly double MinPlaybackRate = 0.1;
    readonly double MaxPlaybackRate = 4;

    bool _nowPlaybackRateChangingFromCode;
    void SetPlaybackRateFromCode(double playbackRate)
    {
        _nowPlaybackRateChangingFromCode = true;
        try
        {
            bool prevPlaying = PlayerState == MediaPlaybackState.Playing;
            MediaPlayer?.Pause();
            _vm.PageSettings.PlaybackRate = Math.Clamp(playbackRate, MinPlaybackRate, MaxPlaybackRate);
            if (prevPlaying)
            {
                Observable.NextFrame()
                    .Subscribe(MediaPlayer, (_, s) => s?.Play());                
            }
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
        MediaPlayer?.PlaybackSession.PlaybackRate = _vm.PageSettings.PlaybackRate;
    }


    [RelayCommand]
    void SetPlaybackRate(double d)
    {
        SetPlaybackRateFromCode(d);
        MediaPlayer?.PlaybackSession.PlaybackRate = _vm.PageSettings.PlaybackRate;
    }

    #endregion


    #region Sound Volume

    void HandleSoundVolumeChanged(ref DisposableBuilder db)
    {
        _vm.PageSettings.ObservePropertyChanged(x => x.SoundVolume)
            .Subscribe((this), (x, s) => s.MediaPlayer?.Volume = x)
            .AddTo(ref db);

        SoundVolume_Display = _vm.PageSettings.SoundVolume;

        ControlUI_SoundVolumeSlider.ValueChanged -= ControlUI_SoundVolumeSlider_ValueChanged;
        ControlUI_SoundVolumeSlider.ValueChanged += ControlUI_SoundVolumeSlider_ValueChanged;
        Disposable.Create(this, s => s.ControlUI_SoundVolumeSlider.ValueChanged += s.ControlUI_SoundVolumeSlider_ValueChanged)
            .AddTo(ref db);
    }

   
    [ObservableProperty]
    double _soundVolume_Display = 0.5;

    [ObservableProperty]
    bool _isMute;

    [RelayCommand]
    void ToggleIsMuted()
    {
        IsMute = !IsMute;
    }

    partial void OnIsMuteChanged(bool value)
    {
        MediaPlayer?.IsMuted = value;

        _nowSoundVolumeChanging = true;
        try
        {
            if (value)
            {
                SoundVolume_Display = 0;
            }
            else
            {
                SoundVolume_Display = _vm.PageSettings.SoundVolume;
            }
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

        SetSoundVolumeFromCode((double)e.NewValue);        
    }

    void SetSoundVolumeFromCode(double v)
    {
        _nowSoundVolumeChanging = true;
        try
        {
            _vm.PageSettings.SoundVolume = Math.Clamp(v, 0.0, 1.0);
            IsMute = _vm.PageSettings.SoundVolume == 0;
            //SoundVolume_Display = SoundVolume;
        }
        finally
        {
            _nowSoundVolumeChanging = false;
        }
    }    

    #endregion




    private void Page2MenuFlyout_Opening(object sender, object e)
    {

    }

    private void ForceClosePage(object sender, RoutedEventArgs e)
    {        
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
        MediaPlayer?.Pause();
        await Observable.NextFrame().WaitAsync();
        var appView = ApplicationView.GetForCurrentView();
        if (appView.IsFullScreenMode)
        {
            appView.ExitFullScreenMode();
        }
        else
        {
            appView.TryEnterFullScreenMode();
        }

        await Observable.TimerFrame(10).WaitAsync();
        if (isPlaying)
        {
            MediaPlayer?.Play();
        }
    }

    async Task SetPlayerStretch_Internal(Stretch stretch)
    {
        if (MediaPlayer == null) { return; }
        if (MyMediaPlayerElement.Stretch == stretch) { return; }

        bool isPlaying = PlayerState == MediaPlaybackState.Playing;
        MediaPlayer.Pause();
        await Observable.NextFrame().WaitAsync();
        MyMediaPlayerElement.Stretch = stretch;
        await Observable.NextFrame().WaitAsync();
        if (isPlaying)
        {
            MediaPlayer.Play();
        }
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

        bool isPlaying = PlayerState == MediaPlaybackState.Playing;
        MediaPlayer.Pause();
        await Observable.NextFrame().WaitAsync();
        MediaPlayer.PlaybackSession.PlaybackRotation = rotate;
        await Observable.NextFrame().WaitAsync();
        if (isPlaying)
        {
            MediaPlayer.Play();
        }
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
    async Task SetThumbnailImageAsync()
    {
        if (MediaPlayer == null) { return; }

        try
        {
            bool prevPlaying = false;
            if (MediaPlayer.PlaybackSession.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Playing)
            {
                MediaPlayer.Pause();
                prevPlaying = true;
            }

            await Observable.NextFrame().WaitAsync();

            using CanvasRenderTarget crt = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), (float)MyMediaPlayerElement.ActualWidth, (float)MyMediaPlayerElement.ActualHeight, DisplayInformation.GetForCurrentView().LogicalDpi);
            MediaPlayer.CopyFrameToVideoSurface(crt);

            using (var stream = _vm.RecyclableMemoryStreamManager.GetStream())
            {
                await crt.SaveAsync(stream.AsRandomAccessStream(), CanvasBitmapFileFormat.Jpeg);
                stream.Seek(0, SeekOrigin.Begin);
                await _vm.ThumbnailManager.SetThumbnailAsync(_vm.MovieFile, stream, true, this.GetCancellationTokenOnNavigatingFrom());
            }

            _messenger.SendShowTextNotificationMessage("ThumbnailImageChanged".Translate());

            if (prevPlaying)
            {
                MediaPlayer.Play();
            }
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
        MediaPlayer?.Pause();
        await Launcher.LaunchFileAsync(_vm.MovieFile);
    }

    [RelayCommand]
    async Task OpenMovieFileWithExplorerAsync()
    {
        if (_vm.MovieFile == null) { return; }
        MediaPlayer?.Pause();
        await Launcher.LaunchFolderPathAsync(
            Path.GetDirectoryName(_vm.MovieFile.Path),
            new() { ItemsToSelect = { _vm.MovieFile } });
    }


    [RelayCommand]
    async Task SaveCurrentFrameAsync()
    {
        if (MediaPlayer == null) { return; }

        bool prevPlaying = false;
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

            using CanvasRenderTarget crt = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), (float)MyMediaPlayerElement.ActualWidth, (float)MyMediaPlayerElement.ActualHeight, DisplayInformation.GetForCurrentView().LogicalDpi);
            MediaPlayer.CopyFrameToVideoSurface(crt);

            using (var fileStream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite))
            {
                await crt.SaveAsync(fileStream, CanvasBitmapFileFormat.Jpeg);                
            }

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
        await Launcher.LaunchFileAsync(SavedVideoFrameFile);
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