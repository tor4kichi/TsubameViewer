using Microsoft.Toolkit.Uwp.UI.Animations;
using Prism.Commands;
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
using Uno.Threading;
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
using Prism.Ioc;
using AsyncLock = Uno.Threading.AsyncLock;
using Windows.UI.Xaml.Media.Animation;

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
            DebugPanel.Visibility = Visibility.Visible;
#endif


            Loaded += ResetAnimationUIContainer_Loaded1;
            Unloaded += TapAndController_Unloaded;



            WebView.ContentRefreshStarting += WebView_ContentRefreshStarting;
            WebView.ContentRefreshComplete += WebView_ContentRefreshComplete;

            WebView.Opacity = 0.0;

            WebView.Loaded += WebView_Loaded;
            WebView.Unloaded += WebView_Unloaded;

            WebView.WebResourceRequested += WebView_WebResourceRequested;

            this.Loaded += PageNavigationCommandInitialize_Loaded;
            this.Unloaded += PageNavigationCommandDispose_Unloaded;

            DataContextChanged += OnDataContextChanged;
        }



        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            var oldViewModel = _vm;
            _vm = args.NewValue as EBookReaderPageViewModel;
            if (_vm != null && oldViewModel != _vm)
            {
                this.Bindings.Update();
            }
        }



        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
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

            PrimaryWindowCoreLayout.IsPreventSystemBackNavigation = false;

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

            Windows.UI.Core.SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = Windows.UI.Core.AppViewBackButtonVisibility.Collapsed;

            var appView = ApplicationView.GetForCurrentView();
            appView.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            appView.TitleBar.ButtonHoverBackgroundColor = Color.FromArgb(0x7f, 0xff, 0xff, 0xff);
            appView.TitleBar.ButtonInactiveBackgroundColor = Color.FromArgb(0xcf, 0xff, 0xff, 0xff);
            appView.TitleBar.ButtonPressedBackgroundColor = Color.FromArgb(0x9f, 0xff, 0xff, 0xff);

            PrimaryWindowCoreLayout.IsPreventSystemBackNavigation = true;

            ConnectedAnimation animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("ImageJumpInAnimation");
            if (animation != null)
            {
                animation.Cancel();                
            }

            base.OnNavigatedTo(e);
        }



        private EBookReaderPageViewModel _vm { get; set; }

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
            var pageVM = _vm;
            this.InnerGoPrevImageCommand = new DelegateCommand(ExecuteGoPrevCommand);
            this.InnerGoNextImageCommand = new DelegateCommand(ExecuteGoNextCommand);

            _disposables = new CompositeDisposable();
        }

        CompositeDisposable _disposables;

        #region Bottom UI Menu


        private void TapAndController_Unloaded(object sender, RoutedEventArgs e)
        {
            SystemNavigationManager.GetForCurrentView().BackRequested -= ImageViewerPage_BackRequested;
        }

        private void ResetAnimationUIContainer_Loaded1(object sender, RoutedEventArgs e)
        {
            AnimationBuilder.Create()
                .Translation(Axis.Y, (float)AnimationUICommandBar.ActualHeight + 24, duration: TimeSpan.FromMilliseconds(175), delay: TimeSpan.FromMilliseconds(1000))
                .Start(AnimationUICommandBar);

            SwipeProcessScreen.Tapped += SwipeProcessScreen_Tapped;
            SwipeProcessScreen.ManipulationMode = ManipulationModes.TranslateY | ManipulationModes.TranslateX;
            SwipeProcessScreen.ManipulationStarting += SwipeProcessScreen_ManipulationStarting;
            SwipeProcessScreen.ManipulationStarted += SwipeProcessScreen_ManipulationStarted;
            SwipeProcessScreen.ManipulationCompleted += SwipeProcessScreen_ManipulationCompleted;

            SystemNavigationManager.GetForCurrentView().BackRequested += ImageViewerPage_BackRequested;
        }

        private void ImageViewerPage_BackRequested(object sender, BackRequestedEventArgs e)
        {
            if (TocContainer.Visibility == Visibility.Visible)
            {
                CloseTocPaneCommand.Execute();
            }      
            else
            {
                (_vm.BackNavigationCommand as ICommand).Execute(null);
            }
        }

        private void SwipeProcessScreen_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var pt = e.GetPosition(RootGrid);

            if (isOnceSkipTapped)
            {
                var bottomUIItems = VisualTreeHelper.FindElementsInHostCoordinates(pt, AnimationUICommandBar);
                if (bottomUIItems.Any()) { return; }

                CloseBottomUI();
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
                else if (item == ToggleBottomMenuButton)
                {
                    if (ToggleBottomMenuButton.Command?.CanExecute(null) ?? false)
                    {
                        ToggleBottomMenuButton.Command.Execute(null);
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
            if (e.Cumulative.Translation.X > 60
                || e.Velocities.Linear.X > 0.75
                )
            {
                // 右スワイプ
                LeftPageMoveButton.Command.Execute(null);
            }
            else if (e.Cumulative.Translation.X < -60
                || e.Velocities.Linear.X < -0.75
                )
            {
                // 左スワイプ
                RightPageMoveButton.Command.Execute(null);
            }
            else if (e.Cumulative.Translation.Y < -60
                || e.Velocities.Linear.Y < -0.25
                )
            {
                _ = CompleteOpenBottomUI();
                e.Handled = true;
            }
            else
            {
                CloseBottomUI();
                e.Handled = true;
            }
        }

        private void CloseBottomUI()
        {
            IsOpenBottomMenu = false;
            AnimationBuilder.Create()
                .Opacity(0.0, duration: TimeSpan.FromMilliseconds(175))
                .Start(AnimationUIContainer);

            AnimationBuilder.Create()
                .Translation(Axis.Y, (float)AnimationUICommandBar.ActualHeight, duration: TimeSpan.FromMilliseconds(175))
                .Start(AnimationUICommandBar);
        }

        private async Task CompleteOpenBottomUI()
        {
            IsOpenBottomMenu = true;

            AnimationBuilder.Create()
                .Opacity(1.0, duration: TimeSpan.FromMilliseconds(175))
                .Start(AnimationUIContainer);

            await AnimationBuilder.Create()
                .Translation(Axis.Y, 0, duration: TimeSpan.FromMilliseconds(175))
                .StartAsync(AnimationUICommandBar);
        }




        public bool IsOpenBottomMenu
        {
            get { return (bool)GetValue(IsOpenBottomMenuProperty); }
            set { SetValue(IsOpenBottomMenuProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsOpenBottomMenu.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsOpenBottomMenuProperty =
            DependencyProperty.Register("IsOpenBottomMenu", typeof(bool), typeof(ImageViewerPage), new PropertyMetadata(false));


        // コントローラー操作用
        public async void ToggleOpenCloseBottomUI()
        {
            if (!IsOpenBottomMenu)
            {
                ImageNavigationFlyoutButton.Focus(FocusState.Keyboard);
                await CompleteOpenBottomUI();
            }
            else
            {
                CloseBottomUI();
            }
        }

        private DelegateCommand _toggleBottomMenuCommand;
        public DelegateCommand ToggleBottomMenuCommand =>
            _toggleBottomMenuCommand ?? (_toggleBottomMenuCommand = new DelegateCommand(ExecuteToggleBottomMenuCommand, () => true) { IsActive = true });

        void ExecuteToggleBottomMenuCommand()
        {
            ToggleOpenCloseBottomUI();
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
            (DataContext as EBookReaderPageViewModel).CompletePageLoading();

            AnimationBuilder.Create()
                .Opacity(1.0, duration: TimeSpan.FromMilliseconds(100))
                .Start(WebView);
                
            //WebView.Fade(1.0f, 100).Start();
        }

        public ICommand InnerGoNextImageCommand
        {
            get { return (ICommand)GetValue(InnerGoNextImageCommandProperty); }
            private set { SetValue(InnerGoNextImageCommandProperty, value); }
        }


        Models.Infrastructure.AsyncLock _movePageLock = new ();

        // Using a DependencyProperty as the backing store for MyProperty.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty InnerGoNextImageCommandProperty =
            DependencyProperty.Register("InnerGoNextImageCommand", typeof(ICommand), typeof(EBookReaderPage), new PropertyMetadata(null));


        async void ExecuteGoNextCommand()
        {
            using (await _movePageLock.LockAsync(default))
            {
                if (WebView.CanGoNext())
                {
                    WebView.GoNext();
                }
                else
                {
                    var pageVM = DataContext as EBookReaderPageViewModel;
                    if (pageVM.CanGoNext())
                    {
                        await AnimationBuilder.Create()
                            .Opacity(0.0, duration: TimeSpan.FromMilliseconds(50))
                            .StartAsync(WebView);

                        WebView.PrepareGoNext();
                        await pageVM.GoNextImageAsync();
                    }
                }
            }
        }






        public ICommand InnerGoPrevImageCommand
        {
            get { return (ICommand)GetValue(InnerGoPrevImageCommandProperty); }
            private set { SetValue(InnerGoPrevImageCommandProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MyProperty.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty InnerGoPrevImageCommandProperty =
            DependencyProperty.Register("InnerGoPrevImageCommand", typeof(ICommand), typeof(EBookReaderPage), new PropertyMetadata(null));


        async void ExecuteGoPrevCommand()
        {
            using (await _movePageLock.LockAsync(default))
            {
                if (WebView.CanGoPreview())
                {
                    WebView.GoPreview();
                }
                else
                {
                    var pageVM = DataContext as EBookReaderPageViewModel;
                    if (pageVM.CanGoPrev())
                    {
                        await AnimationBuilder.Create()
                            .Opacity(0.0, duration: TimeSpan.FromMilliseconds(50))
                            .StartAsync(WebView);

                        WebView.PrepareGoPreview();
                        await pageVM.GoPrevImageAsync();
                    }
                }
            }
        }



        private DelegateCommand _OpenTocPaneCommand;
        public DelegateCommand OpenTocPaneCommand =>
            _OpenTocPaneCommand ?? (_OpenTocPaneCommand = new DelegateCommand(ExecuteOpenTocPaneCommand));

        async void ExecuteOpenTocPaneCommand()
        {
            CloseBottomUI();

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


        private DelegateCommand _CloseTocPaneCommand;
        public DelegateCommand CloseTocPaneCommand =>
            _CloseTocPaneCommand ?? (_CloseTocPaneCommand = new DelegateCommand(ExecuteCloseTocPaneCommand));

        void ExecuteCloseTocPaneCommand()
        {
            TocContainer.Visibility = Visibility.Collapsed;
        }

        private void CoverImage_Tapped(object sender, TappedRoutedEventArgs e)
        {
            (DataContext as EBookReaderPageViewModel).CurrentImageIndex = 0;
        }

        public void RefreshPage()
        {
            WebView.Refresh();
        }

        private void BackgroundColorPickerFlyout_Opening(object sender, object e)
        {
            var pageVM = DataContext as EBookReaderPageViewModel;
            var color = pageVM.EBookReaderSettings.BackgroundColor;
            if (color.A == 0)
            {
                color.A = 0xff;
                pageVM.EBookReaderSettings.BackgroundColor = color;
            }
        }

        private void ForegroundColorPickerFlyout_Opening(object sender, object e)
        {
            var pageVM = DataContext as EBookReaderPageViewModel;
            var color = pageVM.EBookReaderSettings.ForegroundColor;
            if (color == null)
            {
                pageVM.EBookReaderSettings.ForegroundColor = new Color() { A = 0xff } ;
            }
        }
    }
}
