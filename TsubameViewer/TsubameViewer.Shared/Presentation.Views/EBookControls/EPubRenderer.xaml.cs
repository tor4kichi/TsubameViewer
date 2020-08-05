using Newtonsoft.Json;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
//using TsubameViewer.Presentation.ViewModels;
using Uno.Threading;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
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

            if (e.NewValue is string newPageHtml)
            {
                _this.WebView.NavigateToString(newPageHtml);
            }
            else
            {
                _this.WebView.NavigateToString(string.Empty);
            }
        }

        public EPubRenderer()
        {
            this.InitializeComponent();

            WebView.NavigationStarting += WebView_NavigationStarting;
            WebView.NavigationCompleted += WebView_NavigationCompleted;
            WebView.DOMContentLoaded += WebView_DOMContentLoaded;
            WebView.SizeChanged += WebView_SizeChanged;
        }

        private void WebView_NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
        {
            ContentRefreshComplete?.Invoke(this, EventArgs.Empty);
        }

        private void WebView_NavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
        {
            ContentRefreshStarting?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler ContentRefreshStarting;
        public event EventHandler ContentRefreshComplete;

        FastAsyncLock _domUpdateLock = new FastAsyncLock();

        bool isFirstContent = true;
        int _innerCurrentPage;
        int _innerPageCount;
        int _webViewScrollableSize;
        double _onePageScrollSize;


        public bool CanGoNext()
        {
            return _innerCurrentPage + 1 < _innerPageCount;
        }

        public void GoNext()
        {
            if (!CanGoNext()) { throw new Exception(); }

            _innerCurrentPage++;
            CurrentInnerPage = _innerCurrentPage;
            Debug.WriteLine($"InnerPage: {_innerCurrentPage}/{_innerPageCount}");
            _ = SetScrollPositionAsync();
        }


        public bool CanGoPreview()
        {
            return _innerCurrentPage > 0;
        }

        public void GoPreview()
        {
            if (!CanGoPreview()) { throw new Exception(); }

            _innerCurrentPage--;
            CurrentInnerPage = _innerCurrentPage;
            Debug.WriteLine($"InnerPage: {_innerCurrentPage}/{_innerPageCount}");
            _ = SetScrollPositionAsync();
        }


        public void Refresh()
        {
            WebView.Refresh();
        }


        bool _Domloaded;

        private async void WebView_DOMContentLoaded(WebView sender, WebViewDOMContentLoadedEventArgs args)
        {
            using var _ = await _domUpdateLock.LockAsync(default);

            _Domloaded = true;

            //
            // 段組みレイアウトについて
            // column-countを1以上に指定してやると単純にそれだけの段に分かれた表示にできるが
            // 例えば縦書きなら横幅をViewPortサイズに限定することで縦長の段組みを描画できる
            // 縦に等間隔に並んだページに対してページ高さと同等のスクロール量をページ毎に設定することで
            // ページ送りを表現している。
            //

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
            Debug.WriteLine($"writingModeString: {writingModeString}, IsVerticalLayout: {IsVerticalLayout}");

            var columnCount = !WithSeparetePage ? 1 : 2;
            if (IsVerticalLayout)
            {
                // heightを指定しないと overflow: hidden; が機能しない
                // width: 100vwとすることで表示領域に幅を限定する。段組みをビューポートの高さを越えて縦長に描画させるために必要。
                // column-countは表示領域に対して分割数の上限。段組み描画のために必要。
                // column-rule-widthはデフォルトでmidium。アプリ側での細かい高さ計算の省略ために0pxに指定。
                await WebView.InvokeScriptAsync("eval", new[] { $"document.body.style = \"width: 100vw; overflow: hidden; max-height: {WebView.ActualHeight}px; column-count: {columnCount}; column-rule-width: 0px; \"" });
            }
            else
            {
                // Note: -8は下側と右側の見切れ対策
                await WebView.InvokeScriptAsync("eval", new[] { $"document.body.style = \"overflow: hidden; width:{WebView.ActualWidth - 8}; max-height:{WebView.ActualHeight - 8}px; column-count: {columnCount}; column-rule-width: 0px; \"" });
            }

            // TODO: ePub）lr レイアウトのページ送りに対応する

            // TODO: ePub）p（パラグラフ）が無い場合に、画像のみのページと仮定して、xaml側で設定してる余白を非表示に切り替えたい

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
                var sizeItems = JsonConvert.DeserializeObject<int[]>(sizeList).Distinct().Select(x => IsVerticalLayout ? x : x - 8).ToArray();
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
            else
            {
                _innerCurrentPage = Math.Min(PreservedCurrentInnerPageIndex, _innerPageCount - 1);
            }

            await SetScrollPositionAsync();

            TotalInnerPageCount = _innerPageCount;
            CurrentInnerPage = _innerCurrentPage;
        }

        private async void WebView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            using var _ = await _domUpdateLock.LockAsync(default);

            if (!_Domloaded) { return; }

            // リサイズしたら再描画しないとレイアウトが崩れるっぽい
            WebView.Refresh();
        }



        private async Task SetScrollPositionAsync()
        {
            double position = _innerCurrentPage * _onePageScrollSize;

            // Note: vertical-rlでは縦スクロールが横倒しして扱われるので縦書き横書きどちらもXにだけ設定すればOK
            await WebView.InvokeScriptAsync("eval", new[] { $"window.scrollTo({position}, 0);" });

#if DEBUG
            if (IsVerticalLayout)
            {
                Debug.WriteLine(await GetScrollTop());
            }
            else
            {
                Debug.WriteLine(await GetScrollLeft());
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
            var scrollTop = await WebView.InvokeScriptAsync("eval", new[] { "window.pageXOffset.toString()" });
            return double.TryParse(scrollTop, out var value) ? value : 0;
        }


    }
}
