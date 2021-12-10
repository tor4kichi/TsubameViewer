using Microsoft.Toolkit.Uwp.Helpers;
using Microsoft.Toolkit.Uwp.UI;
using Prism.Commands;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml;
using TsubameViewer.Models.Domain.EBook;
//using TsubameViewer.Presentation.ViewModels;
using Uno.Threading;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// ユーザー コントロールの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234236 を参照してください

namespace TsubameViewer.Presentation.Views.EBookControls
{
    public sealed partial class EPubRenderer : UserControl
    {


        public bool IsVerticalLayout
        {
            get { return (bool)GetValue(IsVerticalLayoutProperty); }
            private set { SetValue(IsVerticalLayoutProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsVerticalLayout.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsVerticalLayoutProperty =
            DependencyProperty.Register("IsVerticalLayout", typeof(bool), typeof(EPubRenderer), new PropertyMetadata(false));





        public bool NowRightToLeftReadingMode
        {
            get { return (bool)GetValue(NowRightToLeftReadingModeProperty); }
            set { SetValue(NowRightToLeftReadingModeProperty, value); }
        }

        // Using a DependencyProperty as the backing store for NowRightToLeftReadingMode.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty NowRightToLeftReadingModeProperty =
            DependencyProperty.Register("NowRightToLeftReadingMode", typeof(bool), typeof(EPubRenderer), new PropertyMetadata(false));



        public bool WithSeparetePage
        {
            get { return (bool)GetValue(WithSeparetePageProperty); }
            set { SetValue(WithSeparetePageProperty, value); }
        }

        // Using a DependencyProperty as the backing store for WithSeparetePage.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty WithSeparetePageProperty =
            DependencyProperty.Register("WithSeparetePage", typeof(bool), typeof(EPubRenderer), new PropertyMetadata(false));




        public bool NowOnlyImageView
        {
            get { return (bool)GetValue(NowOnlyImageViewProperty); }
            private set { SetValue(NowOnlyImageViewProperty, value); }
        }

        // Using a DependencyProperty as the backing store for NowOnlyImageView.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty NowOnlyImageViewProperty =
            DependencyProperty.Register("NowOnlyImageView", typeof(bool), typeof(EPubRenderer), new PropertyMetadata(false));



        public Color PageBackgroundColor
        {
            get { return (Color)GetValue(PageBackgroundColorProperty); }
            set { SetValue(PageBackgroundColorProperty, value); }
        }

        // Using a DependencyProperty as the backing store for PageBackgroundColor.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PageBackgroundColorProperty =
            DependencyProperty.Register("PageBackgroundColor", typeof(Color), typeof(EPubRenderer), new PropertyMetadata(Colors.Transparent));




        public int TotalInnerPageCount
        {
            get { return (int)GetValue(TotalInnerPageCountProperty); }
            private set { SetValue(TotalInnerPageCountProperty, value); }
        }

        // Using a DependencyProperty as the backing store for TotalInnerPageCount.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TotalInnerPageCountProperty =
            DependencyProperty.Register("TotalInnerPageCount", typeof(int), typeof(EPubRenderer), new PropertyMetadata(0));




        public int CurrentInnerPage
        {
            get { return (int)GetValue(CurrentInnerPageProperty); }
            private set { SetValue(CurrentInnerPageProperty, value); }
        }

        // Using a DependencyProperty as the backing store for CurrentInnerPage.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CurrentInnerPageProperty =
            DependencyProperty.Register("CurrentInnerPage", typeof(int), typeof(EPubRenderer), new PropertyMetadata(0));





        public int FirstApproachingPageIndex
        {
            get { return (int)GetValue(FirstApproachingPageIndexProperty); }
            set { SetValue(FirstApproachingPageIndexProperty, value); }
        }

        // Using a DependencyProperty as the backing store for FirstApproachingPageIndex.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty FirstApproachingPageIndexProperty =
            DependencyProperty.Register("FirstApproachingPageIndex", typeof(int), typeof(EPubRenderer), new PropertyMetadata(0));



        public int PreservedCurrentInnerPageIndex
        {
            get { return (int)GetValue(PreservedCurrentInnerPageIndexProperty); }
            set { SetValue(PreservedCurrentInnerPageIndexProperty, value); }
        }

        // Using a DependencyProperty as the backing store for PreservedCurrentInnerPageIndex.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PreservedCurrentInnerPageIndexProperty =
            DependencyProperty.Register("PreservedCurrentInnerPageIndex", typeof(int), typeof(EPubRenderer), new PropertyMetadata(0));






        public string ContentsFontFamily
        {
            get { return (string)GetValue(ContentsFontFamilyProperty); }
            set { SetValue(ContentsFontFamilyProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ContentsFontFamily.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ContentsFontFamilyProperty =
            DependencyProperty.Register("ContentsFontFamily", typeof(string), typeof(EPubRenderer), new PropertyMetadata(null));



        public string RubyFontFamily
        {
            get { return (string)GetValue(RubyFontFamilyProperty); }
            set { SetValue(RubyFontFamilyProperty, value); }
        }

        // Using a DependencyProperty as the backing store for RubyFontFamily.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty RubyFontFamilyProperty =
            DependencyProperty.Register("RubyFontFamily", typeof(string), typeof(EPubRenderer), new PropertyMetadata(null));





        public double LetterSpacingInPixel
        {
            get { return (double)GetValue(LetterSpacingInPixelProperty); }
            set { SetValue(LetterSpacingInPixelProperty, value); }
        }

        // Using a DependencyProperty as the backing store for LetterSpacingInPixel.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty LetterSpacingInPixelProperty =
            DependencyProperty.Register("LetterSpacingInPixel", typeof(double), typeof(EPubRenderer), new PropertyMetadata(0.0));





        public double LineHeightInNoUnit
        {
            get { return (double)GetValue(LineHeightInNoUnitProperty); }
            set { SetValue(LineHeightInNoUnitProperty, value); }
        }

        // Using a DependencyProperty as the backing store for LineHeightInNoUnit.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty LineHeightInNoUnitProperty =
            DependencyProperty.Register("LineHeightInNoUnit", typeof(double), typeof(EPubRenderer), new PropertyMetadata(1.5));




        public double RubyFontSizeInPixel
        {
            get { return (double)GetValue(RubyFontSizeInPixelProperty); }
            set { SetValue(RubyFontSizeInPixelProperty, value); }
        }

        // Using a DependencyProperty as the backing store for RubyFontSizeInPixel.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty RubyFontSizeInPixelProperty =
            DependencyProperty.Register("RubyFontSizeInPixel", typeof(double), typeof(EPubRenderer), new PropertyMetadata(10.0));




        public Color FontColor
        {
            get { return (Color)GetValue(FontColorProperty); }
            set { SetValue(FontColorProperty, value); }
        }

        // Using a DependencyProperty as the backing store for FontColor.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty FontColorProperty =
            DependencyProperty.Register("FontColor", typeof(Color), typeof(EPubRenderer), new PropertyMetadata(Colors.Transparent));




        public WritingMode OverrideWritingMode
        {
            get { return (WritingMode)GetValue(OverrideWritingModeProperty); }
            set { SetValue(OverrideWritingModeProperty, value); }
        }

        // Using a DependencyProperty as the backing store for OverrideWritingMode.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty OverrideWritingModeProperty =
            DependencyProperty.Register("OverrideWritingMode", typeof(WritingMode), typeof(EPubRenderer), new PropertyMetadata(WritingMode.Inherit));





        public string PageHtml
        {
            get { return (string)GetValue(PageHtmlProperty); }
            set { SetValue(PageHtmlProperty, value); }
        }

        // Using a DependencyProperty as the backing store for PageHtml.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PageHtmlProperty =
            DependencyProperty.Register("PageHtml", typeof(string), typeof(EPubRenderer), new PropertyMetadata(null, OnPageHtmlPropertyChanged ));

        private static async void OnPageHtmlPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var _this = (EPubRenderer)d;

            using var _ = await _this._domUpdateLock.LockAsync(default);

            if (e.NewValue is string newPageHtml && !string.IsNullOrEmpty(newPageHtml))
            {
                _this.ContentRefreshStarting?.Invoke(_this, EventArgs.Empty);
                _this.WebView.NavigateToString(_this.ToStyleEmbedHtml(newPageHtml));
            }
            else
            {
                _this.WebView.NavigateToString(string.Empty);
            }
        }


        // evalでdocument.headにstyleタグを追加しても反映されないため
        // html文字列に直接埋め込む
        string ToStyleEmbedHtml(string pageHtml)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("html, body {");
            if (OverrideWritingMode == WritingMode.Horizontal_TopToBottom)
            {
                sb.Append($"writing-mode: horizontal-tb !important;");
            }
            else if (OverrideWritingMode == WritingMode.Vertical_RightToLeft)
            {
                sb.Append($"writing-mode: vertical-rl !important;");
            }
            else if (OverrideWritingMode == WritingMode.Vertical_LeftToRight)
            {
                sb.Append($"writing-mode: vertical-lr !important;");
            }
            sb.Append("}");

            sb.Append("body, p, span{");
            sb.Append($"letter-spacing: {LetterSpacingInPixel}px !important;");
            sb.Append($"line-height: {LineHeightInNoUnit} !important;");
            if (ContentsFontFamily != null)
            {
                sb.Append($"font-family: \"{ContentsFontFamily}\" !important;");
            }
            if (FontColor != Colors.Transparent)
            {
                var color = FontColor;
                color.A = 0xff;
                sb.Append($"color: rgba({color.R},{color.G},{color.B}, 1.0) !important;");
            }

            sb.Append("}");
            sb.Append("rt {");
            sb.Append($"font-size: {RubyFontSizeInPixel}px !important;");
            if (RubyFontFamily != null)
            {
                sb.Append($"font-family: \"{RubyFontFamily}\" !important;");
            }
            else if (ContentsFontFamily != null)
            {
                sb.Append($"font-family: \"{ContentsFontFamily}\" !important;");
            }
            sb.Append("}");
            string ePubRendererCss = sb.ToString();

            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(pageHtml);

            var root = xmlDoc.DocumentElement;

            Stack<XmlNode> nodes = new Stack<XmlNode>();
            nodes.Push(root);
            while (nodes.Any())
            {
                var node = nodes.Pop();

                if (node.Name == "head")
                {
                    var cssItems = new[] { ePubRendererCss };
                    foreach (var css in cssItems)
                    {
                        var cssNode = xmlDoc.CreateElement("style");
                        var typeAttr = xmlDoc.CreateAttribute("type");
                        typeAttr.Value = "text/css";
                        cssNode.Attributes.Append(typeAttr);
                        cssNode.InnerText = css;
                        node.AppendChild(cssNode);
                    }
                    break;
                }

                foreach (var child in node.ChildNodes)
                {
                    nodes.Push(child as XmlNode);
                }
            }

            using (var stringWriter = new StringWriter())
            using (var xmlTextWriter = XmlWriter.Create(stringWriter))
            {
                xmlDoc.WriteTo(xmlTextWriter);
                xmlTextWriter.Flush();
                return stringWriter.GetStringBuilder().ToString();
            }
        }

        public EPubRenderer()
        {
            this.InitializeComponent();

            Loaded += EPubRenderer_Loaded;
            Unloaded += EPubRenderer_Unloaded;            
        }



        CompositeDisposable _compositeDisposable;

        private void EPubRenderer_Loaded(object sender, RoutedEventArgs e)
        {
            WebView.NavigationStarting -= WebView_NavigationStarting;
            WebView.NavigationCompleted -= WebView_NavigationCompleted;
            WebView.DOMContentLoaded -= WebView_DOMContentLoaded;
            WebView.NavigationStarting += WebView_NavigationStarting;
            WebView.NavigationCompleted += WebView_NavigationCompleted;
            WebView.DOMContentLoaded += WebView_DOMContentLoaded;

            _compositeDisposable = new CompositeDisposable();
            var dispatcher = Dispatcher;

            Observable.FromEventPattern<WindowSizeChangedEventHandler, WindowSizeChangedEventArgs>(
                h => Window.Current.SizeChanged += h,
                h => Window.Current.SizeChanged -= h
                )
                .Do(_ => ContentRefreshStarting?.Invoke(this, EventArgs.Empty))
                .Throttle(TimeSpan.FromMilliseconds(100))
                .Subscribe(async args =>
                {
                    await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => 
                    {
                        await Task.Delay(50);
                        if (IsLoaded is false) { return; }

                        using (await _domUpdateLock.LockAsync(default))
                        {
                            // WebView内部のリサイズが完了してからリサイズさせることで表示崩れを防ぐ
                            //await Task.Delay(50);

                            // リサイズしたら再描画しないとレイアウトが崩れるっぽい
                            WebView.Refresh();
                        }
                    });
                })
                .AddTo(_compositeDisposable);

            new[]
            {
                this.ObserveDependencyProperty(FontSizeProperty),
                this.ObserveDependencyProperty(LetterSpacingInPixelProperty),
                this.ObserveDependencyProperty(LineHeightInNoUnitProperty),
                this.ObserveDependencyProperty(RubyFontSizeInPixelProperty),
                this.ObserveDependencyProperty(ContentsFontFamilyProperty),
                this.ObserveDependencyProperty(RubyFontFamilyProperty),
                this.ObserveDependencyProperty(FontColorProperty),
                this.ObserveDependencyProperty(OverrideWritingModeProperty),
            }
            .Merge()
            .Throttle(TimeSpan.FromMilliseconds(10))
            .Subscribe(_ => { var __ = dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => ReloadPageHtml()); })
            .AddTo(_compositeDisposable);
        }

        private void EPubRenderer_Unloaded(object sender, RoutedEventArgs e)
        {
            WebView.NavigationStarting -= WebView_NavigationStarting;
            WebView.NavigationCompleted -= WebView_NavigationCompleted;
            WebView.DOMContentLoaded -= WebView_DOMContentLoaded;


            _compositeDisposable.Dispose();
        }



        private void WebView_WebResourceRequested(WebView sender, WebViewWebResourceRequestedEventArgs args)
        {
            WebResourceRequested?.Invoke(this, args);
        }

        public event EventHandler<WebViewWebResourceRequestedEventArgs> WebResourceRequested;




        private async void WebView_NavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
        {
            using var _ = await _domUpdateLock.LockAsync(default);
        }

        private async void WebView_NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
        {
            using var _ = await _domUpdateLock.LockAsync(default);
            ContentRefreshComplete?.Invoke(this, EventArgs.Empty);            
        }

        public event EventHandler ContentRefreshStarting;
        public event EventHandler ContentRefreshComplete;

        Models.Infrastructure.AsyncLock _domUpdateLock = new ();

        bool isFirstContent = true;
        int _innerCurrentPage;
        int _innerPageCount;
        int _webViewScrollableSize;
        double _onePageScrollSize;


        public bool CanGoNext()
        {
            return _innerCurrentPage + 1 < _innerPageCount;
        }

        public async void GoNext()
        {
            using var _ = await _domUpdateLock.LockAsync(default);

            if (isContentReady is false) { return; }
            if (!CanGoNext()) { throw new Exception(); }

            _innerCurrentPage++;
            CurrentInnerPage = _innerCurrentPage;
            Debug.WriteLine($"InnerPage: {_innerCurrentPage}/{_innerPageCount}");
            await SetScrollPositionAsync();
        }


        public bool CanGoPreview()
        {
            return _innerCurrentPage > 0;
        }

        public async void GoPreview()
        {
            using var _ = await _domUpdateLock.LockAsync(default);

            if (isContentReady is false) { return; }
            if (!CanGoPreview()) { throw new Exception(); }

            _innerCurrentPage--;
            CurrentInnerPage = _innerCurrentPage;
            Debug.WriteLine($"InnerPage: {_innerCurrentPage}/{_innerPageCount}");
            await SetScrollPositionAsync();
        }


        public void Refresh()
        {
            WebView.Refresh();
        }


        private void ReloadPageHtml()
        {
            ContentRefreshStarting?.Invoke(this, EventArgs.Empty);
            WebView.NavigateToString(this.ToStyleEmbedHtml(PageHtml));
        }


        public void PrepareTocSelectionChange()
        {
            PreservedCurrentInnerPageIndex = 0;
            _isGoNextOrPreview = true;
        }


        public void PrepareGoNext()
        {
            PreservedCurrentInnerPageIndex = 0;
            _isGoNextOrPreview = true;
        }

        public void PrepareGoPreview()
        {
            PreservedCurrentInnerPageIndex = int.MaxValue;
            _isGoNextOrPreview = true;
        }

        private bool _isGoNextOrPreview;

        bool isContentReady = false;
        private async void WebView_DOMContentLoaded(WebView sender, WebViewDOMContentLoadedEventArgs args)
        {
            if (PageHtml == null) { return; }

            using var _ = await _domUpdateLock.LockAsync(default);

            isContentReady = false;


            var oldPageCount = _innerPageCount == 0 ? 1 : _innerPageCount;
            var oldCurrentPageIndex = _innerCurrentPage;
            double oldPageInPercentage = _innerCurrentPage / (double)_innerPageCount;


            //
            // 段組みレイアウトについて
            // column-countを1以上に指定してやると単純にそれだけの段に分かれた表示にできるが
            // 例えば縦書きなら横幅をViewPortサイズに限定することで縦長の段組みを描画できる
            // 縦に等間隔に並んだページに対してページ高さと同等のスクロール量をページ毎に設定することで
            // ページ送りを表現している。
            //

            {
                // 縦書きかをチェック
                var writingModeString = await WebView.InvokeScriptAsync("eval", new[] { @"
                window.getComputedStyle(document.body).getPropertyValue('writing-mode')
                " });
                IsVerticalLayout = writingModeString switch
                {
                    "vertical-rl" => true,
                    "vertical-lr" => true,
                    "sideways-rl" => true,
                    "sideways-lr" => true,
                    _ => false,
                };

                NowRightToLeftReadingMode = writingModeString.EndsWith("rl");

                Debug.WriteLine($"writingModeString: {writingModeString}, IsVerticalLayout: {IsVerticalLayout}");
            }

            var columnCount = !WithSeparetePage ? 1 : 2;
            if (IsVerticalLayout)
            {
                // heightを指定しないと overflow: hidden; が機能しない
                // width: 100vwとすることで表示領域に幅を限定する。段組みをビューポートの高さを越えて縦長に描画させるために必要。
                // column-countは表示領域に対して分割数の上限。段組み描画のために必要。
                // column-rule-widthはデフォルトでmidium。アプリ側での細かい高さ計算の省略ために0pxに指定。
                await WebView.InvokeScriptAsync("eval", new[] { $"document.body.style = \"width: 100vw; overflow: hidden; max-height: {WebView.ActualHeight}px; column-count: {columnCount}; column-rule-width: 0px; font-size:{FontSize}px; \";" });
            }
            else
            {
                // Note: -8は下側と右側の見切れ対策
                await WebView.InvokeScriptAsync("eval", new[] { $"document.body.style = \"overflow: hidden; width:{WebView.ActualWidth - 8}; max-height:{WebView.ActualHeight - 8}px; column-count: {columnCount}; column-rule-width: 0px; font-size:{FontSize}px; \";" });
            }

            //
            // １ページの高さを求める
            // ページの各要素のoffsetが各ページごとのスクロール基準位置の候補となる。
            // 章のタイトルなどズレているものもあるため、
            // アプリ側でスクロール基準位置として使えない値を切り落とす。
            // 先頭からいくつか候補の値をピックアップして、
            // ある値がより多くの他のスクロール基準位置の値を割り切れた(X mod value == 0)場合に１ページの高さとして扱う。
            // 
            {
                string offsetText = IsVerticalLayout ? "offsetTop" : "offsetLeft";
                var sizeList = await WebView.InvokeScriptAsync("eval", new[]
                {
                    $@"
                    const pList = document.querySelectorAll('p, div, img, span');
                    const heightArray = [];
                    var count = 0;
                    for (var i = 0; i < pList.length; i++)
                    {{
                        const elementScrollTop = pList[i].{offsetText};
                        if (elementScrollTop == null) {{ continue; }}

                        if (heightArray.length == 0)
                        {{
                            heightArray.push(elementScrollTop);
                        }}
                        else if (heightArray[count] != elementScrollTop)
                        {{
                            heightArray.push(elementScrollTop);
                            count++;
                        }}                        
                    }}
                    JSON.stringify(heightArray);
                    "
                });
                Debug.WriteLine(sizeList);
                var sizeItems = JsonSerializer.Deserialize<int[]>(sizeList).Distinct();
                var first = sizeItems.ElementAtOrDefault(0);
                sizeItems = sizeItems.Select(x => x - first).ToArray();
                var pageRealSize = IsVerticalLayout ? await GetPageHeight() : await GetPageWidth();
                const int candidateSampleCount = 5;
                const int compareSampleCount = 10;
                int heroPageHeight = -1;
                int heroHitCount = -1;
                foreach (var candidatePageSize in sizeItems.Skip(1).Where(x => x > pageRealSize).Take(candidateSampleCount))
                {
                    var hitCount = sizeItems.TakeLast(compareSampleCount).Count(x => x % candidatePageSize == 0);
                    if (hitCount > heroHitCount)
                    {
                        heroPageHeight = candidatePageSize;
                        heroHitCount = hitCount;
                    }
                }

                if (pageRealSize > heroPageHeight)
                {
                    _innerPageCount = 1;
                    _onePageScrollSize = heroPageHeight;
                    _webViewScrollableSize = heroPageHeight;

                    // 1ページに収まってる場合は画像のみのページかどうかをチェックする
                    var pCount = int.Parse(await WebView.InvokeScriptAsync("eval", new[] { "document.querySelectorAll('p').length.toString();" }));
                    NowOnlyImageView = pCount == 0;
                    Debug.WriteLine("NowOnlyImageView: " + NowOnlyImageView);
                }
                else
                {
                    var pageScrollPositions = sizeItems.Where(x => x % heroPageHeight == 0).ToArray();

                    _innerPageCount = pageScrollPositions.Length;
                    _onePageScrollSize = heroPageHeight;
                    _webViewScrollableSize = pageScrollPositions.Last() + heroPageHeight;

                    NowOnlyImageView = false;
                }

                Debug.WriteLine($"WebViewSize: {_webViewScrollableSize}, pageCount: {_innerPageCount}, onePageScrollSize: {_onePageScrollSize}");

                // ページ最後尾にスクロール用の余白を作る
                // 最後のページのスクロール位置が前ページを含んだ形になってしまう問題を回避する
                await WebView.InvokeScriptAsync("eval", new[]
                {
                    $@"
                    for (var i = 0; i < 100; i++)
                    {{
                        document.body.appendChild(document.createElement('p'));
                    }}
                    "
                });
            }

            if (isFirstContent)
            {
                isFirstContent = false;

                _innerCurrentPage = Math.Clamp(FirstApproachingPageIndex, 0, _innerPageCount - 1);
            }
            else if (!_isGoNextOrPreview) // Refresh
            {
                var newPageCount = _innerPageCount;
                var newCurrentPageIndex = _innerCurrentPage;

                _innerCurrentPage = (int)Math.Round(_innerPageCount * oldPageInPercentage);

                Debug.WriteLine($"{oldCurrentPageIndex}/{oldPageCount} -> {_innerCurrentPage}/{newPageCount}");
            }
            else
            {
                _innerCurrentPage = Math.Min(PreservedCurrentInnerPageIndex, _innerPageCount - 1);
            }

            _isGoNextOrPreview = false;


            await SetScrollPositionAsync();

            TotalInnerPageCount = _innerPageCount;
            CurrentInnerPage = _innerCurrentPage;

            isContentReady = true; 
        }




        private async Task SetScrollPositionAsync()
        {
            double position = _innerCurrentPage * _onePageScrollSize;

            // Note: vertical-rlでは縦スクロールが横倒しして扱われるので縦書き横書きどちらもXにだけ設定すればOK
            await WebView.InvokeScriptAsync("eval", new[] { $"window.scrollTo({position}, 0);" });

#if DEBUG
            if (IsVerticalLayout)
            {
                Debug.WriteLine("ScrollPosition: " + await GetScrollTop());
            }
            else
            {
                Debug.WriteLine("ScrollPosition: " + await GetScrollLeft());
            }
#endif
        }

        private async Task<double> GetScrollableWidth()
        {
            var widthText = await WebView.InvokeScriptAsync("eval", new[] { "Math.max(document.body.scrollWidth, document.documentElement.scrollWidth,document.body.offsetWidth, document.documentElement.offsetWidth,document.body.clientWidth, document.documentElement.clientWidth).toString();" });
            return double.TryParse(widthText, out var value) ? value : 0;
        }

        private async Task<double> GetScrollableHeight()
        {
            var heightText = await WebView.InvokeScriptAsync("eval", new[] { "Math.max(document.body.scrollHeight, document.documentElement.scrollHeight,document.body.offsetHeight, document.documentElement.offsetHeight,document.body.clientHeight, document.documentElement.clientHeight).toString();" });
            return double.TryParse(heightText, out var value) ? value : 0;
        }

        private async Task<double> GetPageWidth()
        {
            var widthText = await WebView.InvokeScriptAsync("eval", new[] { "window.innerWidth.toString()" });
            return double.TryParse(widthText, out var value) ? value : 0;
        }

        private async Task<double> GetPageHeight()
        {
            var heightText = await WebView.InvokeScriptAsync("eval", new[] { "window.innerHeight.toString()" });
            return double.TryParse(heightText, out var value) ? value : 0;
        }

        private async Task<double> GetScrollLeft()
        {
            var XOffsetText = await WebView.InvokeScriptAsync("eval", new[] { "window.pageXOffset.toString()" });
            return double.TryParse(XOffsetText, out var value) ? value : 0;
        }

        private async Task<double> GetScrollTop()
        {
            // Note: writing-mode:vertical-rlが指定されてるとページオフセットの値が上下左右で入れ替わる挙動があるのでpageXOffsetから取得している
            var scrollTop = await WebView.InvokeScriptAsync("eval", new[] { "window.pageYOffset.toString()" });
            return double.TryParse(scrollTop, out var value) ? value : 0;
        }


    }
}
