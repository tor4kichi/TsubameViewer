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
    public DataTemplate? GetContent()
    {
        return TitlebarContent;
    }

    public R3.Observable<string> ObserveTitleChanged()
    {
        return _vm.ObservePropertyChanged(x => x.CurrentFolderItem).Select(x => x?.Name ?? "");
    }

    internal readonly EBookViewerPageViewModel _vm;
    private readonly IMessenger _messenger;

    private readonly Core.AsyncLock _movePageLock = new();

    public EBookViewerPage()
    {
        this.InitializeComponent();
        
        DataContext = _vm = Ioc.Default.GetRequiredService<EBookViewerPageViewModel>();
        _messenger = Ioc.Default.GetRequiredService<IMessenger>();

        Loaded += MoveButtonEnablingWorkAround_EBookReaderPage_Loaded;

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

    private void MoveButtonEnablingWorkAround_EBookReaderPage_Loaded(object sender, RoutedEventArgs e)
    {
        //ControlHeight = EPubRendererContainer.ActualHeight - 64;
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


    AnimationBuilder _fadeOutAnim = AnimationBuilder.Create()
            .Opacity(0.0, duration: TimeSpan.FromMilliseconds(75));
    AnimationBuilder _fadeInAnim = AnimationBuilder.Create()
            .Opacity(1, delay: TimeSpan.FromMilliseconds(50),
            duration: TimeSpan.FromMilliseconds(125));

    private void WebView_ContentRefreshStarting(object sender, EventArgs e)
    {            
        NowEnablePageMove = false;
        _fadeOutAnim
            .Start(EPubRenderer);
    }

    private void WebView_ContentRefreshComplete(object sender, EventArgs e)
    {
        NowEnablePageMove = true;

        if (_vm.PageHtml != null)
        {
            _vm.CompletePageLoading();
        }
        _fadeInAnim.Start(EPubRenderer);
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

    private void MySwipeDistanceBehavior_Invoked(Behaviors.SwipeDistanceBehavior sender, Behaviors.SwipeDistanceInvokedEventArgs args)
    {
        if (args.X > 1)
        {
            LeftPageMoveButton.Command.Execute(null);
        }
        else if (args.X < -1)
        {
            RightPageMoveButton.Command.Execute(null);
        }

        if (args.Y > 1)
        {
            // 下スワイプ
            OpenTocPane();
        }
        else if (args.Y < -7.5)
        {
            // 上スワイプ
            _vm.BackNavigationCommand.Execute(null);
        }


        Debug.WriteLine(args.Y);
    }
}
