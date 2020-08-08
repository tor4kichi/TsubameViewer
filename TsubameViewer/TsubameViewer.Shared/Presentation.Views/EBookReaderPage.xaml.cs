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
using Xamarin.Essentials;

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
        }

        #region Bottom UI Menu


        private void TapAndController_Unloaded(object sender, RoutedEventArgs e)
        {
            SystemNavigationManager.GetForCurrentView().BackRequested -= ImageViewerPage_BackRequested;
        }

        private void ResetAnimationUIContainer_Loaded1(object sender, RoutedEventArgs e)
        {
            AnimationUICommandBar.Offset(offsetY: (float)AnimationUIContainer.ActualHeight, duration: 0).Start();

            SwipeProcessScreen.Tapped += SwipeProcessScreen_Tapped;
            SwipeProcessScreen.ManipulationMode = ManipulationModes.TranslateY;
            SwipeProcessScreen.ManipulationStarting += SwipeProcessScreen_ManipulationStarting;
            SwipeProcessScreen.ManipulationStarted += SwipeProcessScreen_ManipulationStarted;
            SwipeProcessScreen.ManipulationDelta += SwipeProcessScreen_ManipulationDelta;
            SwipeProcessScreen.ManipulationCompleted += SwipeProcessScreen_ManipulationCompleted;

            SystemNavigationManager.GetForCurrentView().BackRequested += ImageViewerPage_BackRequested;
        }

        private void ImageViewerPage_BackRequested(object sender, BackRequestedEventArgs e)
        {
            if (TocContainer.Visibility == Visibility.Collapsed)
            {
                ToggleOpenCloseBottomUI();
            }

            CloseTocPaneCommand.Execute();
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

        private void SwipeProcessScreen_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            if (!e.IsInertial)
            {
                Debug.WriteLine(e.Cumulative.Translation.Y);
                if (e.Cumulative.Translation.Y < 0)
                {
                    AnimationUIContainer.Fade(0.5f, duration: 20).Start();
                    AnimationUICommandBar.Offset(offsetY: 16, duration: 20).Start();
                }
                else
                {
                    AnimationUIContainer.Fade(0.0f, duration: 20).Start();
                    AnimationUICommandBar.Offset(offsetY: (float)AnimationUIContainer.ActualHeight, duration: 20).Start();
                }
            }
        }

        private void SwipeProcessScreen_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            if (e.Cumulative.Translation.Y < -60
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

        private async void CloseBottomUI()
        {
            await AnimationUICommandBar.Offset(offsetY: (float)AnimationUIContainer.ActualHeight, duration: 100).StartAsync();
            await AnimationUIContainer.Fade(0.0f, duration: 100).StartAsync();
        }

        private async Task CompleteOpenBottomUI()
        {
            AnimationUIContainer.Fade(1.0f, duration: 100).Start();
            await AnimationUICommandBar.Offset(offsetY: 0, duration: 100).StartAsync();
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
            IsOpenBottomMenu = !IsOpenBottomMenu;
            if (IsOpenBottomMenu)
            {
                await CompleteOpenBottomUI();
                ImageNavigationFlyoutButton.Focus(FocusState.Keyboard);
            }
            else
            {
                CloseBottomUI();
            }
        }

        private DelegateCommand _toggleBottomMenuCommand;
        public DelegateCommand ToggleBottomMenuCommand =>
            _toggleBottomMenuCommand ?? (_toggleBottomMenuCommand = new DelegateCommand(ExecuteToggleBottomMenuCommand));

        void ExecuteToggleBottomMenuCommand()
        {
            ToggleOpenCloseBottomUI();
        }



        #endregion



        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // https://docs.microsoft.com/ja-jp/windows/uwp/design/shell/title-bar
            var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            coreTitleBar.ExtendViewIntoTitleBar = true;

            Window.Current.SetTitleBar(DraggableTitleBarArea_Desktop);
            Windows.UI.Core.SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = Windows.UI.Core.AppViewBackButtonVisibility.Collapsed;

            var appView = ApplicationView.GetForCurrentView();
            appView.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            appView.TitleBar.ButtonHoverBackgroundColor = Color.FromArgb(0x7f, 0xff, 0xff, 0xff);
            appView.TitleBar.ButtonInactiveBackgroundColor = Color.FromArgb(0x3f, 0xff, 0xff, 0xff);
            appView.TitleBar.ButtonPressedBackgroundColor = Color.FromArgb(0xaf, 0xff, 0xff, 0xff);

            PrimaryWindowCoreLayout.IsPreventSystemBackNavigation = true;

            base.OnNavigatedTo(e);
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

            PrimaryWindowCoreLayout.IsPreventSystemBackNavigation = false;

            base.OnNavigatingFrom(e);
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
            WebView.Fade(1.0f, 100).Start();
        }

        private DelegateCommand _InnerGoNextImageCommand;
        public DelegateCommand InnerGoNextImageCommand =>
            _InnerGoNextImageCommand ?? (_InnerGoNextImageCommand = new DelegateCommand(ExecuteGoNextCommand));

        async void ExecuteGoNextCommand()
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
                    await WebView.Fade(0, 50).StartAsync();
                    
                    WebView.PrepareGoNext();
                    pageVM.GoNextImageCommand.Execute();
                }
            }
        }

        private DelegateCommand _InnerGoPrevImageCommand;
        public DelegateCommand InnerGoPrevImageCommand =>
            _InnerGoPrevImageCommand ?? (_InnerGoPrevImageCommand = new DelegateCommand(ExecuteGoPrevCommand));

        async void ExecuteGoPrevCommand()
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
                    await WebView.Fade(0, 50).StartAsync();

                    WebView.PrepareGoPreview();
                    pageVM.GoPrevImageCommand.Execute();
                }
            }

        }


        private DelegateCommand _OpenTocPaneCommand;
        public DelegateCommand OpenTocPaneCommand =>
            _OpenTocPaneCommand ?? (_OpenTocPaneCommand = new DelegateCommand(ExecuteOpenTocPaneCommand));

        async void ExecuteOpenTocPaneCommand()
        {
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

            CloseBottomUI();
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
