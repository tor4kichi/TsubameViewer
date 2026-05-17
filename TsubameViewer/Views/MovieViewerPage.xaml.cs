using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using R3;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TsubameViewer.Views.Helpers;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Playback;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
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

    private readonly MovieViewerPageViewModel _vm;

    public MovieViewerPage()
    {
        this.InitializeComponent();

        DataContext = _vm = Ioc.Default.GetRequiredService<MovieViewerPageViewModel>();

        Loaded += MovieViewerPage_Loaded;
        Unloaded += MovieViewerPage_Unloaded;
    }

    private void MovieViewerPage_Loaded(object sender, RoutedEventArgs e)
    {
        MediaPlayer?.Dispose();       
        MediaPlayer = new MediaPlayer();
        MyMediaPlayerElement.SetMediaPlayer(MediaPlayer);

        _vm.ObservePropertyChanged(x => x.MovieSource)
            .Subscribe(x => MediaPlayer.Source = x)
            .RegisterTo(this.GetCancellationTokenOnUnloaded());

        MediaPlayer.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;
    }

    public Visibility IsPalyerPreparing(MediaPlaybackState state)
    {
        return (state is MediaPlaybackState.Opening or MediaPlaybackState.Buffering).TrueToVisible();
    }

    private void PlaybackSession_PlaybackStateChanged(MediaPlaybackSession sender, object args)
    {
        Observable.NextFrame()
            .Subscribe(_ => PlayerState = sender.PlaybackState);
    }

    private void MovieViewerPage_Unloaded(object sender, RoutedEventArgs e)
    {
        if (MediaPlayer == null) { return; }

        MediaPlayer.PlaybackSession.PlaybackStateChanged -= PlaybackSession_PlaybackStateChanged;

        MyMediaPlayerElement.SetMediaPlayer(null);
        MediaPlayer?.Dispose();
        MediaPlayer = null;        
    }

    [ObservableProperty]
    MediaPlayer? _mediaPlayer;



    [ObservableProperty]
    MediaPlaybackState _playerState;
}
