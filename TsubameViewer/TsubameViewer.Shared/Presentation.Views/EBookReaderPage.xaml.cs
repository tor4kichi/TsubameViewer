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

            // 縦書きかをチェック
            var writingModeString = await WebView.InvokeScriptAsync("eval", new[] { @"
                window.getComputedStyle(document.body).getPropertyValue('writing-mode')
                " });
            Debug.WriteLine(writingModeString);

            IsVerticalLayout = writingModeString == "vertical-rl" || writingModeString == "vertical-rl";
            if (IsVerticalLayout)
            {
                await WebView.InvokeScriptAsync("eval", new[] { MakeWritingModeVertialSupportBodyStyle() });

                ResetSizeCulc(await GetScrollableHeight(), await GetPageHeight());
            }
            else
            {
                await WebView.InvokeScriptAsync("eval", new[] { MakeWritingModeHorizontalSupportBodyStyle() });
                
                ResetSizeCulc(await GetScrollableWidth(), await GetPageWidth());
            }      
            
            // TODO: lr レイアウトのページ送りに対応する



            if (isFirstLoaded)
            {
                isFirstLoaded = false;

                _innerCurrentPage = Math.Min((DataContext as EBookReaderPageViewModel).InnerCurrentImageIndex, _innerPageCount - 1);
                SetScrollPosition();
            }

            if (_nowGoPrevLoading)
            {
                _nowGoPrevLoading = false;
                _innerCurrentPage = _innerPageCount - 1;
                (DataContext as EBookReaderPageViewModel).InnerCurrentImageIndex = _innerCurrentPage;
                SetScrollPosition();
            }
            WebView.Opacity = 1.0;
        }

        bool isFirstLoaded = true;

        const int Horizontal_GapWidth = 48;

        string MakeWritingModeHorizontalSupportBodyStyle()
        {
            return $"document.body.style = \"overflow: hidden;  max-height:{WebView.ActualHeight}px; column-count: 1; column-gap: {Horizontal_GapWidth}px; column-rule-width: 0px; margin: 0px {Vertical_MarginHeight}px; \"";
        }


        const int Vertical_MarginHeight = 24; 
        const int Vertical_GapHeight = 48;
        
        string MakeWritingModeVertialSupportBodyStyle()
        {          
            // heightを指定しないと overflow: hidden; が機能しない
            // width: 98vwとすることで表示領域の98%に幅を限定する
            // column-countは表示領域に対して分割数の上限
            // column-rule-widthはデフォルトでmidiumのため高さ計算のために0pxにする
            // TODO: body要素ではmarginやpaddingが機能しない
            // TODO: column-countが2以上の時、分割数がページ数で割り切れない場合にページ終端のスクロールが詰まる
            return $"document.body.style = \"width: 98vw; overflow: hidden; max-height: {WebView.ActualHeight}px; width: {WebView.ActualWidth}px; column-count: 1; column-gap: {Vertical_GapHeight}px; column-rule-width: 0px; margin-top: {Vertical_MarginHeight}px; margin-bottom: {Vertical_MarginHeight}px;\"";
//            return $"document.body.style = \"width: 98vw; overflow: hidden; max-height: {WebView.ActualHeight}; column-count: 1;\"";

        }

        void ResetSizeCulc(double webViewScrollableSize, double pageSize)
        {
            _webViewScrollableSize = (int)webViewScrollableSize;
            var div = _webViewScrollableSize / (pageSize);
            var mod = _webViewScrollableSize % (int)pageSize;
            if (IsVerticalLayout)
            {
                _innerPageCount = Math.Max((int)(mod == 0 ? div : div + 1), 1);
            }
            else
            {
                _innerPageCount = Math.Max((int)(mod == 0 ? div : div + 1), 1);
            }
            _onePageScrollSize = pageSize;

            Debug.WriteLine($"WebViewHeight: {_webViewScrollableSize}, pageCount: {_innerPageCount}, onePageScrollHeight: {_onePageScrollSize}");
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
            var scrollTop = await WebView.InvokeScriptAsync("eval", new[] { "window.pageXOffset.toString()" });
            return double.TryParse(scrollTop, out var value) ? value : 0;
        }

        bool _Domloaded;


        private async void SetScrollPosition()
        {
            double position = 0.0;
            if (_innerCurrentPage == 0)
            {
                position = 0;
            }
            else
            {
                position = _innerCurrentPage * _onePageScrollSize;
            }

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
                SetScrollPosition();
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
                SetScrollPosition();
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
