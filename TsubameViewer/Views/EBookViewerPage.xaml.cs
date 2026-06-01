using CommunityToolkit.Diagnostics;
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
using Windows.UI.Xaml.Data;
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

        EPubRenderer_1.ContentRefreshStarting += WebView_ContentRefreshStarting_1;
        EPubRenderer_1.ContentRefreshComplete += WebView_ContentRefreshComplete_1;
        EPubRenderer_1.Loaded += WebView_Loaded;
        EPubRenderer_1.WebResourceRequested += WebView_WebResourceRequested;

        EPubRenderer_2.ContentRefreshStarting += WebView_ContentRefreshStarting_2;
        EPubRenderer_2.ContentRefreshComplete += WebView_ContentRefreshComplete_2;
        EPubRenderer_2.Loaded += WebView_Loaded;
        EPubRenderer_2.WebResourceRequested += WebView_WebResourceRequested;
    }

    bool _isFirstDisplay = true;
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

        EPubRenderer_1.Opacity = 0;
        EPubRenderer_2.Opacity = 0;
        _vm.ObservePropertyChanged(x => x.NowDisplayRendererIndex)
            .Subscribe(this, static (x, s) =>
            {
                var _this = s;
                if (x == 0)
                {
                    Debug.WriteLine("Display EPubRenderer_1");
                    _this.EPubRenderer_1.Opacity = 1;
                    _this.EPubRenderer_2.Opacity = 0;
                }
                else
                {
                    Debug.WriteLine("Display EPubRenderer_2");
                    _this.EPubRenderer_1.Opacity = 0;
                    _this.EPubRenderer_2.Opacity = 1;
                }
            })
            .RegisterTo(this.GetCancellationTokenOnNavigatingFrom());

        EPubRenderer_1.Visibility = Visibility.Visible;
        EPubRenderer_2.Visibility = Visibility.Visible;

        base.OnNavigatedTo(e);
    }

    protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
    {
        EPubRenderer_1.Visibility = Visibility.Collapsed;
        EPubRenderer_2.Visibility = Visibility.Collapsed;

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

    private void WebView_Loaded(object sender, RoutedEventArgs e)
    {
        var elem = (EPubRenderer)sender;
        var db = new DisposableBuilder();
        elem.ObserveDependencyProperty(EPubRenderer.CurrentInnerPageProperty)
            .ToObservable()
            .Subscribe((this, elem), static (_, s) =>
            {
                var (_this, elem) = s;
                var _vm = _this._vm;
                if (elem == _this.EPubRenderer_1)
                {
                    _vm.SwapPages[0].InnerCurrentPageIndex = elem.CurrentInnerPage;
                    if (_vm.NowDisplayRendererIndex == 0)
                    {
                        _vm.InnerCurrentImageIndex = elem.CurrentInnerPage;
                    }
                }
                else if (elem == _this.EPubRenderer_2)
                {
                    _vm.SwapPages[1].InnerCurrentPageIndex = elem.CurrentInnerPage;
                    if (_vm.NowDisplayRendererIndex == 1)
                    {
                        _vm.InnerCurrentImageIndex = elem.CurrentInnerPage;
                    }
                }
            })
            .AddTo(ref db);

        elem.ObserveDependencyProperty(EPubRenderer.TotalInnerPageCountProperty)
            .ToObservable()
            .Subscribe((this, elem), static (_, s) =>
            {
                var (_this, elem) = s;
                var _vm = _this._vm;
                if (elem == _this.EPubRenderer_1)
                {
                    _vm.SwapPages[0].InnerTotalPageCount = elem.TotalInnerPageCount;
                    _vm.InnerImageTotalCount = elem.TotalInnerPageCount;
                }
                else if (elem == _this.EPubRenderer_2)
                {
                    _vm.SwapPages[1].InnerTotalPageCount = elem.TotalInnerPageCount;
                    _vm.InnerImageTotalCount = elem.TotalInnerPageCount;
                }
            })
            .AddTo(ref db);

        db.Build().RegisterTo(elem.GetCancellationTokenOnUnloaded());
        NowEnablePageMove_1 = false;
        NowEnablePageMove_2 = false;
    }



    public bool NowEnablePageMove_1
    {
        get { return (bool)GetValue(NowEnablePageMove_1Property); }
        set { SetValue(NowEnablePageMove_1Property, value); }
    }

    public static readonly DependencyProperty NowEnablePageMove_1Property =
        DependencyProperty.Register("NowEnablePageMove_1", typeof(bool), typeof(EBookViewerPage), new PropertyMetadata(false));



    public bool NowEnablePageMove_2
    {
        get { return (bool)GetValue(NowEnablePageMove_2Property); }
        set { SetValue(NowEnablePageMove_2Property, value); }
    }

    public static readonly DependencyProperty NowEnablePageMove_2Property =
        DependencyProperty.Register(nameof(NowEnablePageMove_2), typeof(bool), typeof(EBookViewerPage), new PropertyMetadata(false));




    AnimationBuilder _fadeOutAnim = AnimationBuilder.Create()
            .Opacity(0.0, duration: TimeSpan.FromMilliseconds(75));
    AnimationBuilder _fadeInAnim = AnimationBuilder.Create()
            .Opacity(1, delay: TimeSpan.FromMilliseconds(50),
            duration: TimeSpan.FromMilliseconds(125));

    private void WebView_ContentRefreshStarting_1(object sender, EventArgs e)
    {            
        NowEnablePageMove_1 = false;
    }

    private void WebView_ContentRefreshComplete_1(object sender, EventArgs e)
    {
        NowEnablePageMove_1 = true;
        if (_vm.SwapPages[0].PageHtml != null)
        {
            _vm.CompletePageLoading_1();
        }
    }

    private void WebView_ContentRefreshStarting_2(object sender, EventArgs e)
    {
        NowEnablePageMove_2= false;
    }

    private void WebView_ContentRefreshComplete_2(object sender, EventArgs e)
    {
        NowEnablePageMove_2 = true;
        if (_vm.SwapPages[1].PageHtml != null)
        {
            _vm.CompletePageLoading_2();
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
        using (await _movePageLock.LockAsync(default))
        {
            var currentEPubRenderer = _vm.NowDisplayRendererIndex == 0
                ? EPubRenderer_1
                : EPubRenderer_2;
            var altEPubRenderer = _vm.NowDisplayRendererIndex == 0
                ? EPubRenderer_2
                : EPubRenderer_1;

            if (_vm.NowDisplayRendererIndex == 0
                && NowEnablePageMove_1 is false)
            {
                return;
            }
            else if (_vm.NowDisplayRendererIndex == 1
                && NowEnablePageMove_2 is false)
            {
                return;
            }

            if (currentEPubRenderer.CanGoNext())
            {
                currentEPubRenderer.GoNext();
            }
            else
            {
                if (_vm.CanGoNext())
                {
                    altEPubRenderer.PrepareGoNext();
                    currentEPubRenderer.PrepareGoNext();
                    await _vm.GoNextImageAsync();
                }
            }
        }
    }

    [RelayCommand]
    async Task ExecuteGoPrevCommand()
    {
        using (await _movePageLock.LockAsync(default))
        {
            var currentEPubRenderer = _vm.NowDisplayRendererIndex == 0
                ? EPubRenderer_1
                : EPubRenderer_2;
            var altEPubRenderer = _vm.NowDisplayRendererIndex == 0
                ? EPubRenderer_2
                : EPubRenderer_1;

            if (_vm.NowDisplayRendererIndex == 0
                && NowEnablePageMove_1 is false)
            {
                return;
            }
            else if (_vm.NowDisplayRendererIndex == 1
                && NowEnablePageMove_2 is false)
            {
                return;
            }

            if (currentEPubRenderer.CanGoPreview())
            {
                currentEPubRenderer.GoPreview();
            }
            else
            {
                if (_vm.CanGoPrev())
                {
                    altEPubRenderer.PrepareGoPreview();
                    currentEPubRenderer.PrepareGoPreview();
                    await _vm.GoPrevImageAsync();
                }
            }
        }
    }

    [RelayCommand]
    async Task ExecuteOpenTocPane()
    {
        if (NowEnablePageMove_1 is false) { return; }

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
        var currentEPubRenderer = _vm.NowDisplayRendererIndex == 0
                ? EPubRenderer_1
                : EPubRenderer_2;
        currentEPubRenderer.Refresh();
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

public sealed class FilePathToFileNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return Path.GetFileNameWithoutExtension((string)value);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}