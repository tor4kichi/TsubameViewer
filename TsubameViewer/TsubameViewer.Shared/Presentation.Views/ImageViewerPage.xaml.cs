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
using Uno;
using Uno.Disposables;
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

// 空白ページの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234238 を参照してください

namespace TsubameViewer.Presentation.Views
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class ImageViewerPage : Page
    {
        public ImageViewerPage()
        {
            this.InitializeComponent();

            Loaded += ImageViewerPage_Loaded;

            Loaded += ResetAnimationUIContainer_Loaded1;
            Unloaded += TapAndController_Unloaded;
        }

        private void TapAndController_Unloaded(object sender, RoutedEventArgs e)
        {
            SystemNavigationManager.GetForCurrentView().BackRequested -= ImageViewerPage_BackRequested;
        }

        private void ResetAnimationUIContainer_Loaded1(object sender, RoutedEventArgs e)
        {
            AnimationUICommandBar.Offset(offsetY: (float)AnimationUIContainer.ActualHeight, duration: 0 ).Start();
            
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
            ToggleOpenCloseBottomUI();
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


        private async void ImageViewerPage_Loaded(object sender, RoutedEventArgs e)
        {
            using (var cts = new CancellationTokenSource(5000))
            {
                Image.Opacity = 0.0;
                while (Image.Source == null)
                {
                    await Task.Delay(50, cts.Token);
                }

                Image.Fade(1.0f, 175f)
                    .Start();
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
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
    }
}
