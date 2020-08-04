using Microsoft.Toolkit.Uwp.UI.Animations;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Presentation.ViewModels;
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




        public EBookReaderPage()
        {
            this.InitializeComponent();

            WebView.WebResourceRequested += WebView_WebResourceRequested;
            
            WebView.FrameNavigationStarting += WebView_FrameNavigationStarting;
            WebView.DOMContentLoaded += WebView_DOMContentLoaded;
            WebView.SizeChanged += WebView_SizeChanged;

            Loaded += MoveButtonEnablingWorkAround_EBookReaderPage_Loaded;
        }




        private void MoveButtonEnablingWorkAround_EBookReaderPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Note: WebViewにフォーカスがあるとWebViewより前面にあるボタンが押せないバグのワークアラウンド
            this.LeftPageMoveButton.Focus(FocusState.Programmatic);
        }

        private void WebView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_Domloaded) { return; }

            WebView.Opacity = 0.0;

            // リサイズしたら再描画しないとレイアウトが崩れるっぽい
            WebView.Refresh();
        }

        bool _Domloaded;
        bool isFirstLoaded = true;

        private async void WebView_DOMContentLoaded(WebView sender, WebViewDOMContentLoadedEventArgs args)
        {
            _Domloaded = true;

            //
            // 段組みレイアウトについて
            // column-countを1以上に指定してやると単純にそれだけの段に分かれた表示にできるが
            // 例えば縦書きなら横幅をViewPortサイズに限定することで縦長の段組みを描画できる
            // 縦に等間隔に並んだページに対してページ高さと同等のスクロール量をページ毎に設定することで
            // ページ送りを表現している。
            //

            //
            // サイズ計算について
            // まず、bodyの最後尾に位置計算用のマーカー要素を追加して、
            // そのマーカー要素のページ先頭からの相対位置と、ページ全体のスクロール可能な範囲の差から
            // １ページ分のスクロール量を求める。としたいんだけど、
            // Borderか何かの2ピクセルのズレが出ているのでマジックナンバー気味だけど補正して１ページのスクロール量としている。
            // 
            // 参考： https://stackoverflow.com/questions/6989306/how-to-get-css3-multi-column-count-in-javascript

            // 縦書きかをチェック
            var writingModeString = await WebView.InvokeScriptAsync("eval", new[] { @"
                window.getComputedStyle(document.body).getPropertyValue('writing-mode')
                " });
            Debug.WriteLine(writingModeString);

            await WebView.InvokeScriptAsync("eval", new[] { "let markar = document.createElement('div'); markar.id = 'mark_last'; document.body.appendChild(markar);" });

            IsVerticalLayout = writingModeString == "vertical-rl" || writingModeString == "vertical-lr";
            if (IsVerticalLayout)
            {
                // TODO: ePub）column-countが2以上の時、分割数がページ数で割り切れない場合にページ終端のスクロール幅が足りず、前のページが一部入り込んだスクロールになってしまう

                // heightを指定しないと overflow: hidden; が機能しない
                // width: 100vwとすることで表示領域に幅を限定する。段組みをビューポートの高さを越えて縦長に描画させるために必要。
                // column-countは表示領域に対して分割数の上限。段組み描画のために必要。
                // column-rule-widthはデフォルトでmidium。アプリ側での細かい高さ計算の省略ために0pxに指定。
                await WebView.InvokeScriptAsync("eval", new[] { $"document.body.style = \"width: 100vw; overflow: hidden; max-height: {WebView.ActualHeight}px; column-count: 1; column-rule-width: 0px; \"" });

                var markerPositionLeft = double.Parse(await WebView.InvokeScriptAsync("eval", new[] { "document.getElementById('mark_last').offsetTop.toString();" }));
                Debug.WriteLine("markerPositionLeft: " + markerPositionLeft);
                var scrollableSize = await GetScrollableHeight();
                var pageSize = await GetPageHeight();
                var onePageSize = scrollableSize - markerPositionLeft + 16;
                ResetSizeCulc(scrollableSize + 2, markerPositionLeft < pageSize ? scrollableSize + 2 : onePageSize);
            }
            else
            {
                // Note: "width:{WebView.ActualWidth - 8}"の-8は右側の見切れ対策
                await WebView.InvokeScriptAsync("eval", new[] { $"document.body.style = \"overflow: hidden; width:{WebView.ActualWidth - 8}; max-height:{WebView.ActualHeight}px; column-count: 1; column-rule-width: 0px; \"" });

                var markerPositionLeft = double.Parse(await WebView.InvokeScriptAsync("eval", new[] { "document.getElementById('mark_last').offsetLeft.toString();" }));
                Debug.WriteLine("markerPositionLeft: " + markerPositionLeft);
                var scrollableSize = await GetScrollableWidth();
                var pageSize = await GetPageWidth();
                var onePageSize = scrollableSize - markerPositionLeft + 16;
                ResetSizeCulc(scrollableSize + 2, markerPositionLeft < pageSize ? scrollableSize + 2 : onePageSize);
            }

            // TODO: ePub）lr レイアウトのページ送りに対応する

            // TODO: ePub）p（パラグラフ）が無い場合に、画像のみのページと仮定して、xaml側で設定してる余白を非表示に切り替えたい

            if (isFirstLoaded)
            {
                isFirstLoaded = false;

                _innerCurrentPage = Math.Min((DataContext as EBookReaderPageViewModel).InnerCurrentImageIndex, _innerPageCount - 1);
                await SetScrollPositionAsync();
            }

            if (_nowGoPrevLoading)
            {
                _nowGoPrevLoading = false;
                _innerCurrentPage = _innerPageCount - 1;
                (DataContext as EBookReaderPageViewModel).InnerCurrentImageIndex = _innerCurrentPage;

                //await Task.Delay(100);
                await SetScrollPositionAsync();
            }

            WebView.Opacity = 1.0;
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
