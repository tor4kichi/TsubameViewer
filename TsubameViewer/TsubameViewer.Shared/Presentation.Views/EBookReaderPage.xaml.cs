using Microsoft.Toolkit.Uwp.UI.Animations;
using Newtonsoft.Json;
using Prism.Commands;
using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Presentation.ViewModels;
using Uno.Threading;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace TsubameViewer.Presentation.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class EBookReaderPage : Page
    {


        public bool IsVerticalLayout
        {
            get { return (bool)GetValue(IsVerticalLayoutProperty); }
            set { SetValue(IsVerticalLayoutProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsVerticalLayout.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsVerticalLayoutProperty =
            DependencyProperty.Register("IsVerticalLayout", typeof(bool), typeof(EBookReaderPage), new PropertyMetadata(false));

        
        public bool WithSeparetePage
        {
            get { return (bool)GetValue(WithSeparetePageProperty); }
            set { SetValue(WithSeparetePageProperty, value); }
        }

        // Using a DependencyProperty as the backing store for WithSeparetePage.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty WithSeparetePageProperty =
            DependencyProperty.Register("WithSeparetePage", typeof(bool), typeof(EBookReaderPage), new PropertyMetadata(false));




        public bool NowOnlyImageView
        {
            get { return (bool)GetValue(NowOnlyImageViewProperty); }
            set { SetValue(NowOnlyImageViewProperty, value); }
        }

        // Using a DependencyProperty as the backing store for NowOnlyImageView.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty NowOnlyImageViewProperty =
            DependencyProperty.Register("NowOnlyImageView", typeof(bool), typeof(EBookReaderPage), new PropertyMetadata(false));






        FastAsyncLock _domUpdateLock = new FastAsyncLock();


        public EBookReaderPage()
        {
            this.InitializeComponent();

            WebView.WebResourceRequested += WebView_WebResourceRequested;
            
            WebView.FrameNavigationStarting += WebView_FrameNavigationStarting;
            WebView.DOMContentLoaded += WebView_DOMContentLoaded;
            WebView.SizeChanged += WebView_SizeChanged;

            Loaded += MoveButtonEnablingWorkAround_EBookReaderPage_Loaded;

            WebView.Opacity = 0.0;
#if DEBUG
            FontSizeSettingComboBox.Visibility = Visibility.Visible;
#endif
        }

        private void MoveButtonEnablingWorkAround_EBookReaderPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Note: WebViewにフォーカスがあるとWebViewより前面にあるボタンが押せないバグのワークアラウンド
            this.LeftPageMoveButton.Focus(FocusState.Programmatic);
        }



        private async void WebView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            using var _ = await _domUpdateLock.LockAsync(default);

            if (!_Domloaded) { return; }

            WebView.Fade(0, 75);

            // リサイズしたら再描画しないとレイアウトが崩れるっぽい
            WebView.Refresh();
        }

        bool _Domloaded;
        bool isFirstLoaded = true;

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

            if (isFirstLoaded)
            {
                isFirstLoaded = false;

                _innerCurrentPage = Math.Min((DataContext as EBookReaderPageViewModel).InnerCurrentImageIndex, _innerPageCount - 1);

                await SetScrollPositionAsync();

                WebView.Fade(1.0f, 150).Start();
            }
            else if (_nowGoPrevLoading)
            {
                _nowGoPrevLoading = false;

                _innerCurrentPage = _innerPageCount - 1;

                await SetScrollPositionAsync();

                WebView.Fade(1.0f, 75).Start();
            }
            else
            {
                WebView.Fade(1.0f, 75).Start();
            }

            (DataContext as EBookReaderPageViewModel).InnerImageTotalCount = _innerPageCount;
            (DataContext as EBookReaderPageViewModel).InnerCurrentImageIndex = _innerCurrentPage;
        }


        void ResetSizeCulc(double webViewScrollableSize, double pageSize)
        {
            _webViewScrollableSize = (int)webViewScrollableSize;
            var div = _webViewScrollableSize / (pageSize);
            var mod = _webViewScrollableSize % pageSize;
            _innerPageCount = Math.Max((int)(mod == 0 ? div : div + 1), 1);
            _onePageScrollSize = pageSize;

            Debug.WriteLine($"WebViewSize: {_webViewScrollableSize}, pageCount: {_innerPageCount}, onePageScrollSize: {_onePageScrollSize}");
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

        bool _nowGoPrevLoading = false;

        int _innerCurrentPage;
        int _innerPageCount;
        int _webViewScrollableSize;
        double _onePageScrollSize;
        private DelegateCommand _InnerGoNextImageCommand;
        public DelegateCommand InnerGoNextImageCommand =>
            _InnerGoNextImageCommand ?? (_InnerGoNextImageCommand = new DelegateCommand(ExecuteGoNextCommand));

        void ExecuteGoNextCommand()
        {
            if (_innerCurrentPage + 1 < _innerPageCount)
            {
                _innerCurrentPage++;
                (DataContext as EBookReaderPageViewModel).InnerCurrentImageIndex = _innerCurrentPage;
                Debug.WriteLine($"InnerPage: {_innerCurrentPage}/{_innerPageCount}");
                _ = SetScrollPositionAsync();
            }
            else
            {
                var pageVM = DataContext as EBookReaderPageViewModel;
                if (pageVM.GoNextImageCommand.CanExecute())
                {
                    WebView.Fade(0, 50).Start();

                    _innerCurrentPage = 0;
                    (DataContext as EBookReaderPageViewModel).InnerCurrentImageIndex = _innerCurrentPage;
                    pageVM.GoNextImageCommand.Execute();
                }
            }
        }

        private DelegateCommand _InnerGoPrevImageCommand;
        public DelegateCommand InnerGoPrevImageCommand =>
            _InnerGoPrevImageCommand ?? (_InnerGoPrevImageCommand = new DelegateCommand(ExecuteGoPrevCommand));

        void ExecuteGoPrevCommand()
        {
            if (_innerCurrentPage > 0)
            {
                _innerCurrentPage--;
                (DataContext as EBookReaderPageViewModel).InnerCurrentImageIndex = _innerCurrentPage;
                Debug.WriteLine($"InnerPage: {_innerCurrentPage}/{_innerPageCount}");
                _ = SetScrollPositionAsync();
            }
            else
            {
                var pageVM = DataContext as EBookReaderPageViewModel;
                if (pageVM.GoPrevImageCommand.CanExecute())
                {
                    WebView.Fade(0, 50).Start();

                    _innerCurrentPage = 0;
                    (DataContext as EBookReaderPageViewModel).InnerCurrentImageIndex = _innerCurrentPage;
                    _nowGoPrevLoading = true;
                    pageVM.GoPrevImageCommand.Execute();
                }
            }

        }




        private void WebView_FrameNavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
        {
            sender.AddWebAllowedObject("myContext", DataContext);
        }

        public void RefreshPage()
        {
            var pageVM = DataContext as EBookReaderPageViewModel;
            WebView.NavigateToString(pageVM.PageHtml);
        }

        private void WebView_WebResourceRequested(WebView sender, WebViewWebResourceRequestedEventArgs args)
        {
            var defferal = args.GetDeferral();
            try
            {
                var pageVM = DataContext as EBookReaderPageViewModel;
                args.Response = pageVM.ResolveWebResourceRequest(args.Request.RequestUri);
            }
            finally
            {
                defferal.Complete();
                defferal.Dispose();
            }
        }
    }
}
