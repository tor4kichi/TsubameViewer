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
using TsubameViewer.Models.Domain.Bookmark;
using TsubameViewer.Models.Domain.EBook;
using TsubameViewer.Models.Domain.SourceFolders;
using TsubameViewer.Presentation.ViewModels.PageNavigation.Commands;
using TsubameViewer.Presentation.Views;
using TsubameViewer.Presentation.Views.ViewManagement.Commands;
using VersOne.Epub;
using Windows.Storage;
using Windows.UI.Core;

namespace TsubameViewer.Presentation.ViewModels
{
    public sealed class EBookReaderPageViewModel : ViewModelBase
    {
        string _AppCSS;
        string _LightThemeCss;
        string _DarkThemeCss;

        private string _currentToken;
        private StorageFolder _tokenGettingFolder;

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
        List<EpubTextContentFileRef> _currentBookReadingOrder;

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


        private DelegateCommand _ResetEBookReaderSettingsCommand;
        public DelegateCommand ResetEBookReaderSettingsCommand =>
            _ResetEBookReaderSettingsCommand ?? (_ResetEBookReaderSettingsCommand = new DelegateCommand(ExecuteResetEBookReaderSettingsCommand));

        void ExecuteResetEBookReaderSettingsCommand()
        {
            EBookReaderSettings.RootFontSizeInPixel = EBookReaderSettings.DefaultRootFontSizeInPixel;
            EBookReaderSettings.LetterSpacingInPixel = EBookReaderSettings.DefaultLetterSpacingInPixel;
            EBookReaderSettings.LineHeightInNoUnit = EBookReaderSettings.DefaultLineHeightInNoUnit;
            EBookReaderSettings.RubySizeInPixel = EBookReaderSettings.DefaultRubySizeInPixel;
        }



        CompositeDisposable _navigationDisposables;
        
        private CancellationTokenSource _leavePageCancellationTokenSource;
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly BookmarkManager _bookmarkManager;
        private readonly IScheduler _scheduler;

        public EBookReaderSettings EBookReaderSettings { get; }
        public IReadOnlyList<double> RootFontSizeItems { get; } = Enumerable.Range(10, 50).Select(x => (double)x).ToList();
        public IReadOnlyList<double> LeffterSpacingItems { get; } = Enumerable.Range(1, 40).Select(x => x - 10).Select(x => (double)x).ToList();
        public IReadOnlyList<double> LineHeightItems { get; } = Enumerable.Range(1, 40).Select(x => x * 0.1).Select(x => (double)x).ToList();
        public IReadOnlyList<double> RubySizeItems { get; } = Enumerable.Range(1, 50).Select(x => (double)x).ToList();

        public EBookReaderPageViewModel(
            SourceStorageItemsRepository sourceStorageItemsRepository,
            BookmarkManager bookmarkManager,
            EBookReaderSettings themeSettings,
            IScheduler scheduler,
            ToggleFullScreenCommand toggleFullScreenCommand,
            BackNavigationCommand backNavigationCommand
            )
        {
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
            _bookmarkManager = bookmarkManager;
            ToggleFullScreenCommand = toggleFullScreenCommand;
            BackNavigationCommand = backNavigationCommand;
            EBookReaderSettings = themeSettings;
            _scheduler = scheduler;

            DisplayInnerCurrentImageIndex = this.ObserveProperty(x => x.InnerCurrentImageIndex)
                .Select(x => x + 1)
                .ToReadOnlyReactivePropertySlim();
        }


        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
            _leavePageCancellationTokenSource?.Cancel();
            _leavePageCancellationTokenSource?.Dispose();
            _leavePageCancellationTokenSource = null;

            _navigationDisposables.Dispose();
            _navigationDisposables = null;

            _readingSessionDisposer.Dispose();
            _readingSessionDisposer = null;


            base.OnNavigatedFrom(parameters);
        }

        public override void OnNavigatingTo(INavigationParameters parameters)
        {
            PrimaryWindowCoreLayout.SetCurrentNavigationParameters(parameters);

            base.OnNavigatingTo(parameters);
        }

        public override async Task OnNavigatedToAsync(INavigationParameters parameters)
        {
            _AppCSS ??= await FileIO.ReadTextAsync(await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/EPub/app.css")));
            _DarkThemeCss ??= await FileIO.ReadTextAsync(await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/EPub/DarkTheme.css")));
            _LightThemeCss ??= await FileIO.ReadTextAsync(await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/EPub/LightTheme.css")));

            _navigationDisposables = new CompositeDisposable();
            _leavePageCancellationTokenSource = new CancellationTokenSource();
            var mode = parameters.GetNavigationMode();

            if (mode == NavigationMode.New
                || mode == NavigationMode.Forward
                || mode == NavigationMode.Back)
            {
                if (parameters.TryGetValue("token", out string token))
                {
                    if (_currentToken != token)
                    {
                        _currentPath = null;
                        _currentFolderItem = null;

                        _currentToken = token;

                        var item = await _sourceStorageItemsRepository.GetItemAsync(token);

                        _tokenGettingFolder = item as StorageFolder;

                        // ファイルアクティベーションなど
                        if (item is StorageFile file)
                        {
                            _currentFolderItem = file;
                        }
                    }
                }
                else
                {
                    throw new ArgumentException("EBookReaderPage was require 'token' parameter.");
                }

                if (parameters.TryGetValue("path", out string path))
                {
                    var unescapedPath = Uri.UnescapeDataString(path);
                    if (_currentPath != unescapedPath)
                    {
                        _currentPath = unescapedPath;
                        if (_tokenGettingFolder == null)
                        {
                            // token がファイルを指す場合は _currentFolderItem を通じて表示する
                            if (_currentFolderItem.Name != unescapedPath)
                            {
                                throw new Exception("token parameter is require for path parameter.");
                            }
                        }
                        else
                        {
                            var item = await FolderHelper.GetFolderItemFromPath(_tokenGettingFolder, _currentPath);

                            if (item is StorageFile file)
                            {
                                _currentFolderItem = file;
                            }
                            else
                            {
                                throw new ArgumentException("EBookReaderPage can not open StorageFolder.");
                            }
                        }
                    }
                }
            }

            
            if (_tokenGettingFolder != null || _currentFolderItem != null)
            {
                await RefreshItems(_leavePageCancellationTokenSource.Token);
            }

            // 表示する画像を決める
            if (mode == NavigationMode.Forward
                || parameters.ContainsKey("__restored")
                || (mode == NavigationMode.New && !parameters.ContainsKey("pageName"))
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
                            break;
                        }
                    }
                }
            }
            else if (mode == NavigationMode.New && parameters.ContainsKey("pageName")
                )
            {
                if (parameters.TryGetValue("pageName", out string pageName))
                {
                    var unescapedPageName = Uri.UnescapeDataString(pageName);
                    var firstSelectItem = _currentBookReadingOrder.FirstOrDefault(x => x.FileName == unescapedPageName);
                    if (firstSelectItem != null)
                    {
                        CurrentImageIndex = _currentBookReadingOrder.IndexOf(firstSelectItem);
                    }
                }
            }

            // ページの切り替え
            new[] 
            {
                this.ObserveProperty(x => x.CurrentImageIndex).ToUnit(),
                EBookReaderSettings.PropertyChangedAsObservable().ToUnit(),
            }
            .Merge()
            .Throttle(TimeSpan.FromMilliseconds(10), _scheduler)
            .Subscribe(async _ => 
                {
                    
                    var currentPage = _currentBookReadingOrder.ElementAtOrDefault(CurrentImageIndex);
                    if (currentPage == null) { throw new IndexOutOfRangeException(); }

                    Debug.WriteLine(currentPage.FileName);

                    ApplicationTheme theme = EBookReaderSettings.Theme;
                    if (theme == ApplicationTheme.Default)
                    {
                        theme = EBookReaderSettings.GetSystemTheme();
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

                            if (node.Name == "html")
                            {
                                var style = node.Attributes["style"];
                                if (style == null)
                                {
                                    style = xmlDoc.CreateAttribute("style");
                                    node.Attributes.Append(style);
                                }
                                style.Value = $"font-size:{EBookReaderSettings.RootFontSizeInPixel}px;";
                            }

                            if (node.Name == "head")
                            {
                                string settingsCss = 
$@"
body, p, span {{ 
    letter-spacing: {EBookReaderSettings.LetterSpacingInPixel}px !important; 
    line-height: {EBookReaderSettings.LineHeightInNoUnit} !important;
}}
rt {{ 
    font-size: {EBookReaderSettings.RubySizeInPixel}px !important; 
}}
";

                                var cssItems = new[] { _AppCSS, settingsCss, theme == ApplicationTheme.Light ? _LightThemeCss : _DarkThemeCss };
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
                                            var base64Image = Convert.ToBase64String(await image.Value.ReadContentAsBytesAsync());
                                            var sb = new StringBuilder();
                                            sb.Append("data:");
                                            sb.Append(image.Value.ContentMimeType);
                                            sb.Append(";base64,");
                                            sb.Append(base64Image);
                                            imageSourceAttr.Value = sb.ToString();
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
                                        if (_currentBook.Content.Css.TryGetValue(hrefValue, out var cssContent))
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
                    });

                    // ブックマークに登録
                    _bookmarkManager.AddBookmark(_currentFolderItem.Path, currentPage.FileName);

                    // タイトルを更新
                    Title = $"{_currentBook.Title} - {Path.GetFileNameWithoutExtension(currentPage.FileName)}";
                })
                .AddTo(_navigationDisposables);

            // ブックマーク更新
            this.ObserveProperty(x => x.InnerCurrentImageIndex, isPushCurrentValueAtFirst: false)
                .Subscribe(innerPageIndex =>
                {
                    var currentPage = _currentBookReadingOrder.ElementAtOrDefault(CurrentImageIndex);
                    if (currentPage == null) { return; }

                    _bookmarkManager.AddBookmark(_currentFolderItem.Path, currentPage.FileName, innerPageIndex);
                })
                .AddTo(_navigationDisposables);

            await base.OnNavigatedToAsync(parameters);
        }

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

            Debug.WriteLine(epubBook.Title);

            GoNextImageCommand.RaiseCanExecuteChanged();
            GoPrevImageCommand.RaiseCanExecuteChanged();
        }



        #region Commands

        public ToggleFullScreenCommand ToggleFullScreenCommand { get; }
        public BackNavigationCommand BackNavigationCommand { get; }

        private DelegateCommand _GoNextImageCommand;
        public DelegateCommand GoNextImageCommand =>
            _GoNextImageCommand ??= new DelegateCommand(ExecuteGoNextImageCommand, CanGoNextCommand);

        private void ExecuteGoNextImageCommand()
        {
            CurrentImageIndex += 1;

            GoNextImageCommand.RaiseCanExecuteChanged();
            GoPrevImageCommand.RaiseCanExecuteChanged();
        }

        private bool CanGoNextCommand()
        {
            return _currentBookReadingOrder?.Count > CurrentImageIndex + 1;
        }

        private DelegateCommand _GoPrevImageCommand;
        public DelegateCommand GoPrevImageCommand =>
            _GoPrevImageCommand ??= new DelegateCommand(ExecuteGoPrevImageCommand, CanGoPrevCommand);

        private void ExecuteGoPrevImageCommand()
        {
            CurrentImageIndex -= 1;

            GoNextImageCommand.RaiseCanExecuteChanged();
            GoPrevImageCommand.RaiseCanExecuteChanged();
        }

        private bool CanGoPrevCommand()
        {
            return _currentBookReadingOrder?.Count >= 2 && CurrentImageIndex > 0;
        }



        private DelegateCommand _SizeChangedCommand;
        public DelegateCommand SizeChangedCommand =>
            _SizeChangedCommand ??= new DelegateCommand(async () =>
            {
                throw new NotImplementedException();
                //if (!(Images?.Any() ?? false)) { return; }

                //await Task.Delay(50);

                //RaisePropertyChanged(nameof(CurrentImageIndex));
            });


        #endregion
    }
}
