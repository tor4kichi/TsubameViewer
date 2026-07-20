using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI.Animations;
using I18NPortable;
using R3;
using R3.Extensions;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Contracts.Notification;
using TsubameViewer.Core.Models.EBook;
using TsubameViewer.ViewModels;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.Views.EBookControls;
using VersOne.Epub;
using Windows.ApplicationModel.Core;
using Windows.UI;
using Windows.UI.Core;
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
    readonly IMessenger _messenger;

    readonly Core.AsyncLock _movePageLock = new();

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

    CancellationToken _navigationCt;
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        AnimationBuilder.Create()
            .Translation(Axis.X, -320, duration: TimeSpan.FromMilliseconds(1))
            .Start(TocContentPanel);

        _navigationCt = this.GetCancellationTokenOnNavigatingFrom();
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

        EPubRenderer_1_Translate.X = 50000;
        EPubRenderer_2_Translate.X = 50000;

        DisposableBuilder db = new();
        _vm.ObservePropertyChanged(x => x.NowDisplayRendererIndex)
            .Subscribe(this, static (x, s) =>
            {
                var _this = s;
                if (x == 0)
                {
                    Debug.WriteLine("Display EPubRenderer_1");
                    s.EPubRenderer_1_Translate.X = 0;
                    s.EPubRenderer_2_Translate.X = 50000;
                }
                else
                {
                    Debug.WriteLine("Display EPubRenderer_2");
                    s.EPubRenderer_1_Translate.X = 50000;
                    s.EPubRenderer_2_Translate.X = 0;
                }
            })
            .AddTo(ref db);
            ;

        EPubRenderer_1.Visibility = Visibility.Visible;
        EPubRenderer_2.Visibility = Visibility.Visible;

        R3.Observable.Merge(
            Window.Current.ObserveSizeChanged().AsUnitObservable(),
            this.ObserveSizeChanged().AsUnitObservable().Skip(1)
            )         
            .ThrottleFirstFrame(1)
            .Subscribe(_ => 
            {
                if (_vm.NowDisplayRendererIndex == 0)
                {
                    EPubRenderer_2.Visibility = Visibility.Collapsed;
                    _fadeOutAnim.Start(EPubRenderer_1);                    
                }
                else
                {
                    EPubRenderer_1.Visibility = Visibility.Collapsed;
                    _fadeOutAnim.Start(EPubRenderer_2);
                }
            })
            .AddTo(ref db);

        db.Build().RegisterTo(_navigationCt);
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
    void WebView_WebResourceRequested(object sender, WebViewWebResourceRequestedEventArgs e)
    {
        var reqesutUri = e.Request.RequestUri;
        using (var defferral = e.GetDeferral())
        {
            var stream = _vm.ResolveWebResourceRequest(reqesutUri);
            if (stream != null)
            {
                e.Response = new Windows.Web.Http.HttpResponseMessage(statusCode: Windows.Web.Http.HttpStatusCode.Ok);
                e.Response.Content = new HttpStreamContent(stream.AsInputStream());
            }
        }
    }


    //
    // Note: ePubRenderer内のページ遷移を含めたページ移動コマンドの左右入れ替え実装について
    // VisualStateManagerで切り替えたかったが、null参照エラーが出て動かないため
    // コードビハインドで切り替える形にした。
    // デバッグあり実行だと動くが、デバッグ無し実行だと動かなかった。（リリースビルドでも同様）
    //

    void MoveButtonEnablingWorkAround_EBookReaderPage_Loaded(object sender, RoutedEventArgs e)
    {
        //ControlHeight = EPubRendererContainer.ActualHeight - 64;
        // Note: WebViewにフォーカスがあるとWebViewより前面にあるボタンが押せないバグのワークアラウンド
        this.LeftPageMoveButton.Focus(FocusState.Programmatic);
    }

    void WebView_Loaded(object sender, RoutedEventArgs e)
    {
        var elem = (EPubRenderer)sender;
        var db = new DisposableBuilder();
        elem.ObserveDependencyProperty(EPubRenderer.CurrentInnerPageProperty)
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

        _vm.SwapPages[0].ObservePropertyChanged(x => x.IsLoaded)
            .Where(x => x is false)
            .Subscribe(this, (_, s) => 
            {
                s._fadeOutAnim.Start(s.EPubRenderer_1);
            })
            .AddTo(ref db);
        _vm.SwapPages[1].ObservePropertyChanged(x => x.IsLoaded)
            .Where(x => x is false)
            .Subscribe(this, (_, s) =>
            {
                s._fadeOutAnim.Start(s.EPubRenderer_2);
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
            .Opacity(0.00001, duration: TimeSpan.FromMilliseconds(50));
    AnimationBuilder _fadeInAnim = AnimationBuilder.Create()
            .Opacity(1, delay: TimeSpan.FromMilliseconds(32), duration: TimeSpan.FromMilliseconds(125));

    void WebView_ContentRefreshStarting_1(object sender, EventArgs e)
    {            
        NowEnablePageMove_1 = false;
    }

    void WebView_ContentRefreshComplete_1(object sender, EventArgs e)
    {        
        if (_navigationCt.IsCancellationRequested) { return; }
        
        NowEnablePageMove_1 = true;
        if (_vm.SwapPages[0].PageHtml != null)
        {
            _vm.CompletePageLoading_1();
        }

        _fadeInAnim.Start(EPubRenderer_1);

        // リサイズ後の更新を順列制御したい
        if (EPubRenderer_2.Visibility == Visibility.Collapsed)
        {
            EPubRenderer_2.Visibility = Visibility.Visible;
            EPubRenderer_2.Refresh();
        }
    }

    void WebView_ContentRefreshStarting_2(object sender, EventArgs e)
    {
        NowEnablePageMove_2= false;
    }

    void WebView_ContentRefreshComplete_2(object sender, EventArgs e)
    {
        if (_navigationCt.IsCancellationRequested) { return; }

        NowEnablePageMove_2 = true;
        if (_vm.SwapPages[1].PageHtml != null)
        {
            _vm.CompletePageLoading_2();
        }

        _fadeInAnim.Start(EPubRenderer_2);

        // リサイズ後の更新を順列制御したい
        if (EPubRenderer_1.Visibility == Visibility.Collapsed)
        {
            EPubRenderer_1.Visibility = Visibility.Visible;
            EPubRenderer_1.Refresh();
        }
    }


    [RelayCommand]
    async Task InnerGoPrevImage()
    {
        if (_vm.EBookReaderSettings.IsReversePageFliping_Button
            || _vm.EBookReaderSettings.OverrideWritingMode == WritingMode.Vertical_LeftToRight)
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
        if (_vm.EBookReaderSettings.IsReversePageFliping_Button
            || _vm.EBookReaderSettings.OverrideWritingMode == WritingMode.Vertical_LeftToRight)
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
        using (await _movePageLock.LockAsync(_navigationCt))
        {
            var currentEPubRenderer = _vm.NowDisplayRendererIndex == 0
                ? EPubRenderer_1
                : EPubRenderer_2;
            if (currentEPubRenderer.CanGoNext())
            {
                currentEPubRenderer.GoNext();
            }
            else
            {
                if (_vm.CanGoNext())
                {
                    var altEPubRenderer = _vm.NowDisplayRendererIndex == 0
                        ? EPubRenderer_2
                        : EPubRenderer_1;
                    EPubRenderer_2.FirstApproachingPageIndex = 0;
                    altEPubRenderer.PrepareGoNext();
                    currentEPubRenderer.PrepareGoNext();
                    if (_vm.IsNextPageCached() is false)
                    {
                        altEPubRenderer.Opacity = 0.00001;
                        await Task.Delay(75);
                    }
                    await _vm.GoNextImageAsync();
                }
                else if (_vm.ViewerSettings.IsAutoMoveToNextEnabled
                        && _vm.NextImageSource != null)
                {
                    if (_vm.CurrentFolderItem != null
                        && _vm.CurrentBookReadingOrder != null)
                    {
                        _vm.BookmarkManager.AddBookmarkForEBookViewer(
                                    _vm.CurrentFolderItem.Path,
                                    _vm.CurrentBookReadingOrder[0].FilePath,
                                    0,
                                    default,
                                    true
                                    );
                    }
                    _vm.OpenEpubFileCommand.Execute(_vm.NextImageSource);
                    _messenger.SendShowTextNotificationMessage("AutoMoveToNext_Notice".Translate(_vm.NextImageSource.Name));
                }
            }
        }
    }

    [RelayCommand]
    async Task ExecuteGoPrevCommand()
    {
        using (await _movePageLock.LockAsync(_navigationCt))
        {
            var currentEPubRenderer = _vm.NowDisplayRendererIndex == 0
                ? EPubRenderer_1
                : EPubRenderer_2;
            if (currentEPubRenderer.CanGoPreview())
            {
                currentEPubRenderer.GoPreview();
            }
            else
            {
                if (_vm.CanGoPrev())
                {
                    var altEPubRenderer = _vm.NowDisplayRendererIndex == 0
                        ? EPubRenderer_2
                        : EPubRenderer_1;

                    // EPubRenderer_2のisFirstContent挙動を踏まえて
                    // 戻りの場合は前ファイルの最後尾ページを開くようにしたい
                    EPubRenderer_2.FirstApproachingPageIndex = int.MaxValue;
                    altEPubRenderer.PrepareGoPreview();
                    currentEPubRenderer.PrepareGoPreview();
                    if (_vm.IsPrevPageCached() is false)
                    {
                        altEPubRenderer.Opacity = 0.00001;
                        await Task.Delay(75);
                    }
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
        _vm.SelectedTocItem = _vm.CurrentPageInfo?.TocItem;
        _vm.CurrentPage = _vm.CurrentPageInfo?.EpubFileRef;
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
        _vm.SelectedTocItem = _vm.CurrentPageInfo?.TocItem;
        _vm.CurrentPage = _vm.CurrentPageInfo?.EpubFileRef;
        TocContainer.Visibility = Visibility.Visible;
    }

    [RelayCommand]
    void CloseTocPane()
    {
        TocContainer.Visibility = Visibility.Collapsed;
    }

    void CoverImage_Tapped(object sender, TappedRoutedEventArgs e)
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

    void CurrentBookReadingOrder_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (((FrameworkElement)sender).IsLoaded == false) { return; }

        if (e.AddedItems.ElementAtOrDefault(0) is EpubLocalTextContentFileRef pageRef)
        {
            _vm.SetPageAsync(pageRef).FireAndForgetSafe();
        }
    }

    void MySwipeDistanceBehavior_Invoked(Behaviors.SwipeDistanceBehavior sender, Behaviors.SwipeDistanceInvokedEventArgs args)
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


    Color _foregroundDefaultColor;
    private void ForegroundColorColorPickerFlyoutOpened(object sender, object e)
    {
        _foregroundDefaultColor = (_vm.GetCurrentTheme() == Core.Models.ApplicationTheme.Dark
                ? Color.FromArgb(0xFF, 0xff, 0xe4, 0xd1)
                : Color.FromArgb(0xFF, 0x1f, 0x1f, 0x1f));
        ForegroundColorPicker.Color = _vm.EBookReaderSettings.ForegroundColor.A == 0x00
            ? _foregroundDefaultColor
            : _vm.EBookReaderSettings.ForegroundColor;
    }

    private void ForegroundColorColorPickerFlyoutClosed(object sender, object e)
    {
        if (ForegroundColorPicker.Color == _foregroundDefaultColor 
            || ForegroundColorPicker.Color.A == 0)
        {
            _vm.EBookReaderSettings.ForegroundColor = Colors.Transparent;
        }
        else
        {
            _vm.EBookReaderSettings.ForegroundColor = ForegroundColorPicker.Color;
        }
    }

    private void ForegroundColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        _vm.EBookReaderSettings.ForegroundColor = ForegroundColorPicker.Color;
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