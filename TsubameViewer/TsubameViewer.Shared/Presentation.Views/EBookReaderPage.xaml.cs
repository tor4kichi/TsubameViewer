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
        public EBookReaderPage()
        {
            this.InitializeComponent();

            WebView.WebResourceRequested += WebView_WebResourceRequested;
            
            WebView.FrameNavigationStarting += WebView_FrameNavigationStarting;
            WebView.DOMContentLoaded += WebView_DOMContentLoaded;
            WebView.SizeChanged += WebView_SizeChanged;
        }

        private void WebView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_Domloaded) { return; }

            WebView.Opacity = 0.0;
            RefreshPage();

            // TODO: リサイズ後のページ位置の再計算
            // リサイズ前の表示ページ位置の総ページ数との割合から近似ページ数を割り出して適用する？

            /*
            // TODO: WebView_SizeChangedで 一旦Opacity=0.0にしてレイアウト崩れを見せないようにする？

            await WebView.InvokeScriptAsync("eval", new[] { MakeBodyStyle() });

            var scroolableHeight = await GetScrollableHeight();
            var pageHeight = await GetPageHeight();
            ResetHeightCulc(scroolableHeight, pageHeight);

            // WebViewリサイズ後に表示スクロール位置を再設定
            SetScrollPosition();
            */
        }

        private async void WebView_DOMContentLoaded(WebView sender, WebViewDOMContentLoadedEventArgs args)
        {
            _Domloaded = true;
            await WebView.InvokeScriptAsync("eval", new[] { MakeBodyStyle() });

            var scroolableHeight = await GetScrollableHeight();
            var pageHeight = await GetPageHeight();
            ResetHeightCulc(scroolableHeight, pageHeight);

            if (_nowGoPrevLoading)
            {
                _nowGoPrevLoading = false;
                _innerCurrentPage = _innerPageCount - 1;
                SetScrollPosition();
            }
            WebView.Opacity = 1.0;

        }

        const int MarginHeight = 24; 
        const int GapHeight = 48;
        
        string MakeBodyStyle()
        {          
            // heightを指定しないと overflow: hidden; が機能しない
            // width: 98vwとすることで表示領域の98%に幅を限定する
            // column-countは表示領域に対して分割数の上限
            // column-rule-widthはデフォルトでmidiumのため高さ計算のために0pxにする
            // TODO: body要素ではmarginやpaddingが機能しない
            // TODO: column-countが2以上の時、分割数がページ数で割り切れない場合にページ終端のスクロールが詰まる
            return $"document.body.style = \"width: 98vw; overflow: hidden; max-height: {WebView.ActualHeight}px; width: {WebView.ActualWidth}px; column-count: 1; column-gap: {GapHeight}px; column-rule-width: 0px; margin-top: {MarginHeight}px; margin-bottom: {MarginHeight}px;\"";
//            return $"document.body.style = \"width: 98vw; overflow: hidden; max-height: {WebView.ActualHeight}; column-count: 1;\"";

        }

        void ResetHeightCulc(double webViewHeight, double pageHeight)
        {
            _webViewInsideHeight = (int)webViewHeight;
            var div = _webViewInsideHeight / (pageHeight);
            var mod = _webViewInsideHeight % (int)pageHeight;
            _innerPageCount = Math.Max((int)(mod == 0 ? div : div + 1), 1);
            _onePageScrollHeight = pageHeight;


            Debug.WriteLine($"WebViewHeight: {_webViewInsideHeight}, pageCount: {_innerPageCount}, onePageScrollHeight: {_onePageScrollHeight}");
        }



        private async Task<double> GetScrollableHeight()
        {
            var heightText = await WebView.InvokeScriptAsync("eval", new[] { "Math.max(document.body.scrollHeight, document.documentElement.scrollHeight,document.body.offsetHeight, document.documentElement.offsetHeight,document.body.clientHeight, document.documentElement.clientHeight).toString();" });
            return double.TryParse(heightText, out var height) ? height : 0;
        }

        private async Task<double> GetPageHeight()
        {
            var heightText = await WebView.InvokeScriptAsync("eval", new[] { "window.innerHeight.toString()" });
            return double.TryParse(heightText, out var height) ? height : 0;
        }

        private async Task<double> GetScrollTop()
        {
            var heightText = await WebView.InvokeScriptAsync("eval", new[] { "window.pageXOffset.toString()" });
            return double.TryParse(heightText, out var height) ? height : 0;
        }

        bool _Domloaded;


        private async void SetScrollPosition()
        {
            double positionTop = 0.0;
            if (_innerCurrentPage == 0)
            {
                positionTop = 0;
            }
            else
            {
                positionTop = _innerCurrentPage * _onePageScrollHeight;
            }
            
            _ = _nowVerticalLayout
//                ? await WebView.InvokeScriptAsync("eval", new[] { $"document.body.scrollLeft = {(int)positionTop}" })
                ? await WebView.InvokeScriptAsync("eval", new[] { $"window.scrollTo({positionTop}, 0);" })
                : await WebView.InvokeScriptAsync("eval", new[] { $"document.body.scrollTop = {positionTop}" })
                ;

            var top = await GetScrollTop();
            Debug.WriteLine(top);
        }

        bool _nowGoPrevLoading = false;

        bool _nowVerticalLayout = true;
        int _innerCurrentPage;
        int _innerPageCount;
        int _webViewInsideHeight;
        double _onePageScrollHeight;
        private DelegateCommand _InnerGoNextImageCommand;
        public DelegateCommand InnerGoNextImageCommand =>
            _InnerGoNextImageCommand ?? (_InnerGoNextImageCommand = new DelegateCommand(ExecuteGoNextCommand));

        void ExecuteGoNextCommand()
        {
            if (_innerCurrentPage + 1 < _innerPageCount)
            {
                _innerCurrentPage++;
                Debug.WriteLine($"InnerPage: {_innerCurrentPage}/{_innerPageCount}");
                SetScrollPosition();
            }
            else
            {
                var pageVM = DataContext as EBookReaderPageViewModel;
                if (pageVM.GoNextImageCommand.CanExecute())
                {
                    _innerCurrentPage = 0;
                    using (var cts = new CancellationTokenSource(3000))
                    {
                        pageVM.GoNextImageCommand.Execute();
                    }
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
                Debug.WriteLine($"InnerPage: {_innerCurrentPage}/{_innerPageCount}");
                SetScrollPosition();
            }
            else
            {
                var pageVM = DataContext as EBookReaderPageViewModel;
                if (pageVM.GoPrevImageCommand.CanExecute())
                {
                    _innerCurrentPage = 0;
                    using (var cts = new CancellationTokenSource(3000))
                    {
                        _nowGoPrevLoading = true;

                        pageVM.GoPrevImageCommand.Execute();
                    }
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
