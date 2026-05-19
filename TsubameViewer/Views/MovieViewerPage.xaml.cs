using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
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
using TsubameViewer.ViewModels;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.Views.Helpers;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

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

    [ObservableProperty]
    MediaPlayer? _mediaPlayer;

    [ObservableProperty]
    bool _isDisplayControlUI;


    private void ControlUIInteractionWall_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var pt = e.GetCurrentPoint(null);
        if (pt.Properties.IsMiddleButtonPressed)
        {
            _vm.ToggleFullScreen();
        }
        if (pt.Properties.IsLeftButtonPressed)
        {
            if (_lastHideDisplayControlUIWithAutoHide) { return; }
            IsDisplayControlUI = !IsDisplayControlUI;
        }
    }


    public MovieViewerPage()
    {
        this.InitializeComponent();

        DataContext = _vm = Ioc.Default.GetRequiredService<MovieViewerPageViewModel>();
        _messenger = Ioc.Default.GetRequiredService<IMessenger>();
        Loaded += MovieViewerPage_Loaded;
        Unloaded += MovieViewerPage_Unloaded;

    }

    private void MovieViewerPage_Unloaded(object sender, RoutedEventArgs e)
    {
        ShowMouseCursor();

        if (MediaPlayer == null) { return; }

        Window.Current.CoreWindow.PointerPressed -= CoreWindow_VideoPositionSlider_PointerPressed;
        Window.Current.CoreWindow.PointerReleased -= CoreWindow_VideoPositionSlider_PointerReleased;

        MediaPlayer.PlaybackSession.PlaybackStateChanged -= PlaybackSession_PlaybackStateChanged;
        MediaPlayer.PlaybackSession.PositionChanged -= PlaybackSession_PositionChanged;
        MediaPlayer.PlaybackSession.NaturalDurationChanged -= PlaybackSession_NaturalDurationChanged;
        
        MediaPlayer.Source = null;
        _playbackResources?.Dispose();
        _playbackResources = null;
        MyMediaPlayerElement.SetMediaPlayer(null);
        MediaPlayer.Dispose();
        MediaPlayer = null;
    }


    IDisposable? _playbackResources;
    private void MovieViewerPage_Loaded(object sender, RoutedEventArgs e)
    {
        MediaPlayer = new MediaPlayer();
        MyMediaPlayerElement.SetMediaPlayer(MediaPlayer);

        MediaPlayer.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;
        MediaPlayer.PlaybackSession.PositionChanged += PlaybackSession_PositionChanged;
        MediaPlayer.PlaybackSession.NaturalDurationChanged += PlaybackSession_NaturalDurationChanged;                

        Window.Current.CoreWindow.PointerPressed += CoreWindow_VideoPositionSlider_PointerPressed;
        Window.Current.CoreWindow.PointerReleased += CoreWindow_VideoPositionSlider_PointerReleased;

        DisposableBuilder db = new();        
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
                    s.HideMouseCursor();
                    s.IsDisplayControlUI = false;
                }
            })
            .AddTo(ref db);

        InitializeZoomReaction(ref db);

        _lastHideDisplayControlUIWithAutoHide = false;
        var observeMouseMove = ObservableEventExtensions.FromTypedEvent<MouseDevice, MouseEventArgs>(
            h => MouseDevice.GetForCurrentView().MouseMoved += h,
            h => MouseDevice.GetForCurrentView().MouseMoved -= h
            );

        var observeWindowActivate = Observable.FromEvent<WindowActivatedEventHandler, WindowActivatedEventArgs>(
            conversion => (sender, args) => conversion(args),
            h => Window.Current.Activated += h,
            h => Window.Current.Activated -= h);

        var observePointerEntered = Observable.FromEvent<PointerEventHandler, PointerRoutedEventArgs>(
            conversion => (sender, args) => conversion(args),
            h => this.PointerEntered += h,
            h => this.PointerEntered -= h);
        var observePointerExited = Observable.FromEvent<PointerEventHandler, PointerRoutedEventArgs>(
            conversion => (sender, args) => conversion(args),
            h => this.PointerExited += h,
            h => this.PointerExited -= h);

        var insideWindowRp = Observable.Merge(observePointerEntered.Select(x => true), observePointerExited.Select(x => false))
            .ToReadOnlyReactiveProperty(true)
            .AddTo(ref db);

        observeWindowActivate.Subscribe(this, static (e, s) => s._isWindowActive = e.WindowActivationState != CoreWindowActivationState.Deactivated)
            .AddTo(ref db);
        observeMouseMove
            .Subscribe(this, static (x, s) =>
            {
                s.ShowMouseCursor();
                if (s._lastHideDisplayControlUIWithAutoHide)
                {
                    s._lastHideDisplayControlUIWithAutoHide = false;
                    s.IsDisplayControlUI = true;
                }
            })
            .AddTo(ref db);
        Observable.Merge(
            observeMouseMove.AsUnitObservable(),
            this.ObservePropertyChanged(x => x.IsDisplayControlUI).Where(x => x).AsUnitObservable(),
            this.ObservePropertyChanged(x => x.PlayerState).Where(x => x == MediaPlaybackState.Playing).AsUnitObservable()
            )            
            .Debounce(TimeSpan.FromSeconds(1.25))            
            .Where((this, insideWindowRp), static (_, s) => 
            {
                if (!s.insideWindowRp.CurrentValue) { return false; }
                if (!s.Item1._isWindowActive) { return false; }
                if (s.Item1.PlayerState != MediaPlaybackState.Playing) { return false; }
                //if (IsDisplayControlUI) { return false; }

                return true;
            })
            .Subscribe(this, static (x, s) =>
            {
                s.HideMouseCursor();
                s._lastHideDisplayControlUIWithAutoHide = s.IsDisplayControlUI;
                s.IsDisplayControlUI = false;
            })
            .AddTo(ref db);

        db.Build().RegisterTo(this.GetCancellationTokenOnUnloaded());
    }

    #region Display Style



    bool _isWindowActive = true;
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

    partial void OnPlayerStateChanged(MediaPlaybackState value)
    {
        if (value == MediaPlaybackState.Paused
            && _nowRequestPlayStart)
        {
            _nowRequestPlayStart = false;
            MediaPlayer?.Play();
        }

        Debug.WriteLine(value);
    }

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

    [ObservableProperty]
    bool _isLoopingEnabled;

    partial void OnIsLoopingEnabledChanged(bool value)
    {
        MediaPlayer?.IsLoopingEnabled = value;
    }


    #endregion


    #region Position and Duration
    
    private void PlaybackSession_NaturalDurationChanged(MediaPlaybackSession sender, object args)
    {
        Observable.NextFrame()
            .Subscribe((this, sender), (_, s) => s.Item1.VideoDuration = s.sender.NaturalDuration);
    }

    private void PlaybackSession_PositionChanged(MediaPlaybackSession sender, object args)
    {
        Observable.NextFrame()
            .Subscribe(this, (_, s) => s.SetVideoPositionFromCode(s.MediaPlayer?.PlaybackSession.Position ?? TimeSpan.Zero));
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
        if (_videoPositionChangingFromCode) 
        {
            return; 
        }

        _videoPosition = TimeSpan.FromSeconds((double)e.NewValue);
        MediaPlayer?.PlaybackSession.Position = _videoPosition;
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
            PlaybackRate = Math.Clamp(playbackRate, MinPlaybackRate, MaxPlaybackRate);
        }
        finally
        {
            _nowPlaybackRateChangingFromCode = false;
        }
    }

    [ObservableProperty]
    double _playbackRate = 1;

    private void PlaybackRateSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_nowPlaybackRateChangingFromCode) { return; }

        SetPlaybackRateFromCode((double)e.NewValue);
        MediaPlayer?.PlaybackSession.PlaybackRate = _playbackRate;
    }


    [RelayCommand]
    void SetPlaybackRate(double d)
    {
        SetPlaybackRateFromCode(d);
        MediaPlayer?.PlaybackSession.PlaybackRate = _playbackRate;
    }

    #endregion


    #region Sound Volume

    [ObservableProperty]    
    double _soundVolume = 0.5;

    partial void OnSoundVolumeChanged(double value)
    {
        MediaPlayer?.Volume = SoundVolume;
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
                SoundVolume_Display = SoundVolume;
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
        if (_nowSoundVolumeChanging) { return; }

        SetSoundVolumeFromCode((double)e.NewValue);        
    }

    void SetSoundVolumeFromCode(double v)
    {
        _nowSoundVolumeChanging = true;
        try
        {
            SoundVolume = Math.Clamp(v, 0.0, 1.0);
            IsMute = SoundVolume == 0;
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
}

public class SecondsToVideoTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double d)
        {
            return TimeSpan.FromSeconds(d).ToString();
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