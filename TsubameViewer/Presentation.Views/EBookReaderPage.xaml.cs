using Microsoft.Toolkit.Uwp.UI.Animations;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using TsubameViewer.Presentation.ViewModels;
using TsubameViewer.Presentation.Views.EBookControls;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Web.Http;
using Xamarin.Essentials;
using Windows.UI.Xaml.Media.Animation;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using Microsoft.Toolkit.Mvvm.Messaging;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Input;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace TsubameViewer.Presentation.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class EBookReaderPage : Page
    {

        private readonly EBookReaderPageViewModel _vm;
        private readonly IMessenger _messenger;

        private readonly Models.Infrastructure.AsyncLock _movePageLock = new();

        private readonly RelayCommand _InnerGoNextImageCommand;
        private readonly RelayCommand _InnerGoPrevImageCommand;

        private readonly RelayCommand _OpenTocPaneCommand;


        public EBookReaderPage()
        {
            this.InitializeComponent();
            
            DataContext = _vm = Ioc.Default.GetService<EBookReaderPageViewModel>();
            _messenger = Ioc.Default.GetService<IMessenger>();

#if DEBUG
            DebugPanel.Visibility = Visibility.Visible;
#endif
            Loaded += MoveButtonEnablingWorkAround_EBookReaderPage_Loaded;

            Loaded += ResetAnimationUIContainer_Loaded1;
            Unloaded += TapAndController_Unloaded;

            EPubRenderer.ContentRefreshStarting += WebView_ContentRefreshStarting;
            EPubRenderer.ContentRefreshComplete += WebView_ContentRefreshComplete;

            EPubRenderer.Loaded += WebView_Loaded;
            EPubRenderer.Unloaded += WebView_Unloaded;

            EPubRenderer.WebResourceRequested += WebView_WebResourceRequested;

            this.Loaded += PageNavigationCommandInitialize_Loaded;
            this.Unloaded += PageNavigationCommandDispose_Unloaded;

            _InnerGoPrevImageCommand = new RelayCommand(() =>
            {
                if (_vm.EBookReaderSettings.IsReversePageFliping_Button)
                {
                    ExecuteGoNextCommand();
                }
                else
                {
                    ExecuteGoPrevCommand();
                }
            });
            _InnerGoNextImageCommand = new RelayCommand(() =>
            {
                if (_vm.EBookReaderSettings.IsReversePageFliping_Button)
                {
                    ExecuteGoPrevCommand();
                }
                else
                {
                    ExecuteGoNextCommand();
                }
            });

            _OpenTocPaneCommand = new RelayCommand(ExecuteOpenTocPaneCommand);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            EPubRenderer.Visibility = Visibility.Collapsed;
            
            _messenger.Unregister<BackNavigationRequestingMessage>(this);

            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = false;
            Window.Current.SetTitleBar(null);
            Windows.UI.Core.SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = Windows.UI.Core.AppViewBackButtonVisibility.Visible;

            var appView = ApplicationView.GetForCurrentView();
            appView.TitleBar.ButtonBackgroundColor = null;
            appView.TitleBar.ButtonHoverBackgroundColor = null;
            appView.TitleBar.ButtonInactiveBackgroundColor = null;
            appView.TitleBar.ButtonPressedBackgroundColor = null;

            appView.ExitFullScreenMode();            

            base.OnNavigatingFrom(e);
        }



        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // https://docs.microsoft.com/ja-jp/windows/uwp/design/shell/title-bar
            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;

            if ((bool)App.Current.Resources["DebugTVMode"])
            {
                Window.Current.SetTitleBar(DraggableTitleBarArea_TVorTouch);
            }
            else
            {
                Window.Current.SetTitleBar(DraggableTitleBarArea_Desktop);
            }

            AnimationBuilder.Create()
                .Translation(Axis.X, -320, duration: TimeSpan.FromMilliseconds(1))
                .Start(TocContentPanel);

            Windows.UI.Core.SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = Windows.UI.Core.AppViewBackButtonVisibility.Collapsed;

            var appView = ApplicationView.GetForCurrentView();
            appView.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            appView.TitleBar.ButtonHoverBackgroundColor = Color.FromArgb(0x7f, 0xff, 0xff, 0xff);
            appView.TitleBar.ButtonInactiveBackgroundColor = Color.FromArgb(0xcf, 0xff, 0xff, 0xff);
            appView.TitleBar.ButtonPressedBackgroundColor = Color.FromArgb(0x9f, 0xff, 0xff, 0xff);

            _messenger.Register<BackNavigationRequestingMessage>(this, (r, m) => 
            {
                if (TocContainer.Visibility == Visibility.Visible)
                {
                    m.Value.IsHandled = true;
                    CloseTocPaneCommand.Execute(null);
                }
            });

            ConnectedAnimation animation = ConnectedAnimationService.GetForCurrentView().GetAnimation(PageTransitionHelper.ImageJumpConnectedAnimationName);
            if (animation != null)
            {
                animation.Cancel();                
            }

            EPubRenderer.Visibility = Visibility.Visible;

            base.OnNavigatedTo(e);
        }

        private void WebView_WebResourceRequested(object sender, WebViewWebResourceRequestedEventArgs e)
        {
            var reqesutUri = e.Request.RequestUri;

            using (var defferral = e.GetDeferral())
            {
                try
                {
                    var stream = _vm.ResolveWebResourceRequest(reqesutUri);
                    if (stream != null)
                    {
                        e.Response = new Windows.Web.Http.HttpResponseMessage(statusCode: Windows.Web.Http.HttpStatusCode.Ok);
                        e.Response.Content = new HttpStreamContent(stream.AsInputStream());
                    }
                }
                finally
                {
                    defferral.Complete();
                }
            }
        }


        //
        // Note: ePubRenderer内のページ遷移を含めたページ移動コマンドの左右入れ替え実装について
        // VisualStateManagerで切り替えたかったが、null参照エラーが出て動かないため
        // コードビハインドで切り替える形にした。
        // デバッグあり実行だと動くが、デバッグ無し実行だと動かなかった。（リリースビルドでも同様）
        //

        private void PageNavigationCommandDispose_Unloaded(object sender, RoutedEventArgs e)
        {
            _disposables.Dispose();
        }

        private void PageNavigationCommandInitialize_Loaded(object sender, RoutedEventArgs e)
        {
            _disposables = new CompositeDisposable();
        }

        CompositeDisposable _disposables;

        #region Bottom UI Menu


        private void TapAndController_Unloaded(object sender, RoutedEventArgs e)
        {
        }

        private void ResetAnimationUIContainer_Loaded1(object sender, RoutedEventArgs e)
        {
            SwipeProcessScreen.Tapped -= SwipeProcessScreen_Tapped;
            SwipeProcessScreen.Tapped += SwipeProcessScreen_Tapped;
            SwipeProcessScreen.ManipulationMode = ManipulationModes.TranslateY | ManipulationModes.TranslateX;
            SwipeProcessScreen.ManipulationStarting -= SwipeProcessScreen_ManipulationStarting;
            SwipeProcessScreen.ManipulationStarted -= SwipeProcessScreen_ManipulationStarted;
            SwipeProcessScreen.ManipulationCompleted -= SwipeProcessScreen_ManipulationCompleted;
            SwipeProcessScreen.ManipulationStarting += SwipeProcessScreen_ManipulationStarting;
            SwipeProcessScreen.ManipulationStarted += SwipeProcessScreen_ManipulationStarted;
            SwipeProcessScreen.ManipulationCompleted += SwipeProcessScreen_ManipulationCompleted;
        }

        private void SwipeProcessScreen_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var pt = e.GetPosition(RootGrid);

            if (isOnceSkipTapped)
            {
                //var bottomUIItems = VisualTreeHelper.FindElementsInHostCoordinates(pt, AnimationUICommandBar);
                //if (bottomUIItems.Any()) { return; }

                isOnceSkipTapped = false;
                e.Handled = true;
                return;
            }

            var uiItems = VisualTreeHelper.FindElementsInHostCoordinates(pt, UIContainer);
            foreach (var item in uiItems)
            {
                if (item == RightPageMoveButton)
                {
                    if (RightPageMoveButton.Command?.CanExecute(null) ?? false)
                    {
                        RightPageMoveButton.Command.Execute(null);
                    }
                }
                else if (item == LeftPageMoveButton)
                {
                    if (LeftPageMoveButton.Command?.CanExecute(null) ?? false)
                    {
                        LeftPageMoveButton.Command.Execute(null);
                    }
                }
            }
        }

        private void SwipeProcessScreen_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            Debug.WriteLine(e.Cumulative.Translation.Y);
        }


        bool isOnceSkipTapped = false;
        private void SwipeProcessScreen_ManipulationStarting(object sender, ManipulationStartingRoutedEventArgs e)
        {
            if (AnimationUIContainer.Opacity == 1.0)
            {
                e.Handled = true;
                isOnceSkipTapped = true;
                return;
            }
        }


        private void SwipeProcessScreen_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            if (e.Cumulative.Translation.X > 1
                || e.Velocities.Linear.X > 0.01
                )
            {
                // 右スワイプ
                LeftPageMoveButton.Command.Execute(null);
            }
            else if (e.Cumulative.Translation.X < -1
                || e.Velocities.Linear.X < -0.01
                )
            {
                // 左スワイプ
                RightPageMoveButton.Command.Execute(null);
            }
            else
            {
                e.Handled = true;
            }
        }

        #endregion



        private void MoveButtonEnablingWorkAround_EBookReaderPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Note: WebViewにフォーカスがあるとWebViewより前面にあるボタンが押せないバグのワークアラウンド
            this.LeftPageMoveButton.Focus(FocusState.Programmatic);
        }





        CompositeDisposable _RendererObserveDisposer;

        private void WebView_Loaded(object sender, RoutedEventArgs e)
        {
            _RendererObserveDisposer = new CompositeDisposable();

            EPubRenderer.ObserveDependencyProperty(EPubRenderer.CurrentInnerPageProperty)
                .Subscribe(_ =>
                {
                    _vm.InnerCurrentImageIndex = EPubRenderer.CurrentInnerPage;
                })
                .AddTo(_RendererObserveDisposer);

            EPubRenderer.ObserveDependencyProperty(EPubRenderer.TotalInnerPageCountProperty)
                .Subscribe(_ =>
                {
                    _vm.InnerImageTotalCount = EPubRenderer.TotalInnerPageCount;
                })
                .AddTo(_RendererObserveDisposer);

            NowEnablePageMove = false;
        }

        private void WebView_Unloaded(object sender, RoutedEventArgs e)
        {
            _RendererObserveDisposer.Dispose();            
        }




        public bool NowEnablePageMove
        {
            get { return (bool)GetValue(NowEnablePageMoveProperty); }
            set { SetValue(NowEnablePageMoveProperty, value); }
        }

        // Using a DependencyProperty as the backing store for NowRefreshingEPubRenderer.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty NowEnablePageMoveProperty =
            DependencyProperty.Register("NowEnablePageMove", typeof(bool), typeof(EBookReaderPage), new PropertyMetadata(true));

        private readonly AnimationBuilder _ShowAnimationAb = AnimationBuilder.Create()
            .Opacity(1.0, duration: TimeSpan.FromMilliseconds(75));

        private readonly AnimationBuilder _HideAnimationAb = AnimationBuilder.Create()
            .Opacity(0.0, duration: TimeSpan.FromMilliseconds(75));

        private void WebView_ContentRefreshStarting(object sender, EventArgs e)
        {            
            NowEnablePageMove = false;
        }

        private void WebView_ContentRefreshComplete(object sender, EventArgs e)
        {
            NowEnablePageMove = true;

            if (string.IsNullOrEmpty(_vm.PageHtml) is false)
            {
                _vm.CompletePageLoading();
                _ShowAnimationAb
                    .Start(EPubRenderer);
            }
        }

        async void ExecuteGoNextCommand()
        {
            if (NowEnablePageMove is false) { return; }

            using (await _movePageLock.LockAsync(default))
            {
                if (EPubRenderer.CanGoNext())
                {
                    EPubRenderer.GoNext();
                }
                else
                {
                    if (_vm.CanGoNext())
                    {
                        await _HideAnimationAb
                            .StartAsync(EPubRenderer);

                        EPubRenderer.PrepareGoNext();
                        await _vm.GoNextImageAsync();
                    }
                }
            }
        }

        async void ExecuteGoPrevCommand()
        {
            if (NowEnablePageMove is false) { return; }

            using (await _movePageLock.LockAsync(default))
            {
                if (EPubRenderer.CanGoPreview())
                {
                    EPubRenderer.GoPreview();
                }
                else
                {
                    if (_vm.CanGoPrev())
                    {
                        await _HideAnimationAb
                            .StartAsync(EPubRenderer);

                        EPubRenderer.PrepareGoPreview();
                        await _vm.GoPrevImageAsync();
                    }
                }
            }
        }

        async void ExecuteOpenTocPaneCommand()
        {
            if (NowEnablePageMove is false) { return; }

            TocContainer.Visibility = Visibility.Visible;
            await Task.Delay(250);
            if (TocItemsListView.SelectedItem != null)
            {
                var container = TocItemsListView.ContainerFromItem(TocItemsListView.SelectedItem);
                if (container is SelectorItem control)
                {
                    control.Focus(FocusState.Keyboard);
                }
            }
        }


        private RelayCommand _CloseTocPaneCommand;
        public RelayCommand CloseTocPaneCommand =>
            _CloseTocPaneCommand ?? (_CloseTocPaneCommand = new RelayCommand(ExecuteCloseTocPaneCommand));

        void ExecuteCloseTocPaneCommand()
        {
            TocContainer.Visibility = Visibility.Collapsed;
        }

        private void CoverImage_Tapped(object sender, TappedRoutedEventArgs e)
        {
            _vm.CurrentImageIndex = 0;
        }

        public void RefreshPage()
        {
            EPubRenderer.Refresh();
        }

        private void BackgroundColorPickerFlyout_Opening(object sender, object e)
        {
            var color = _vm.EBookReaderSettings.BackgroundColor;
            if (color.A == 0)
            {
                color.A = 0xff;
                _vm.EBookReaderSettings.BackgroundColor = color;
            }
        }

        private void ForegroundColorPickerFlyout_Opening(object sender, object e)
        {
            var color = _vm.EBookReaderSettings.ForegroundColor;
            if (color.A == 0)
            {
                _vm.EBookReaderSettings.ForegroundColor = new Color() { A = 0xff } ;
            }
        }
    }
}
