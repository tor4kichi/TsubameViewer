using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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
                                  var styleText = 'body, html { overflow: hidden; }'
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

        private void WebView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _ = WebView.InvokeScriptAsync("eval", new[] { $"document.body.style = \"width: 98vw; margin-top: 1rem; column-count: 2; margin-bottom: 1rem;column-gap: 2.5rem; \"" });
        }

        private async void WebView_DOMContentLoaded(WebView sender, WebViewDOMContentLoadedEventArgs args)
        {

            await WebView.InvokeScriptAsync("eval", new[] { $"document.body.style = \"width: 98vw; margin-top: 1rem; column-count: 2; margin-bottom: 1rem;column-gap: 2.5rem; \"" });
            await WebView.InvokeScriptAsync("eval", new[] { DisableScrollingJs });
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
