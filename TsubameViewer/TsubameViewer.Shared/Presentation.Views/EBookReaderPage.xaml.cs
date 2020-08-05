using Microsoft.Toolkit.Uwp.UI.Animations;
using Newtonsoft.Json;
using Prism.Commands;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Presentation.ViewModels;
using TsubameViewer.Presentation.Views.EBookControls;
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
        public EBookReaderPage()
        {
            this.InitializeComponent();
            
            Loaded += MoveButtonEnablingWorkAround_EBookReaderPage_Loaded;

#if DEBUG
            FontSizeSettingComboBox.Visibility = Visibility.Visible;
#endif



            WebView.ContentRefreshStarting += WebView_ContentRefreshStarting;
            WebView.ContentRefreshComplete += WebView_ContentRefreshComplete;

            WebView.Opacity = 0.0;

            WebView.Loaded += WebView_Loaded;
            WebView.Unloaded += WebView_Unloaded;
        }




        private void MoveButtonEnablingWorkAround_EBookReaderPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Note: WebViewにフォーカスがあるとWebViewより前面にあるボタンが押せないバグのワークアラウンド
            this.LeftPageMoveButton.Focus(FocusState.Programmatic);
        }





        CompositeDisposable _RendererObserveDisposer;

        private void WebView_Loaded(object sender, RoutedEventArgs e)
        {
            _RendererObserveDisposer = new CompositeDisposable();

            WebView.ObserveDependencyProperty(EPubRenderer.CurrentInnerPageProperty)
                .Subscribe(_ =>
                {
                    (DataContext as EBookReaderPageViewModel).InnerCurrentImageIndex = WebView.CurrentInnerPage;
                })
                .AddTo(_RendererObserveDisposer);

            WebView.ObserveDependencyProperty(EPubRenderer.TotalInnerPageCountProperty)
                .Subscribe(_ =>
                {
                    (DataContext as EBookReaderPageViewModel).InnerImageTotalCount = WebView.TotalInnerPageCount;
                })
                .AddTo(_RendererObserveDisposer);
        }

        private void WebView_Unloaded(object sender, RoutedEventArgs e)
        {
            _RendererObserveDisposer.Dispose();
        }


        private void WebView_ContentRefreshStarting(object sender, EventArgs e)
        {
            WebView.Opacity = 0.0;
        }

        private void WebView_ContentRefreshComplete(object sender, EventArgs e)
        {
            WebView.Fade(1.0f, 75).Start();
        }

        private DelegateCommand _InnerGoNextImageCommand;
        public DelegateCommand InnerGoNextImageCommand =>
            _InnerGoNextImageCommand ?? (_InnerGoNextImageCommand = new DelegateCommand(ExecuteGoNextCommand));

        void ExecuteGoNextCommand()
        {
            if (WebView.CanGoNext())
            {
                WebView.GoNext();
                
            }
            else
            {
                var pageVM = DataContext as EBookReaderPageViewModel;
                if (pageVM.GoNextImageCommand.CanExecute())
                {
                    WebView.Fade(0, 50).Start();

                    WebView.PreservedCurrentInnerPageIndex = 0;
                    pageVM.GoNextImageCommand.Execute();
                }
            }
        }

        private DelegateCommand _InnerGoPrevImageCommand;
        public DelegateCommand InnerGoPrevImageCommand =>
            _InnerGoPrevImageCommand ?? (_InnerGoPrevImageCommand = new DelegateCommand(ExecuteGoPrevCommand));

        void ExecuteGoPrevCommand()
        {
            if (WebView.CanGoPreview())
            {
                WebView.GoPreview();
            }
            else
            {
                var pageVM = DataContext as EBookReaderPageViewModel;
                if (pageVM.GoPrevImageCommand.CanExecute())
                {
                    WebView.Fade(0, 50).Start();

                    WebView.PreservedCurrentInnerPageIndex = int.MaxValue;
                    pageVM.GoPrevImageCommand.Execute();
                }
            }

        }

        public void RefreshPage()
        {
            WebView.Refresh();
        }
    }
}
