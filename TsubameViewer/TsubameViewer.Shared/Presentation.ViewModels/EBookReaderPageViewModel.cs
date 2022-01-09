using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
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
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.ReadingFeature;
using TsubameViewer.Models.Domain.EBook;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.SourceFolders;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using TsubameViewer.Presentation.ViewModels.PageNavigation.Commands;
using TsubameViewer.Presentation.Views;
using TsubameViewer.Presentation.Views.ViewManagement.Commands;
using Uno.Threading;
using VersOne.Epub;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Web.Http;

namespace TsubameViewer.Presentation.ViewModels
{
    public sealed class TocItemViewModel
    {
        public string Label { get; set; }
        public string Id { get; set; }
    }

    public sealed class EBookReaderPageViewModel : ViewModelBase
    {
        string _AppCSS;
        string _LightThemeCss;
        string _DarkThemeCss;

        private string _currentPath;
        private StorageFile _currentFolderItem;


        private int _CurrentImageIndex;
        public int CurrentImageIndex
        {
            get => _CurrentImageIndex;
            set => SetProperty(ref _CurrentImageIndex, value);
        }

        public IReadOnlyReactiveProperty<int> DisplayInnerCurrentImageIndex { get; }


        private int _InnerCurrentImageIndex;
        public int InnerCurrentImageIndex
        {
            get => _InnerCurrentImageIndex;
            set => SetProperty(ref _InnerCurrentImageIndex, value);
        }

        private int _InnerImageTotalCount;
        public int InnerImageTotalCount
        {
            get => _InnerImageTotalCount;
            set => SetProperty(ref _InnerImageTotalCount, value);
        }



        EpubBookRef _currentBook;
        EpubTextContentFileRef _currentPage;
        List<EpubTextContentFileRef> _currentBookReadingOrder;
        
        private IReadOnlyList<TocItemViewModel> _TocItems;
        public IReadOnlyList<TocItemViewModel> TocItems
        {
            get { return _TocItems; }
            set { SetProperty(ref _TocItems, value); }
        }

        private TocItemViewModel _selectedTocItem;
        public TocItemViewModel SelectedTocItem
        {
            get { return _selectedTocItem; }
            set { SetProperty(ref _selectedTocItem, value); }
        }

        private string _PageHtml;
        public string PageHtml
        {
            get { return _PageHtml; }
            set { SetProperty(ref _PageHtml, value); }
        }

        private string _title;
        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value); }
        }

        private string _currentPageTitle;
        public string CurrentPageTitle
        {
            get { return _currentPageTitle; }
            set { SetProperty(ref _currentPageTitle, value); }
        }

        private BitmapImage _CoverImage;
        public BitmapImage CoverImage
        {
            get { return _CoverImage; }
            set { SetProperty(ref _CoverImage, value); }
        }


        private DelegateCommand _ResetEBookReaderSettingsCommand;
        public DelegateCommand ResetEBookReaderSettingsCommand =>
            _ResetEBookReaderSettingsCommand ?? (_ResetEBookReaderSettingsCommand = new DelegateCommand(ExecuteResetEBookReaderSettingsCommand));

        void ExecuteResetEBookReaderSettingsCommand()
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



        CompositeDisposable _navigationDisposables;
        
        private CancellationTokenSource _leavePageCancellationTokenSource;
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly BookmarkManager _bookmarkManager;
        private readonly ThumbnailManager _thumbnailManager;
        private readonly RecentlyAccessManager _recentlyAccessManager;
        private readonly ApplicationSettings _applicationSettings;
        private readonly IScheduler _scheduler;

        public EBookReaderSettings EBookReaderSettings { get; }
        public IReadOnlyList<double> RootFontSizeItems { get; } = Enumerable.Range(10, 50).Select(x => (double)x).ToList();
        public IReadOnlyList<double> LeffterSpacingItems { get; } = Enumerable.Concat(Enumerable.Range(0, 20).Select(x => (x - 10) * 0.1), Enumerable.Range(1, 9).Select(x => (double)x)).ToList();
        public IReadOnlyList<double> LineHeightItems { get; } = Enumerable.Range(1, 40).Select(x => x * 0.1).Select(x => (double)x).ToList();
        public IReadOnlyList<double> RubySizeItems { get; } = Enumerable.Range(0, 51).Select(x => (double)x).ToList();
        public IReadOnlyList<string> SystemFontFamilies { get; } = Microsoft.Graphics.Canvas.Text.CanvasTextFormat.GetSystemFontFamilies();
        public IReadOnlyList<ApplicationTheme> ThemeItems { get; } = new[] { ApplicationTheme.Default, ApplicationTheme.Light, ApplicationTheme.Dark };
        public IReadOnlyList<WritingMode> WritingModeItems { get; } = new[] { WritingMode.Inherit, WritingMode.Horizontal_TopToBottom, WritingMode.Vertical_RightToLeft, /*WritingMode.Vertical_LeftToRight*/ };

        Windows.UI.ViewManagement.ApplicationView _applicationView;

        public EBookReaderPageViewModel(
            SourceStorageItemsRepository sourceStorageItemsRepository,
            BookmarkManager bookmarkManager,
            ThumbnailManager thumbnailManager,
            RecentlyAccessManager recentlyAccessManager,
            ApplicationSettings applicationSettings,
            EBookReaderSettings themeSettings,
            IScheduler scheduler,
            ToggleFullScreenCommand toggleFullScreenCommand,
            BackNavigationCommand backNavigationCommand
            )
        {
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
            _bookmarkManager = bookmarkManager;
            _thumbnailManager = thumbnailManager;
            _recentlyAccessManager = recentlyAccessManager;
            _applicationSettings = applicationSettings;
            ToggleFullScreenCommand = toggleFullScreenCommand;
            BackNavigationCommand = backNavigationCommand;
            EBookReaderSettings = themeSettings;
            _scheduler = scheduler;

            DisplayInnerCurrentImageIndex = this.ObserveProperty(x => x.InnerCurrentImageIndex)
                .Select(x => x + 1)
                .ToReadOnlyReactivePropertySlim();

            _applicationView = ApplicationView.GetForCurrentView();
        }


        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
            lock (_lock)
            {
                PageHtml = String.Empty;

                _leavePageCancellationTokenSource?.Cancel();
                _leavePageCancellationTokenSource?.Dispose();
                _leavePageCancellationTokenSource = null;

                _navigationDisposables.Dispose();
                _navigationDisposables = null;
                _currentBook = null;
                _currentPage = null;
                _currentBookReadingOrder = null;
                CoverImage = null;

                _readingSessionDisposer.Dispose();
                _readingSessionDisposer = null;

                _applicationView.Title = string.Empty;
            }

            base.OnNavigatedFrom(parameters);
        }

        public override async Task OnNavigatedToAsync(INavigationParameters parameters)
        {
            _AppCSS ??= await FileIO.ReadTextAsync(await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/EPub/app.css")));
            _DarkThemeCss ??= await FileIO.ReadTextAsync(await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/EPub/DarkTheme.css")));
            _LightThemeCss ??= await FileIO.ReadTextAsync(await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/EPub/LightTheme.css")));

            _navigationDisposables = new CompositeDisposable();
            _leavePageCancellationTokenSource = new CancellationTokenSource();
            var mode = parameters.GetNavigationMode();

            string parsedPageName = null;

            if (mode == NavigationMode.New
                || mode == NavigationMode.Forward
                || mode == NavigationMode.Back)
            {
                if (parameters.TryGetValue(PageNavigationConstants.Path, out string path))
                {
                    (var itemPath, parsedPageName, _) = PageNavigationConstants.ParseStorageItemId(Uri.UnescapeDataString(path));

                    var unescapedPath = itemPath;
                    if (_currentPath != unescapedPath)
                    {
                        _currentPath = unescapedPath;
                        // PathReferenceCountManagerへの登録が遅延する可能性がある
                        foreach (var _ in Enumerable.Repeat(0, 100))
                        {
                            _currentFolderItem = await _sourceStorageItemsRepository.GetStorageItemFromPath(_currentPath) as StorageFile;
                            if (_currentFolderItem != null)
                            {
                                break;
                            }
                            await Task.Delay(100);
                        }

                        if (_currentFolderItem == null)
                        {
                            throw new ArgumentException("EBookReaderPage can not open StorageFolder.");
                        }
                    }
                }
            }

            
            if (_currentFolderItem != null)
            {
                await RefreshItems(_leavePageCancellationTokenSource.Token);
            }

            // 表示する画像を決める
            if (mode == NavigationMode.Forward
                || parameters.ContainsKey(PageNavigationConstants.Restored)
                || (mode == NavigationMode.New && string.IsNullOrEmpty(parsedPageName))
                )
            {
                var bookmark = _bookmarkManager.GetBookmarkedPageNameAndIndex(_currentFolderItem.Path);
                if (bookmark.pageName != null)
                {
                    for (var i = 0; i < _currentBookReadingOrder.Count; i++)
                    {
                        if (_currentBookReadingOrder[i].FileName == bookmark.pageName)
                        {
                            CurrentImageIndex = i;
                            InnerCurrentImageIndex = bookmark.innerPageIndex;
                            SelectedTocItem = TocItems.FirstOrDefault(x => x.Id.StartsWith(bookmark.pageName));
                            break;
                        }
                    }
                }
            }
            else if (mode == NavigationMode.New && !string.IsNullOrEmpty(parsedPageName))
            {
                var unescapedPageName = parsedPageName;
                var firstSelectItem = _currentBookReadingOrder.FirstOrDefault(x => x.FileName == unescapedPageName);
                if (firstSelectItem != null)
                {
                    CurrentImageIndex = _currentBookReadingOrder.IndexOf(firstSelectItem);
                }

                SelectedTocItem = TocItems.FirstOrDefault(x => x.Id.StartsWith(firstSelectItem.FileName));
            }

            // 最初のページを表示
            await UpdateCurrentPage(CurrentImageIndex);

            // ブックマーク更新
            this.ObserveProperty(x => x.InnerCurrentImageIndex, isPushCurrentValueAtFirst: false)
                .Subscribe(innerPageIndex =>
                {
                    var currentPage = _currentBookReadingOrder.ElementAtOrDefault(CurrentImageIndex);
                    if (currentPage == null) { return; }

                    _bookmarkManager.AddBookmark(_currentFolderItem.Path, currentPage.FileName, innerPageIndex, new NormalizedPagePosition(_currentBookReadingOrder.Count, _CurrentImageIndex));
                })
                .AddTo(_navigationDisposables);

            await base.OnNavigatedToAsync(parameters);
        }


        private async Task UpdateCurrentPage(int requestPage)
        {
            var ct = _leavePageCancellationTokenSource.Token;
            _InnerPageTotalCountChangedTcs = new TaskCompletionSource<int>();

            try
            {
                CurrentImageIndex = requestPage;

                var currentPage = _currentBookReadingOrder.ElementAtOrDefault(CurrentImageIndex);
                if (currentPage == null) { throw new IndexOutOfRangeException(); }

                _currentPage = currentPage;

                Debug.WriteLine(currentPage.FileName);

                // Tocを更新
                SelectedTocItem = TocItems.FirstOrDefault(x => x.Id.StartsWith(currentPage.FileName));

                // ページ名更新
                CurrentPageTitle = SelectedTocItem?.Label ?? Path.GetFileNameWithoutExtension(currentPage.FileName);

                using (var lockReleaser = await _PageUpdateLock.LockAsync(ct))
                {
                    ApplicationTheme theme = _applicationSettings.Theme;
                    if (theme == ApplicationTheme.Default)
                    {
                        theme = ApplicationSettings.GetSystemTheme();
                    }

                    PageHtml = await Task.Run(async () =>
                    {
                        var xmlDoc = new XmlDocument();
                        var pageContentText = await currentPage.ReadContentAsync();
                        xmlDoc.LoadXml(pageContentText);

                        var root = xmlDoc.DocumentElement;

                        Stack<XmlNode> _nodes = new Stack<XmlNode>();
                        _nodes.Push(root);
                        while (_nodes.Any())
                        {
                            var node = _nodes.Pop();

                            if (node.Name == "head")
                            {
                                var cssItems = new[] { _AppCSS, theme == ApplicationTheme.Light ? _LightThemeCss : _DarkThemeCss };
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
                                XmlAttribute imageSourceAttr = null;
                                if (node.Name == "img")
                                {
                                    imageSourceAttr = node.Attributes["src"];
                                }
                                else if (node.Name == "image")
                                {
                                    imageSourceAttr = node.Attributes["href"] ?? node.Attributes["xlink:href"];
                                }
                                if (imageSourceAttr != null)
                                {
                                    foreach (var image in _currentBook.Content.Images)
                                    {
                                        if (imageSourceAttr.Value.EndsWith(image.Key))
                                        {
                                            // WebView.WebResourceRequestedによるリソース解決まで画像読み込みを遅延させる
                                            /// <see cref="ResolveWebResourceRequest"/>
                                            imageSourceAttr.Value = DummyReosurceRequestDomain + image.Key;
                                        }
                                    }
                                }
                            }

                            // cssの埋め込み
                            {
                                if (node.Name == "link"
                                    && node.Attributes["type"]?.Value == "text/css"
                                )
                                {
                                    var hrefAttr = node.Attributes["href"];
                                    if (hrefAttr != null)
                                    {
                                        var hrefValue = hrefAttr.Value.Split("/").Last();
                                        if (_currentBook.Content.Css.FirstOrDefault(x => x.Key.EndsWith(hrefValue)).Value is not null and var cssContent)
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
                                _nodes.Push(child as XmlNode);
                            }
                        }

                        using (var stringWriter = new StringWriter())
                        using (var xmlTextWriter = XmlWriter.Create(stringWriter))
                        {
                            xmlDoc.WriteTo(xmlTextWriter);
                            xmlTextWriter.Flush();
                            return stringWriter.GetStringBuilder().ToString();
                        }
                    }, ct);

                    // ブックマークに登録
                    _bookmarkManager.AddBookmark(_currentFolderItem.Path, currentPage.FileName, new NormalizedPagePosition(_currentBookReadingOrder.Count, CurrentImageIndex));
                }
            }
            catch (OperationCanceledException)
            {
                _InnerPageTotalCountChangedTcs = null;
                return;
            }


            // Rendererの更新待ち
            // PageHtmlが表示されるまで更新のLockを止め続けることで
            // ページ内ページ（InnerCurrentImageIndex） の+1/-1ページ単位での遷移を確実にする
            try
            {
                using (var timeoutCts = new CancellationTokenSource(3000))
                {
                    using (timeoutCts.Token.Register(() =>
                    {
                        _InnerPageTotalCountChangedTcs.TrySetCanceled(timeoutCts.Token);
                    }))
                    {
                        await _InnerPageTotalCountChangedTcs.Task.ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // TODO: EBookReaderページのページ更新に失敗した場合の表示
            }
            finally
            {
                _InnerPageTotalCountChangedTcs = null;
            }
        }

        public void CompletePageLoading()
        {
            _InnerPageTotalCountChangedTcs?.SetResult(0);
        }



        const string DummyReosurceRequestDomain = "https://dummy.com/";
        object _lock = new object();
        public Stream ResolveWebResourceRequest(Uri requestUri)
        {
            // 注意: EPubReader側の非同期処理に２つのセンシティブな挙動がある
            // 1. 同時呼び出し不可。lockによる順列処理化が必要
            // 2. EPubContentFileRef.ReadContentAsBytesAsync()などのAsync系は呼び出し後は
            //    EPubReader内部の別スレッドにスイッチする（確証なし）ようなので、
            //    ResolveWebResourceRequest呼び出し元とは違うスレッドになってしまう可能性がある
            //    ライブラリ側としてはかなり例外的な内部処理だと思うがAsync系メソッドさえ回避すれば問題ない
            lock (_lock)
            {
                // ページが表示されていない状態の場合はnullを返す
                if (_navigationDisposables == null) { throw new InvalidOperationException(); }

                var key = requestUri.OriginalString.Remove(0, DummyReosurceRequestDomain.Length);
                foreach (var image in _currentBook.Content.Images)
                {
                    if (image.Key == key)
                    {
                        return new MemoryStream(image.Value.ReadContentAsBytes());
                    }
                }

                throw new NotSupportedException();
            }
        }



        TaskCompletionSource<int> _InnerPageTotalCountChangedTcs;

        Models.Infrastructure.AsyncLock _PageUpdateLock = new ();

        CompositeDisposable _readingSessionDisposer;


        private async Task RefreshItems(CancellationToken ct)
        {
            _readingSessionDisposer?.Dispose();
            _readingSessionDisposer = new CompositeDisposable();
 
            var fileStream = await _currentFolderItem.OpenStreamForReadAsync()
                .AddTo(_readingSessionDisposer);

            var epubBook = await EpubReader.OpenBookAsync(fileStream)
                .AddTo(_readingSessionDisposer);

            

            _currentBook = epubBook;
            _currentBookReadingOrder = await _currentBook.GetReadingOrderAsync();

            if (_currentBook.Schema.Epub2Ncx != null)
            {
                TocItems = _currentBook.Schema.Epub2Ncx.NavMap.Select(x => new TocItemViewModel() { Id = x.Content.Source, Label = x.NavigationLabels[0].Text }).ToList();
            }
            else if (_currentBook.Schema.Epub3NavDocument != null)
            {
                var toc = _currentBook.Schema.Epub3NavDocument.Navs.FirstOrDefault(x => x.Type == VersOne.Epub.Schema.StructuralSemanticsProperty.TOC);
                TocItems = toc.Ol.Lis.Select(x => new TocItemViewModel() { Id = x.Anchor.Href, Label = x.Anchor.Text }).ToList();
            }
            else
            {

            }

            SelectedTocItem = TocItems.FirstOrDefault();

            var thumbnailFile = await _thumbnailManager.GetFileThumbnailImageFileAsync(_currentFolderItem, ct);
            if (thumbnailFile != null)
            {
                using (thumbnailFile)
                {
                    var image = new BitmapImage();
                    await image.SetSourceAsync(thumbnailFile).AsTask(ct);
                    CoverImage = image;
                }
            }

            Debug.WriteLine(epubBook.Title);

            // タイトルを更新
            Title = _currentBook.Title;
            _applicationView.Title = _currentBook.Title;


            _recentlyAccessManager.AddWatched(_currentPath, DateTimeOffset.Now);
        }


        // call from View
        public async void UpdateFromCurrentTocItem()
        {
            if (SelectedTocItem == null) { return; }

            var selectedBook = _currentBookReadingOrder.FirstOrDefault(x => SelectedTocItem.Id.StartsWith(x.FileName));
            if (selectedBook == null)
            {
                throw new Exception();
            }

            if (selectedBook == _currentPage) 
            {
                return; 
            }

            await UpdateCurrentPage(_currentBookReadingOrder.IndexOf(selectedBook));
        }


        #region Commands

        public ToggleFullScreenCommand ToggleFullScreenCommand { get; }
        public BackNavigationCommand BackNavigationCommand { get; }

        public async Task GoNextImageAsync()
        {
            await UpdateCurrentPage(Math.Min(CurrentImageIndex + 1, _currentBookReadingOrder.Count - 1));
        }


        public bool CanGoNext()
        {
            return _currentBookReadingOrder?.Count > CurrentImageIndex + 1;
        }

        public async Task GoPrevImageAsync()
        {
            await UpdateCurrentPage(Math.Max(CurrentImageIndex - 1, 0));
        }

        public bool CanGoPrev()
        {
            return _currentBookReadingOrder?.Count >= 2 && CurrentImageIndex > 0;
        }

        #endregion
    }
}
