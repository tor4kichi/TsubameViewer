using Prism.Commands;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using TsubameViewer.Presentation.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
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
    public sealed partial class PrimaryWindowCoreLayout : Page
    {
        public PrimaryWindowCoreLayout(PrimaryWindowCoreLayoutViewModel viewModel)
        {
            this.InitializeComponent();

            ContentFrame.Navigated += Frame_Navigated;
            DataContext = _viewModel = viewModel;


            // Navigation Handling
            SystemNavigationManager.GetForCurrentView().BackRequested += App_BackRequested;
            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
            Window.Current.CoreWindow.PointerPressed += CoreWindow_PointerPressed;
        }


        private void Frame_Navigated(object sender, NavigationEventArgs e)
        {
            IsDisplayMenu = e.SourcePageType != typeof(ImageCollectionViewerPage);

            ProcessNavigationHandlingForCurrentPage(e.SourcePageType);
        }

        public bool IsDisplayMenu
        {
            get { return (bool)GetValue(IsDisplayMenuProperty); }
            set { SetValue(IsDisplayMenuProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsDisplayMenu.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsDisplayMenuProperty =
            DependencyProperty.Register("IsDisplayMenu", typeof(bool), typeof(PrimaryWindowCoreLayout), new PropertyMetadata(true));

        private readonly PrimaryWindowCoreLayoutViewModel _viewModel;
        IPlatformNavigationService _navigationService;
        public IPlatformNavigationService GetNavigationService()
        {
            return _navigationService ??= NavigationService.Create(this.ContentFrame, Gestures.Refresh);
        }


        private DelegateCommand _BackCommand;
        public DelegateCommand BackCommand =>
            _BackCommand ??= new DelegateCommand(() => _ = _navigationService?.GoBackAsync());


        #region Back/Forward Navigation


        Type[] NavigationHistoryResetPageTypes = new[]
        {
            typeof(HomePage)
        };

        void ProcessNavigationHandlingForCurrentPage(Type newPageType)
        {
            if (NavigationHistoryResetPageTypes.Any(x => newPageType == x))
            {
            }
        }


        private void CoreWindow_PointerPressed(CoreWindow sender, PointerEventArgs args)
        {
            if (args.KeyModifiers == Windows.System.VirtualKeyModifiers.None
                && args.CurrentPoint.Properties.IsXButton1Pressed
                )
            {
                if (HandleBackRequest())
                {
                    args.Handled = true;
                    Debug.WriteLine("back navigated with Pointer Back pressed");
                }
            }
            else if (args.KeyModifiers == Windows.System.VirtualKeyModifiers.None
                && args.CurrentPoint.Properties.IsXButton2Pressed
                )
            {
                if (HandleForwardRequest())
                {
                    args.Handled = true;
                    Debug.WriteLine("forward navigated with Pointer Forward pressed");
                }
            }
        }

        private void CoreWindow_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (args.VirtualKey == Windows.System.VirtualKey.GoBack)
            {
                if (HandleBackRequest())
                {
                    args.Handled = true;
                    Debug.WriteLine("back navigated with VirtualKey.Back pressed");
                }
            }
            else if (args.VirtualKey == Windows.System.VirtualKey.GoForward)
            {
                if (HandleForwardRequest())
                {
                    args.Handled = true;
                    Debug.WriteLine("forward navigated with VirtualKey.Back pressed");
                }
            }
        }

        private void App_BackRequested(object sender, BackRequestedEventArgs e)
        {
            if (HandleBackRequest())
            {
                Debug.WriteLine("back navigated with SystemNavigationManager.BackRequested");
            }

            // Note: 強制的にハンドルしないとXboxOneやタブレットでアプリを閉じる動作に繋がってしまう
            e.Handled = true;
        }

        bool HandleBackRequest()
        {
            var currentPageType = ContentFrame.Content?.GetType();
            if (currentPageType != null && NavigationHistoryResetPageTypes.Any(x => currentPageType == x))
            {
                Debug.WriteLine($"{currentPageType.Name} からの戻る操作をブロック");
                return false;
            }

            if (_navigationService.CanGoBack())
            {
                _ = _navigationService.GoBackAsync();
                return true;
            }

            return false;
        }


        bool HandleForwardRequest()
        {
            if (_navigationService.CanGoForward())
            {
                _ = _navigationService.GoForwardAsync();
                return true;
            }

            return false;
        }


        #endregion
    }
}
