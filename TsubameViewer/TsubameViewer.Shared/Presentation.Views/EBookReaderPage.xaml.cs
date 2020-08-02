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

        string DisableScrollingJs = @"function RemoveScrolling()
                              {
                                  var styleElement = document.createElement('style');
                                  var styleText = 'html, body{ touch-action: none; overflow: hidden; }';
                                  var headElements = document.getElementsByTagName('head');
                                  styleElement.type = 'text/css';
                                  if (headElements.length == 1)
                                  {
                                      headElements[0].appendChild(styleElement);
                                  }
                                  else if (document.head)
                                  {
                                      document.head.appendChild(styleElement);
                                  }
                                  if (styleElement.styleSheet)
                                  {
                                      styleElement.styleSheet.cssText = styleText;
                                  }
                              }; RemoveScrolling();";

        private async void WebView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_Domloaded) { return; }

            // TODO: WebView_SizeChangedで 一旦Opacity=0.0にしてレイアウト崩れを見せないようにする？

            // heightを指定しないと overflow: hidden; が機能しない
            // width: 98vwとすることで表示領域の98%に幅を限定する
            // column-countは表示領域に対して分割数の上限。
            await WebView.InvokeScriptAsync("eval", new[] { $"document.body.style = \"width: 98vw; overflow: hidden; max-height: {WebView.ActualHeight}; margin-top: 1rem; column-count: 2; margin-bottom: 1rem;column-gap: 2.5rem; \"" });

            var height = await GetScrollHeight();
            _webViewInsideHeight = height;
            _innerPageCount = Math.Max((int)Math.Floor(_webViewInsideHeight / WebView.ActualHeight), 1);
            _onePageScrollHeight = Math.Floor(_webViewInsideHeight / (double)_innerPageCount);


            Debug.WriteLine($"WebViewHeight: {_webViewInsideHeight}, pageCount: {_innerPageCount}, onePageScrollHeight: {_onePageScrollHeight}");
            // WebViewリサイズ後に表示スクロール位置を再設定
            SetScrollPosition(_innerCurrentPage * _onePageScrollHeight);
        }

        private async void WebView_DOMContentLoaded(WebView sender, WebViewDOMContentLoadedEventArgs args)
        {
            _Domloaded = true;
            await WebView.InvokeScriptAsync("eval", new[] { $"document.body.style = \"width: 98vw; overflow: hidden; max-height: {WebView.ActualHeight}; margin-top: 1rem; column-count: 2; margin-bottom: 1rem;column-gap: 2.5rem; \"" });
            await WebView.InvokeScriptAsync("eval", new[] { DisableScrollingJs });

            var height = await GetScrollHeight();
            _webViewInsideHeight = height;
            _innerPageCount = Math.Max((int)Math.Floor(_webViewInsideHeight / WebView.ActualHeight), 1);
            _onePageScrollHeight = Math.Floor(_webViewInsideHeight / (double)_innerPageCount);

            Debug.WriteLine($"WebViewHeight: {_webViewInsideHeight}, pageCount: {_innerPageCount}, onePageScrollHeight: {_onePageScrollHeight}");
            if (_DomLoadedTaskCompletioinSource != null)
            {
                _DomLoadedTaskCompletioinSource.SetResult(0);
            }
            else
            {
                // 表示位置の初期設定が必要ならここで行なう
                // SetScrollPosition(_innerCurrentPage * _onePageScrollHeight);
            }
        }


        private async Task<int> GetScrollHeight()
        {
            var heightText = _nowVerticalLayout
                ? await WebView.InvokeScriptAsync("eval", new[] { "document.body.scrollWidth.toString()" })
                : await WebView.InvokeScriptAsync("eval", new[] { "document.body.scrollHeight.toString()" })
                ;
            return int.TryParse(heightText, out var height) ? height : 0;
        }

        bool _Domloaded;


        private async void SetScrollPosition(double positionTop)
        {
            _ = _nowVerticalLayout
                ? await WebView.InvokeScriptAsync("eval", new[] { $"document.body.scrollLeft = {(int)positionTop}" })
                : await WebView.InvokeScriptAsync("eval", new[] { $"document.body.scrollTop = {(int)positionTop}" })
                ;
        }

        bool _nowVerticalLayout = true;
        int _innerCurrentPage;
        int _innerPageCount;
        int _webViewInsideHeight;
        double _onePageScrollHeight;
        TaskCompletionSource<int> _DomLoadedTaskCompletioinSource;
        private DelegateCommand _InnerGoNextImageCommand;
        public DelegateCommand InnerGoNextImageCommand =>
            _InnerGoNextImageCommand ?? (_InnerGoNextImageCommand = new DelegateCommand(ExecuteGoNextCommand));

        async void ExecuteGoNextCommand()
        {
            if (_innerCurrentPage + 1 < _innerPageCount)
            {
                _innerCurrentPage++;
                Debug.WriteLine($"InnerPage: {_innerCurrentPage}/{_innerPageCount}");
                SetScrollPosition(_innerCurrentPage * _onePageScrollHeight);
            }
            else
            {
                var pageVM = DataContext as EBookReaderPageViewModel;
                if (pageVM.GoNextImageCommand.CanExecute())
                {
                    _innerCurrentPage = 0;
                    using (var cts = new CancellationTokenSource(3000))
                    {
                        _DomLoadedTaskCompletioinSource = new TaskCompletionSource<int>();
                        pageVM.GoNextImageCommand.Execute();

                        await _DomLoadedTaskCompletioinSource.Task;

                        // 現在ページの設定
                        _innerCurrentPage = 0;
                        // 次ページの頭が表示できればいいので特に何もしない
                    }
                }
            }
        }

        private DelegateCommand _InnerGoPrevImageCommand;
        public DelegateCommand InnerGoPrevImageCommand =>
            _InnerGoPrevImageCommand ?? (_InnerGoPrevImageCommand = new DelegateCommand(ExecuteGoPrevCommand));

        async void ExecuteGoPrevCommand()
        {
            if (_innerCurrentPage > 0)
            {
                _innerCurrentPage--;
                Debug.WriteLine($"InnerPage: {_innerCurrentPage}/{_innerPageCount}");
                SetScrollPosition(_innerCurrentPage * _onePageScrollHeight);
            }
            else
            {
                var pageVM = DataContext as EBookReaderPageViewModel;
                if (pageVM.GoPrevImageCommand.CanExecute())
                {
                    _innerCurrentPage = 0;
                    using (var cts = new CancellationTokenSource(3000))
                    {
                        _DomLoadedTaskCompletioinSource = new TaskCompletionSource<int>();
                        pageVM.GoPrevImageCommand.Execute();

                        await _DomLoadedTaskCompletioinSource.Task;

                        // 現在ページの設定
                        _innerCurrentPage = _innerPageCount - 1;
                        // 前ページの最後尾ページにスクロールする
                        SetScrollPosition(_innerCurrentPage * _onePageScrollHeight);
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
