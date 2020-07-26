using Prism.Commands;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
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

            DataContext = _viewModel = viewModel;

            // Navigation Handling
            ContentFrame.Navigated += Frame_Navigated;
            SystemNavigationManager.GetForCurrentView().BackRequested += App_BackRequested;
            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
            Window.Current.CoreWindow.PointerPressed += CoreWindow_PointerPressed;
        }


        private Type[] MenuPaneHiddenPageTypes = new Type[] 
        {
            typeof(Views.ImageViewerPage),
            typeof(SettingsPage),
        };

        private Type[] CanGoBackPageTypes = new Type[] 
        {
            typeof(Views.FolderListupPage),
            typeof(Views.ImageViewerPage),
            typeof(SettingsPage),
        };

        private void Frame_Navigated(object sender, NavigationEventArgs e)
        {
            BackCommand.RaiseCanExecuteChanged();

            MyNavigtionView.IsPaneVisible = !MenuPaneHiddenPageTypes.Any(x => x == e.SourcePageType);
            if (MyNavigtionView.IsPaneVisible)
            {
                var sourcePageTypeName = e.SourcePageType.Name;
                if (e.SourcePageType == typeof(FolderListupPage))
                {
                    sourcePageTypeName = nameof(Views.SourceFoldersPage);
                }
                var selectedMeuItemVM = ((List<object>)MyNavigtionView.MenuItemsSource).FirstOrDefault(x => (x as MenuItemViewModel)?.PageType == sourcePageTypeName);
                if (selectedMeuItemVM != null)
                {
                    MyNavigtionView.SelectedItem = selectedMeuItemVM;
                }
            }

            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                CanGoBackPageTypes.Contains(e.SourcePageType)
                ? AppViewBackButtonVisibility.Visible
                : AppViewBackButtonVisibility.Collapsed
                ;
        }

        private readonly PrimaryWindowCoreLayoutViewModel _viewModel;
        IPlatformNavigationService _navigationService;
        public IPlatformNavigationService GetNavigationService()
        {
            return _navigationService ??= NavigationService.Create(this.ContentFrame, Gestures.Refresh);
        }


        private DelegateCommand _BackCommand;
        public DelegateCommand BackCommand =>
            _BackCommand ??= new DelegateCommand(
                () => _ = _navigationService?.GoBackAsync(),
                () => _navigationService?.CanGoBack() ?? false
                );


        #region Back/Forward Navigation



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
            if (!CanGoBackPageTypes.Contains(ContentFrame.Content.GetType()))
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
