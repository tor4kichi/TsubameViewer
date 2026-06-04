using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.IO;
using R3;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.EBook;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.Navigation;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.Services.Navigation;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.ViewModels.PageNavigation.Commands;
using TsubameViewer.ViewModels.ViewManagement.Commands;
using TsubameViewer.Views;
using TsubameViewer.Views.Helpers;
using VersOne.Epub;
using VersOne.Epub.Options;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using ZLinq;

#nullable enable
namespace TsubameViewer.ViewModels;

public sealed class TocItemViewModel
{
    private readonly EpubNavigationItemRef _navItemRef;

    public TocItemViewModel(EpubNavigationItemRef navItemRef)
    {
        _navItemRef = navItemRef;
    }
    public string Label => _navItemRef.Title;
    public string FilePath => _navItemRef.HtmlContentFileRef?.FilePath ?? "";
}

public sealed partial class EBookViewerPageViewModel : NavigationAwareViewModelBase
{
    string? _lightThemeCss;
    string? _darkThemeCss;

    string? _currentPath;

    [ObservableProperty]
    StorageFile? _currentFolderItem;

    [ObservableProperty]
    int _currentImageIndex;

    partial void OnCurrentImageIndexChanging(int value)
    {
        InnerCurrentImageIndex = 0;
    }

    [ObservableProperty]
    int _innerCurrentImageIndex;
    [ObservableProperty]
    int _innerImageTotalCount;

    [ObservableProperty]
    EpubBookRef? _currentBook;

    [ObservableProperty]
    EpubLocalTextContentFileRef? _currentPage;

    [ObservableProperty]
    List<EpubLocalTextContentFileRef>? _currentBookReadingOrder;    
    [ObservableProperty]
    IReadOnlyList<TocItemViewModel>? _tocItems;
    [ObservableProperty]
    TocItemViewModel? _selectedTocItem;
    [ObservableProperty]
    XmlDocument? _pageHtml = null;
    [ObservableProperty]
    string _title = "";
    [ObservableProperty]
    string _currentPageTitle = "";
    [ObservableProperty]
    BitmapImage? _coverImage;
    [ObservableProperty]
    double _totalReadingItemContentSize = 0;
    [ObservableProperty]
    double _currentReadingItemPosition = 0;

    [ObservableProperty]
    bool _nowFirstLoadingProgress;

    [ObservableProperty]
    int _firstApproachingPageIndex;


    [RelayCommand]
    void ResetEBookReaderSettings()
    {
        EBookReaderSettings.RootFontSizeInPixel = EBookReaderSettings.DefaultRootFontSizeInPixel;
        EBookReaderSettings.LetterSpacingInPixel = EBookReaderSettings.DefaultLetterSpacingInPixel;
        EBookReaderSettings.LineHeightInNoUnit = EBookReaderSettings.DefaultLineHeightInNoUnit;
        EBookReaderSettings.RubySizeInPixel = EBookReaderSettings.DefaultRubySizeInPixel;
        EBookReaderSettings.FontFamily = null;
        EBookReaderSettings.RubyFontFamily = null;
        EBookReaderSettings.BackgroundColor = Colors.Transparent;
        EBookReaderSettings.ForegroundColor = Colors.Transparent;
    }

    private readonly IMessenger _messenger;
    private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
    private readonly LocalBookmarkRepository _bookmarkManager;
    private readonly ThumbnailImageManager _thumbnailManager;
    private readonly RecentlyAccessRepository _recentlyAccessRepository;
    private readonly ApplicationSettings _applicationSettings;
    private readonly RecyclableMemoryStreamManager _recyclableMemoryStreamManager;

    public EBookReaderSettings EBookReaderSettings { get; }
    public IReadOnlyList<double> RootFontSizeItems { get; } = Enumerable.Range(10, 50).Select(x => (double)x).ToList();
    public IReadOnlyList<double> LeffterSpacingItems { get; } = Enumerable.Concat(Enumerable.Range(0, 20).Select(x => (x - 10) * 0.1), Enumerable.Range(1, 9).Select(x => (double)x)).ToList();
    public IReadOnlyList<double> LineHeightItems { get; } = Enumerable.Range(1, 40).Select(x => x * 0.1).Select(x => (double)x).ToList();
    public IReadOnlyList<double> RubySizeItems { get; } = Enumerable.Range(0, 51).Select(x => (double)x).ToList();
    public IReadOnlyList<string> SystemFontFamilies { get; } = Microsoft.Graphics.Canvas.Text.CanvasTextFormat.GetSystemFontFamilies();
    public IReadOnlyList<ApplicationTheme> ThemeItems { get; } = new[] { ApplicationTheme.Default, ApplicationTheme.Light, ApplicationTheme.Dark };
    public IReadOnlyList<WritingMode> WritingModeItems { get; } = new[]
    {
        WritingMode.Inherit,
        WritingMode.Horizontal_TopToBottom,
        WritingMode.Vertical_RightToLeft, 
        WritingMode.Vertical_LeftToRight 
    };



    [ObservableProperty]
    WritingMode _defaultWritingMode;

    public EBookViewerPageViewModel(
        IMessenger messenger,
        SourceStorageItemsRepository sourceStorageItemsRepository,
        LocalBookmarkRepository bookmarkManager,
        ThumbnailImageManager thumbnailManager,
        RecentlyAccessRepository recentlyAccessRepository,
        ApplicationSettings applicationSettings,
        EBookReaderSettings themeSettings,
        ToggleFullScreenCommand toggleFullScreenCommand,
        BackNavigationCommand backNavigationCommand,
        RecyclableMemoryStreamManager recyclableMemoryStreamManager
        )
    {
        _messenger = messenger;
        _sourceStorageItemsRepository = sourceStorageItemsRepository;
        _bookmarkManager = bookmarkManager;
        _thumbnailManager = thumbnailManager;
        _recentlyAccessRepository = recentlyAccessRepository;
        _applicationSettings = applicationSettings;
        ToggleFullScreenCommand = toggleFullScreenCommand;
        BackNavigationCommand = backNavigationCommand;
        _recyclableMemoryStreamManager = recyclableMemoryStreamManager;
        EBookReaderSettings = themeSettings;    
    }

    public string ToImageIndexStartWithOne(int bindFor_InnerCurrentImageIndex)
    {
        return (bindFor_InnerCurrentImageIndex + 1).ToString();
    }

    public override void OnNavigatedFrom(INavigationParameters parameters)
    {
        lock (_lock)
        {
            if (_currentBook?.FilePath is { } path)
            {
                _messenger.Send(new LatestContentViewUpdateMessage(path));
            }

            PageHtml = null;

            _currentBook = null;
            CurrentPage = null;
            CurrentBookReadingOrder = null;
            CoverImage = null;
            CurrentImageIndex = -1;
            InnerCurrentImageIndex = -1;
            _readingSessionDisposer?.Dispose();
            _readingSessionDisposer = null;
            ClearPageInfo(SwapPages[0]);
            ClearPageInfo(SwapPages[1]);
        }

        base.OnNavigatedFrom(parameters);
    }

    CancellationToken _navigationCt;
    public override async Task OnNavigatedToAsync(INavigationParameters parameters, CancellationToken ct)
    {
        var mode = parameters.GetNavigationMode();
        if (mode == NavigationMode.Refresh)
        {
            return;
        }

        _navigationCt = ct;

        _darkThemeCss ??= await FileIO.ReadTextAsync(await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/EPub/DarkTheme.css")));
        _lightThemeCss ??= await FileIO.ReadTextAsync(await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/EPub/LightTheme.css")));

        string? parsedPageName = null;

        if (mode == NavigationMode.New
            || mode == NavigationMode.Forward
            || mode == NavigationMode.Back)
        {
            if (parameters.TryGetValue(PageNavigationConstants.GeneralPathKey, out string path))
            {
                (var itemPath, parsedPageName) = PageNavigationConstants.ParseStorageItemId(Uri.UnescapeDataString(path));

                var newPath = itemPath;
                if (_currentPath != newPath)
                {
                    _sourceStorageItemsRepository.ThrowIfPathIsUnauthorizedAccess(newPath);

                    _currentPath = newPath;
                    // PathReferenceCountManagerへの登録が遅延する可能性がある
                    foreach (var _ in Enumerable.Repeat(0, 10))
                    {
                        CurrentFolderItem = await _sourceStorageItemsRepository.TryGetStorageItemFromPath(_currentPath) as StorageFile;
                        if (CurrentFolderItem != null)
                        {
                            break;
                        }
                        await Task.Delay(100);
                    }

                    if (CurrentFolderItem == null)
                    {
                        throw new ArgumentException("EBookReaderPage can not open StorageFolder.");
                    }
                }

                NowFirstLoadingProgress = true;
            }
        }

        await _messenger.WorkWithBusyWallAsync(async (ct) =>
        {
            if (CurrentFolderItem == null)
            {
                ThrowHelper.ThrowInvalidOperationException();
            }
            await RefreshItems(ct);

            if (CurrentBookReadingOrder == null)
            {
                ThrowHelper.ThrowInvalidOperationException();
            }

            int firstRequestIndex = 0;
            InnerCurrentImageIndex = 0;
            FirstApproachingPageIndex = 0;
            // 表示する画像を決める
            if (mode == NavigationMode.Forward
                || parameters.ContainsKey(PageNavigationConstants.Restored)
                || (mode == NavigationMode.New && string.IsNullOrEmpty(parsedPageName))
                )
            {
                var bookmark = _bookmarkManager.GetBookmarkedPageNameAndIndex(CurrentFolderItem.Path);
                if (bookmark.pageName != null)
                {
                    for (var i = 0; i < CurrentBookReadingOrder.Count; i++)
                    {
                        if (CurrentBookReadingOrder[i].FilePath == bookmark.pageName)
                        {
                            firstRequestIndex = i;
                            InnerCurrentImageIndex = bookmark.innerPageIndex;
                            FirstApproachingPageIndex = bookmark.innerPageIndex;
                            break;
                        }
                    }
                }
            }
            else if (mode == NavigationMode.New && !string.IsNullOrEmpty(parsedPageName))
            {
                var unescapedPageName = parsedPageName;
                var firstSelectItem = CurrentBookReadingOrder.FirstOrDefault(x => x.FilePath == unescapedPageName);
                if (firstSelectItem != null)
                {
                    firstRequestIndex = CurrentBookReadingOrder.IndexOf(firstSelectItem);                    
                }
                else
                {
                    firstRequestIndex = 0;                    
                }
            }
            else
            {
                firstRequestIndex = 0;                
            }
            
            // 最初のページを表示
            await UpdateCurrentPage(firstRequestIndex, _navigationCt);
        }, ct);

        var db = new DisposableBuilder();

        // ブックマーク更新
        R3.Observable.Merge(
            this.ObservePropertyChanged(x => x.InnerCurrentImageIndex, false).AsUnitObservable(),
            this.ObservePropertyChanged(x => x.CurrentPage, false).AsUnitObservable()
            )
            .Debounce(TimeSpan.FromMilliseconds(300))
            .Subscribe(this, static (_, s) =>
            {
                if (s.CurrentBookReadingOrder == null) { return; }
                if (s.CurrentFolderItem == null) { return; }
                if (s.InnerCurrentImageIndex == -1) { return; }
                var currentPage = s.CurrentBookReadingOrder.ElementAtOrDefault(s.CurrentImageIndex);
                if (currentPage == null) { return; }

                s._bookmarkManager.AddBookmark(s.CurrentFolderItem.Path, currentPage.FilePath, s.InnerCurrentImageIndex, new NormalizedPagePosition(s.CurrentBookReadingOrder.Count, s.CurrentImageIndex));
            })
            .AddTo(ref db);

        R3.Observable.Merge(
            this.ObservePropertyChanged(x => x.NowLoadingPage, false).AsUnitObservable(),
            this.ObservePropertyChanged(x => x.InnerCurrentImageIndex, true).AsUnitObservable(),
            this.ObservePropertyChanged(x => x.CurrentPage, true).AsUnitObservable()
            )
            .ThrottleLast(TimeSpan.FromMilliseconds(250))
            .Subscribe(this, static (_, s) =>
            {
                if (s.CurrentBookReadingOrder == null) { return; }
                if (s.CurrentFolderItem == null) { return; }
                if (s.InnerCurrentImageIndex == -1) { return; }
                if (s.InnerImageTotalCount <= 0) { return; }
                if (s.CurrentImageIndex == -1) { return; }
                var currentPage = s.CurrentBookReadingOrder.ElementAtOrDefault(s.CurrentImageIndex);
                long totalSize = 0;
                foreach (var item in s.CurrentBookReadingOrder.Take(s.CurrentImageIndex))
                {
                    totalSize += item.ContentFileEntry?.Length ?? 0;
                }
                var currentItem = s.CurrentBookReadingOrder[s.CurrentImageIndex];
                var partialPageUnit = currentItem.ContentFileEntry!.Length / s.InnerImageTotalCount;

                s.CurrentReadingItemPosition = totalSize + partialPageUnit * s._innerCurrentImageIndex;
                Debug.WriteLine($"{s.CurrentReadingItemPosition / s.TotalReadingItemContentSize * 100:F1}%");
            })
            .AddTo(ref db);

        R3.Observable.Merge(
            this.ObservePropertyChanged(x => x.InnerCurrentImageIndex, false).AsUnitObservable(),
            this.ObservePropertyChanged(x => x.CurrentPageInfo, false).AsUnitObservable(),
            SwapPages[0].ObservePropertyChanged(x => x.IsLoaded).AsUnitObservable(),
            SwapPages[1].ObservePropertyChanged(x => x.IsLoaded).AsUnitObservable()            
            )
            .ThrottleLast(TimeSpan.FromSeconds(0.25))
            .Subscribe(this, static (x, s) => 
            {
                var _this = s;
                if (_this.EBookReaderSettings.IsPrepareNextPageEnabled is false) { return; }
                if (_this.CurrentPageInfo == null) { return; }

                var altPage = _this.SwapPages[_this.NowDisplayRendererIndex == 0 ? 1 : 0];
                if (_this.CurrentPageInfo.InnerTotalPageCount - 3 <= _this.InnerCurrentImageIndex
                    && _this.CurrentPageInfo.OuterPageIndex + 1 != altPage.OuterPageIndex)
                {
                    _ = _this.PrepareNextPageAsync();
                }
            })
            .AddTo(ref db);



        EBookReaderSettings.ObservePropertyChanged(x => x.IsForceResetStylingInHeadElement, false)
            .Subscribe(this, static (x, s) => 
            {
                var _this = s;
                _this.PageHtml = null;
                _ = _this.UpdateCurrentPage(_this.CurrentImageIndex, _this._navigationCt);
            })
            .AddTo(ref db);

        db.Build().RegisterTo(ct);

        await base.OnNavigatedToAsync(parameters, ct);
    }

    [ObservableProperty]
    bool _nowLoadingPage;

    [ObservableProperty]
    int _nowDisplayRendererIndex = 0;

    public sealed partial class EBookPageInfo : ObservableObject
    {

        [ObservableProperty]
        int _outerPageIndex = -1;
        [ObservableProperty]
        EpubLocalTextContentFileRef _epubFileRef;
        [ObservableProperty]
        TocItemViewModel _tocItem;
        [ObservableProperty]
        string _title;
        [ObservableProperty]
        XmlDocument _pageHtml;

        [ObservableProperty]
        int _innerTotalPageCount = 1;
        [ObservableProperty]
        int _innerCurrentPageIndex = 0;

        public TaskCompletionSource<int>? LoadingTcs { get; set; }

        public bool IsLoaded { get; set; } = false;
    }

    public EBookPageInfo[] SwapPages { get; } = new EBookPageInfo[2] 
    {
        new EBookPageInfo(), new EBookPageInfo()
    };

    [ObservableProperty]
    EBookPageInfo? _currentPageInfo;

    partial void OnCurrentPageInfoChanged(EBookPageInfo? value)
    {
        if (value == null) { return; }

        CurrentImageIndex = value.OuterPageIndex;
        CurrentPageTitle = value.Title;
    }

    private async Task PrepareNextPageAsync()
    {
        using (var lockReleaser = await _pageUpdateLock.LockAsync(_navigationCt))
        {
            var currentPage = CurrentPageInfo;
            if (currentPage == null)
            {
                return;
            }

            var nextPageIndex = currentPage.OuterPageIndex + 1;
            var swapPageIndex = NowDisplayRendererIndex == 0 ? 1 : 0;
            var nextPage = SwapPages[swapPageIndex];
            if (nextPage.OuterPageIndex == nextPageIndex) { return; }

            Debug.WriteLine($"PrepareNextPage: start");
            // 現在のページがまだ読み込み終わってない場合は読み込みを待機したい
            if (currentPage.LoadingTcs?.Task is { } task)
            {
                Debug.WriteLine($"PrepareNextPage: Waiting primary loading Task.");
                await task;
            }

            Debug.WriteLine($"PrepareNextPage: page at {nextPageIndex:000} loading.");            
            Debug.WriteLine($"PrepareNextPage: target SwapPages[{swapPageIndex}]");
            FillPageInfo(nextPageIndex, nextPage);
            await LoadPageAsync(nextPage, _navigationCt);
            Debug.WriteLine($"PrepareNextPage: Complete.");
        }
    }

    private async Task UpdateCurrentPage(int requestPage, CancellationToken ct)
    {
        if (requestPage <= -1) { return; }
        Debug.WriteLine($"UpdateCurrentPage {requestPage:000}: start");
        NowLoadingPage = true;
        EBookPageInfo? currentLoadingPage = null;
        try
        {
            using (var lockReleaser = await _pageUpdateLock.LockAsync(ct))
            {
                if (requestPage == CurrentPageInfo?.OuterPageIndex) { return; }
                // 読み込み済みにスワップできないか試す
                int nextDisplayPageInfoIndex = NowDisplayRendererIndex == 0 ? 1 : 0;
                var nextPageInfo = SwapPages[nextDisplayPageInfoIndex];
                if (nextPageInfo.OuterPageIndex == requestPage)
                {
                    // もし表示準備ができていれば
                    Debug.WriteLine($"UpdateCurrentPage {requestPage:000}: Already loaded. swap to next page.");
                    currentLoadingPage = nextPageInfo;
                    CurrentPageInfo = currentLoadingPage;
                    NowDisplayRendererIndex = nextDisplayPageInfoIndex;
                    Debug.WriteLine($"UpdateCurrentPage {requestPage:000}: use SwapPages[{nextDisplayPageInfoIndex}].");
                }
                else
                {
                    // 読み込まれてないなら
                    int displayPageInfoIndex = CurrentPageInfo == null
                        ? 0
                        : (NowDisplayRendererIndex == 0 ? 1 : 0);

                    Debug.WriteLine($"UpdateCurrentPage {requestPage:000}: Page load to SwapPage[{displayPageInfoIndex}]");
                    var info = SwapPages[displayPageInfoIndex];
                    FillPageInfo(requestPage, info);
                    await LoadPageAsync(info, ct);
                    CurrentPageInfo = info;
                    NowDisplayRendererIndex = displayPageInfoIndex;
                }
            }
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine($"UpdateCurrentPage {requestPage:000}: Canceled.");
            currentLoadingPage?.LoadingTcs = null;
            NowLoadingPage = false;
            NowFirstLoadingProgress = false;
            throw;
        }
        catch (XamlParseException xmlEx)
        {
            Debug.WriteLine($"UpdateCurrentPage {requestPage:000}: ");
            Debug.WriteLine(xmlEx.ToString());
            currentLoadingPage?.LoadingTcs = null;
            NowLoadingPage = false;
            NowFirstLoadingProgress = false;
            throw new FileNotFoundException("", xmlEx);
        }

        if (currentLoadingPage?.LoadingTcs != null)
        {
            var tcs = currentLoadingPage.LoadingTcs;
            // Rendererの更新待ち
            // PageHtmlが表示されるまで更新のLockを止め続けることで
            // ページ内ページ（InnerCurrentImageIndex） の+1/-1ページ単位での遷移を確実にする
            try
            {
                using (var timeoutCts = new CancellationTokenSource(10000))
                {
                    using (timeoutCts.Token.Register(() =>
                    {
                        tcs.TrySetCanceled(timeoutCts.Token);
                    }))
                    {
                        await tcs.Task;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // TODO: EBookReaderページのページ更新に失敗した場合の表示
                if (CurrentFolderItem != null)
                {
                    _bookmarkManager.RemoveBookmark(CurrentFolderItem.Path);
                }
                throw;
            }
            finally
            {
                currentLoadingPage?.LoadingTcs = null;
                NowLoadingPage = false;
                NowFirstLoadingProgress = false;
            }
        }
    }

    void FillPageInfo(int requestPage, EBookPageInfo pageInfo)
    {
        Guard.IsNotNull(CurrentBookReadingOrder);
        EpubLocalTextContentFileRef currentPage = CurrentBookReadingOrder.ElementAtOrDefault(requestPage);
        if (currentPage == null) { throw new IndexOutOfRangeException(); }
        Debug.WriteLine(currentPage.FilePath);
        pageInfo.OuterPageIndex = requestPage;
        pageInfo.EpubFileRef = currentPage;
        foreach (var item in CurrentBookReadingOrder.AsValueEnumerable().Take(requestPage+1).Reverse())
        {
            if (TocItems.FirstOrDefault(x => x.FilePath.Equals(item.FilePath)) is { } toc)
            {
                pageInfo.TocItem = toc;
                break;
            }
        }
        pageInfo.Title = Path.GetFileNameWithoutExtension(currentPage.FilePath);
        pageInfo.LoadingTcs = new TaskCompletionSource<int>();
        pageInfo.InnerCurrentPageIndex = 0;
        pageInfo.IsLoaded = false;
    }

    void ClearPageInfo(EBookPageInfo pageInfo)
    {
        pageInfo.InnerTotalPageCount = 0;
        pageInfo.InnerCurrentPageIndex = 0;
        pageInfo.Title = "";
        pageInfo.PageHtml = null;
        pageInfo.EpubFileRef = null;
        pageInfo.OuterPageIndex = -1;
        pageInfo.LoadingTcs = null;
        pageInfo.IsLoaded = false;
    }

    async Task LoadPageAsync(EBookPageInfo pageInfo, CancellationToken ct)
    {
        ApplicationTheme theme = _applicationSettings.Theme;
        if (theme == ApplicationTheme.Default)
        {
            theme = SystemThemeHelper.GetSystemTheme();
        }
        var currentPage = pageInfo.EpubFileRef;
        pageInfo.PageHtml = await Task.Run(async () =>
        {
            Guard.IsNotNull(_currentBook);

            var xmlDoc = new XmlDocument();
            var pageContentText = await currentPage.ReadContentAsync();
            xmlDoc.LoadXml(pageContentText);

            var root = xmlDoc.DocumentElement;

            Stack<XmlNode> _nodes = new Stack<XmlNode>();
            _nodes.Push(root);
            while (_nodes.Any())
            {
                var node = _nodes.Pop();

                if (node.Name.Equals("head", StringComparison.Ordinal))
                {
                    if (EBookReaderSettings.IsForceResetStylingInHeadElement)
                    {
                        // ヘッダー要素を全削除してカスタムなスタイルを表示させない
                        node.InnerText = "";
                    }

                    var cssItems = new[] { theme == ApplicationTheme.Light ? _lightThemeCss : _darkThemeCss };
                    foreach (var css in cssItems)
                    {
                        var cssNode = xmlDoc.CreateElement("style");
                        var typeAttr = xmlDoc.CreateAttribute("type");
                        typeAttr.Value = "text/css";
                        cssNode.Attributes.Append(typeAttr);
                        cssNode.InnerText = css;
                        node.AppendChild(cssNode);
                    }
                }

                // 画像リソースの埋め込み
                {
                    XmlAttribute? imageSourceAttr = null;
                    if (node.Name.Equals("img", StringComparison.Ordinal))
                    {
                        imageSourceAttr = node.Attributes["src"];
                    }
                    else if (node.Name.Equals("image", StringComparison.Ordinal))
                    {
                        imageSourceAttr = node.Attributes["href"] ?? node.Attributes["xlink:href"];
                    }
                    if (imageSourceAttr != null)
                    {
                        foreach (var image in _currentBook.Content.Images.Local)
                        {
                            if (imageSourceAttr.Value.EndsWith(image.Key, StringComparison.Ordinal))
                            {
                                // WebView.WebResourceRequestedによるリソース解決まで画像読み込みを遅延させる
                                /// <see cref="ResolveWebResourceRequest"/>
                                imageSourceAttr.Value = _dummyReosurceRequestDomain + image.Key;
                            }
                        }
                    }
                }

                // cssの埋め込み
                {
                    if (node.Name.Equals("link", StringComparison.Ordinal)
                        && (node.Attributes["type"]?.Value.Equals("text/css", StringComparison.Ordinal) ?? false))
                    {
                        var hrefAttr = node.Attributes["href"];
                        if (hrefAttr != null)
                        {
                            var hrefValue = hrefAttr.Value.Split("/").Last();
                            if (_currentBook.Content.Css.Local.FirstOrDefault(x => x.Key.EndsWith(hrefValue, StringComparison.Ordinal)) is not null and var cssContent)
                            {
                                var parent = node.ParentNode;
                                parent.RemoveChild(node);
                                var styleNode = xmlDoc.CreateElement("style");
                                var typeAttr = xmlDoc.CreateAttribute("type");
                                typeAttr.Value = cssContent.ContentMimeType;
                                styleNode.Attributes.Append(typeAttr);
                                styleNode.InnerText = await cssContent.ReadContentAsTextAsync();
                                parent.AppendChild(styleNode);
                            }
                        }
                    }
                }

                foreach (var child in node.ChildNodes)
                {
                    _nodes.Push((XmlNode)child);
                }
            }

            return xmlDoc;
        }, ct);        
    }

    public void CompletePageLoading_1()
    {
        var page = SwapPages[0];
        page.LoadingTcs?.TrySetResult(0);
        page.IsLoaded = true;
    }

    public void CompletePageLoading_2()
    {
        var page = SwapPages[1];
        page.LoadingTcs?.TrySetResult(0);
        page.IsLoaded = true;
    }



    private const string _dummyReosurceRequestDomain = "https://dummy.com/";
    private readonly object _lock = new object();
    StringBuilder _resourceSb = new();    
    public Stream? ResolveWebResourceRequest(Uri requestUri)
    {
        if (_currentBook == null) { return null; }

        // 注意: EPubReader側の非同期処理に２つのセンシティブな挙動がある
        // 1. 同時呼び出し不可。lockによる順列処理化が必要
        // 2. EPubContentFileRef.ReadContentAsBytesAsync()などのAsync系は呼び出し後は
        //    EPubReader内部の別スレッドにスイッチする（確証なし）ようなので、
        //    ResolveWebResourceRequest呼び出し元とは違うスレッドになってしまう可能性がある
        //    ライブラリ側としてはかなり例外的な内部処理だと思うがAsync系メソッドさえ回避すれば問題ない
        lock (_lock)
        {
            _resourceSb.Clear();
            _resourceSb.Append(requestUri.OriginalString);
            _resourceSb.Remove(0, _dummyReosurceRequestDomain.Length);
            var key = _resourceSb.ToString();
            foreach (var image in _currentBook.Content.Images.Local)
            {
                if (image.Key.Equals(key, StringComparison.Ordinal))
                {
                    var stream = _recyclableMemoryStreamManager.GetStream();
                    using (var imageStream = image.GetContentStream())
                    {
                        imageStream.CopyTo(stream);
                    }
                    stream.Seek(0, SeekOrigin.Begin);
                    return stream;
                }
            }

            return Stream.Null;
        }
    }

    Core.AsyncLock _pageUpdateLock = new ();
    IDisposable? _readingSessionDisposer;

    readonly EpubReaderOptions _readerOptions = new EpubReaderOptions()
    {
        XmlReaderOptions = new XmlReaderOptions() { SkipXmlHeaders = true },
        PackageReaderOptions = new PackageReaderOptions() { IgnoreMissingToc = true },        
    };
    private async Task RefreshItems(CancellationToken ct)
    {
        _readingSessionDisposer?.Dispose();
        _readingSessionDisposer = null;
        TocItems = null;
        CurrentBookReadingOrder = null;
        TotalReadingItemContentSize = -1;

        var fileStream = await CurrentFolderItem.OpenStreamForReadAsync();        
        var epubBook = await EpubReader.OpenBookAsync(fileStream, _readerOptions);
        if (epubBook == null)
        {
            return;
        }

        var db = new DisposableBuilder();
        db.Add(fileStream);
        db.Add(epubBook);
        _readingSessionDisposer = db.Build();

        _currentBook = epubBook;
        CurrentBookReadingOrder = await _currentBook.GetReadingOrderAsync();

        List<EpubNavigationItemRef>? navigations = _currentBook.GetNavigation();
        if (navigations != null)
        {
            TocItems = navigations.SelectMany(x =>
            {
                if (x.Type == EpubNavigationItemType.HEADER)
                {
                    return x.NestedItems;
                }
                else
                {
                    return [x];
                }
            })
                .Select(x => new TocItemViewModel(x)).ToList();
        }

        var thumbnailImageStream = await Task.Run(async () => await _thumbnailManager.GetFileThumbnailImageStreamAsync(CurrentFolderItem, ct));
        if (thumbnailImageStream != null)
        {
            using (var ras = thumbnailImageStream.AsRandomAccessStream())
            {
                ras.Seek(0);
                var image = new BitmapImage();
                await image.SetSourceAsync(ras).AsTask(ct);
                CoverImage = image;
            }
        }
        long totalSize = 0;
        foreach (var item in CurrentBookReadingOrder)
        {
            totalSize += item.ContentFileEntry?.Length ?? 0;
        }

        TotalReadingItemContentSize = totalSize;
        Debug.WriteLine(totalSize);

        // タイトルを更新
        Title = _currentBook.Title;
        _recentlyAccessRepository.AddWatched(_currentPath, DateTimeOffset.Now);

        Debug.WriteLine(epubBook.Title);
    }    

    // call from View
    public async void UpdateFromCurrentTocItem()
    {
        if (SelectedTocItem == null) { return; }
        if (CurrentBookReadingOrder == null) { return; }

        var selectedBook = CurrentBookReadingOrder.FirstOrDefault(x => SelectedTocItem.FilePath == x.FilePath);
        if (selectedBook == null)
        {
            throw new Exception();
        }

        if (selectedBook == CurrentPage) 
        {
            return; 
        }

        await UpdateCurrentPage(CurrentBookReadingOrder.IndexOf(selectedBook), _navigationCt);
    }

    public async Task SetPageAsync(EpubLocalTextContentFileRef pageRef)
    {
        if (CurrentBookReadingOrder == null) { return; }
        await UpdateCurrentPage(CurrentBookReadingOrder.IndexOf(pageRef), _navigationCt);
    }




    #region Commands

    public ToggleFullScreenCommand ToggleFullScreenCommand { get; }
    public BackNavigationCommand BackNavigationCommand { get; }

    public async Task GoNextImageAsync()
    {
        if (CurrentBookReadingOrder == null) { return; }
        await UpdateCurrentPage(Math.Min(CurrentImageIndex + 1, CurrentBookReadingOrder.Count - 1), _navigationCt);
    }


    public bool CanGoNext()
    {
        return CurrentBookReadingOrder?.Count > CurrentImageIndex + 1;
    }

    public async Task GoPrevImageAsync()
    {
        await UpdateCurrentPage(Math.Max(CurrentImageIndex - 1, 0), _navigationCt);
    }

    public bool CanGoPrev()
    {
        return CurrentBookReadingOrder?.Count >= 2 && CurrentImageIndex > 0;
    }

    #endregion
}
