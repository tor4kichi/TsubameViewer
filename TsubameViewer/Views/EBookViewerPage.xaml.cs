using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Toolkit.Uwp.UI.Animations;
using R3;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using TsubameViewer.ViewModels;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.Views.EBookControls;
using VersOne.Epub;
using Windows.ApplicationModel.Core;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using Windows.Web.Http;

#nullable enable
namespace TsubameViewer.Views;

public sealed partial class EBookViewerPage : Page, ITitlebarContentAware
{
    public DataTemplate? GetHeader()
    {
        return null;
    }

    public DataTemplate? GetContent()
    {
        return TitlebarContent;
    }

    internal readonly EBookViewerPageViewModel _vm;
    private readonly IMessenger _messenger;

    private readonly Core.AsyncLock _movePageLock = new();

    public EBookViewerPage()
    {
        this.InitializeComponent();
        
        DataContext = _vm = Ioc.Default.GetRequiredService<EBookViewerPageViewModel>();
        _messenger = Ioc.Default.GetRequiredService<IMessenger>();

#if DEBUG
        DebugPanel.Visibility = Visibility.Visible;
#endif
        Loaded += MoveButtonEnablingWorkAround_EBookReaderPage_Loaded;
        Loaded += ResetAnimationUIContainer_Loaded1;

        EPubRenderer.ContentRefreshStarting += WebView_ContentRefreshStarting;
        EPubRenderer.ContentRefreshComplete += WebView_ContentRefreshComplete;

        EPubRenderer.Loaded += WebView_Loaded;
        EPubRenderer.Unloaded += WebView_Unloaded;

        EPubRenderer.WebResourceRequested += WebView_WebResourceRequested;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        AnimationBuilder.Create()
            .Translation(Axis.X, -320, duration: TimeSpan.FromMilliseconds(1))
            .Start(TocContentPanel);

        _messenger.Register<BackNavigationRequestingMessage>(this, (r, m) =>
        {
            if (TocContainer.Visibility == Visibility.Visible)
            {
                m.Value.IsHandled = true;
                CloseTocPaneCommand.Execute(null);
            }
        });

        ConnectedAnimation animation = ConnectedAnimationService.GetForCurrentView().GetAnimation(PageTransitionHelper.ImageJumpConnectedAnimationName);
        if (animation != null)
        {
            animation.Cancel();                
        }


        EPubRenderer.Visibility = Visibility.Visible;

        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        EPubRenderer.Visibility = Visibility.Collapsed;

        _messenger.Unregister<BackNavigationRequestingMessage>(this);


        base.OnNavigatingFrom(e);
    }
    Core.AsyncLock _resourceReadLock = new Core.AsyncLock();
    private async void WebView_WebResourceRequested(object sender, WebViewWebResourceRequestedEventArgs e)
    {
        var reqesutUri = e.Request.RequestUri;
        using (var defferral = e.GetDeferral())
        {
            try
            {                
                var stream = _vm.ResolveWebResourceRequest(reqesutUri);
                if (stream != null)
                {
                    e.Response = new Windows.Web.Http.HttpResponseMessage(statusCode: Windows.Web.Http.HttpStatusCode.Ok);
                    e.Response.Content = new HttpStreamContent(stream.AsInputStream());
                }                
            }
            finally
            {
                defferral.Complete();
            }
        }
    }


    //
    // Note: ePubRenderer内のページ遷移を含めたページ移動コマンドの左右入れ替え実装について
    // VisualStateManagerで切り替えたかったが、null参照エラーが出て動かないため
    // コードビハインドで切り替える形にした。
    // デバッグあり実行だと動くが、デバッグ無し実行だと動かなかった。（リリースビルドでも同様）
    //

    #region Bottom UI Menu


    private void ResetAnimationUIContainer_Loaded1(object sender, RoutedEventArgs e)
    {
        SwipeProcessScreen.Tapped -= SwipeProcessScreen_Tapped;
        SwipeProcessScreen.Tapped += SwipeProcessScreen_Tapped;
        SwipeProcessScreen.ManipulationMode = ManipulationModes.TranslateY | ManipulationModes.TranslateX;
        SwipeProcessScreen.ManipulationStarting -= SwipeProcessScreen_ManipulationStarting;
        SwipeProcessScreen.ManipulationStarted -= SwipeProcessScreen_ManipulationStarted;
        SwipeProcessScreen.ManipulationCompleted -= SwipeProcessScreen_ManipulationCompleted;
        SwipeProcessScreen.ManipulationStarting += SwipeProcessScreen_ManipulationStarting;
        SwipeProcessScreen.ManipulationStarted += SwipeProcessScreen_ManipulationStarted;
        SwipeProcessScreen.ManipulationCompleted += SwipeProcessScreen_ManipulationCompleted;
    }

    private void SwipeProcessScreen_Tapped(object sender, TappedRoutedEventArgs e)
    {
        var pt = e.GetPosition(RootGrid);

        if (_isOnceSkipTapped)
        {
            _isOnceSkipTapped = false;
            e.Handled = true;
            return;
        }

        var uiItems = VisualTreeHelper.FindElementsInHostCoordinates(pt, UIContainer);
        foreach (var item in uiItems)
        {
            if (item == RightPageMoveButton)
            {
                if (RightPageMoveButton.Command?.CanExecute(null) ?? false)
                {
                    RightPageMoveButton.Command.Execute(null);
                }
            }
            else if (item == LeftPageMoveButton)
            {
                if (LeftPageMoveButton.Command?.CanExecute(null) ?? false)
                {
                    LeftPageMoveButton.Command.Execute(null);
                }
            }
            else if (item is Button button)
            {
                var command = button.Command;
                var parameter = button.CommandParameter;
                if (command is not null)
                {
                    if (parameter is not null
                        && command.CanExecute(parameter)
                        )
                    {
                        command.Execute(parameter);
                    }
                }
            }
        }
    }

    private void SwipeProcessScreen_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
    {
        Debug.WriteLine(e.Cumulative.Translation.Y);
    }


    bool _isOnceSkipTapped = false;
    private void SwipeProcessScreen_ManipulationStarting(object sender, ManipulationStartingRoutedEventArgs e)
    {
        if (AnimationUIContainer.Opacity == 1.0)
        {
            e.Handled = true;
            _isOnceSkipTapped = true;
            return;
        }
    }


    private void SwipeProcessScreen_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
    {
        if (e.Cumulative.Translation.X > 1
            || e.Velocities.Linear.X > 0.01
            )
        {
            // 右スワイプ
            LeftPageMoveButton.Command.Execute(null);
        }
        else if (e.Cumulative.Translation.X < -1
            || e.Velocities.Linear.X < -0.01
            )
        {
            // 左スワイプ
            RightPageMoveButton.Command.Execute(null);
        }
        else
        {
            e.Handled = true;
        }
    }

    #endregion



    private void MoveButtonEnablingWorkAround_EBookReaderPage_Loaded(object sender, RoutedEventArgs e)
    {
        // Note: WebViewにフォーカスがあるとWebViewより前面にあるボタンが押せないバグのワークアラウンド
        this.LeftPageMoveButton.Focus(FocusState.Programmatic);
    }





    IDisposable? _rendererObserveDisposer;

    private void WebView_Loaded(object sender, RoutedEventArgs e)
    {
        _rendererObserveDisposer?.Dispose();
        _rendererObserveDisposer = null;
        var db = new DisposableBuilder();
        EPubRenderer.ObserveDependencyProperty(EPubRenderer.CurrentInnerPageProperty)
            .Subscribe(_ =>
            {
                _vm.InnerCurrentImageIndex = EPubRenderer.CurrentInnerPage;
            })
            .AddTo(ref db);

        EPubRenderer.ObserveDependencyProperty(EPubRenderer.TotalInnerPageCountProperty)
            .Subscribe(_ =>
            {
                _vm.InnerImageTotalCount = EPubRenderer.TotalInnerPageCount;
            })
            .AddTo(ref db);

        _rendererObserveDisposer = db.Build();
        NowEnablePageMove = false;
    }

    private void WebView_Unloaded(object sender, RoutedEventArgs e)
    {
        _rendererObserveDisposer?.Dispose();
        _rendererObserveDisposer = null;
    }




    public bool NowEnablePageMove
    {
        get { return (bool)GetValue(NowEnablePageMoveProperty); }
        set { SetValue(NowEnablePageMoveProperty, value); }
    }

    public static readonly DependencyProperty NowEnablePageMoveProperty =
        DependencyProperty.Register("NowEnablePageMove", typeof(bool), typeof(EBookViewerPage), new PropertyMetadata(true));

    private void WebView_ContentRefreshStarting(object sender, EventArgs e)
    {            
        NowEnablePageMove = false;
    }

    private void WebView_ContentRefreshComplete(object sender, EventArgs e)
    {
        NowEnablePageMove = true;

        if (_vm.PageHtml != null)
        {
            _vm.CompletePageLoading();
        }
    }


    [RelayCommand]
    async Task InnerGoPrevImage()
    {
        if (_vm.EBookReaderSettings.IsReversePageFliping_Button)
        {
            await ExecuteGoNextCommand();
        }
        else
        {
            await ExecuteGoPrevCommand();
        }
    }


    [RelayCommand]
    async Task InnerGoNextImage()
    {
        if (_vm.EBookReaderSettings.IsReversePageFliping_Button)
        {
            await ExecuteGoPrevCommand();
        }
        else
        {
            await ExecuteGoNextCommand();
        }

    }

    async Task ExecuteGoNextCommand()
    {
        if (NowEnablePageMove is false) { return; }

        using (await _movePageLock.LockAsync(default))
        {
            if (EPubRenderer.CanGoNext())
            {
                EPubRenderer.GoNext();
            }
            else
            {
                if (_vm.CanGoNext())
                {
                    EPubRenderer.PrepareGoNext();
                    await _vm.GoNextImageAsync();
                }
            }
        }
    }

    [RelayCommand]
    async Task ExecuteGoPrevCommand()
    {
        if (NowEnablePageMove is false) { return; }

        using (await _movePageLock.LockAsync(default))
        {
            if (EPubRenderer.CanGoPreview())
            {
                EPubRenderer.GoPreview();
            }
            else
            {
                if (_vm.CanGoPrev())
                {
                    EPubRenderer.PrepareGoPreview();
                    await _vm.GoPrevImageAsync();
                }
            }
        }
    }

    [RelayCommand]
    async Task ExecuteOpenTocPane()
    {
        if (NowEnablePageMove is false) { return; }

        TocContainer.Visibility = Visibility.Visible;
        await Task.Delay(250);
        if (TocItemsListView.SelectedItem != null)
        {
            var container = TocItemsListView.ContainerFromItem(TocItemsListView.SelectedItem);
            if (container is SelectorItem control)
            {
                control.Focus(FocusState.Keyboard);
            }
        }
    }

    [RelayCommand]
    void OpenTocPane()
    {
        TocContainer.Visibility = Visibility.Visible;
    }

    [RelayCommand]
    void CloseTocPane()
    {
        TocContainer.Visibility = Visibility.Collapsed;
    }

    private void CoverImage_Tapped(object sender, TappedRoutedEventArgs e)
    {
        _vm.CurrentImageIndex = 0;
    }

    public void RefreshPage()
    {
        EPubRenderer.Refresh();
    }

    private void CurrentBookReadingOrder_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (((FrameworkElement)sender).IsLoaded == false) { return; }

        if (e.AddedItems.ElementAtOrDefault(0) is EpubLocalTextContentFileRef pageRef)
        {
            _ = _vm.SetPageAsync(pageRef);
        }
    }
}
