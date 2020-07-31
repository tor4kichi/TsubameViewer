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


        private List<INavigationParameters> BackParametersStack = new List<INavigationParameters>();
        private List<INavigationParameters> ForwardParametersStack = new List<INavigationParameters>();

        bool _isFirstNavigation = false;
        private void Frame_Navigated(object sender, NavigationEventArgs e)
        {
            var frame = (Frame)sender;
            BackCommand.RaiseCanExecuteChanged();

            MyNavigtionView.IsPaneVisible = !MenuPaneHiddenPageTypes.Any(x => x == e.SourcePageType);
            if (MyNavigtionView.IsPaneVisible)
            {
                var sourcePageTypeName = e.SourcePageType.Name;
                if (e.SourcePageType == typeof(FolderListupPage))
                {
                    sourcePageTypeName = nameof(Views.SourceStorageItemsPage);
                }
                var selectedMeuItemVM = ((List<object>)MyNavigtionView.MenuItemsSource).FirstOrDefault(x => (x as MenuItemViewModel)?.PageType == sourcePageTypeName);
                if (selectedMeuItemVM != null)
                {
                    MyNavigtionView.SelectedItem = selectedMeuItemVM;
                }
            }

            // 画像ビューワーページはバックスタックに一つしか積まれないようにする
            if (e.SourcePageType == typeof(Views.ImageViewerPage))
            {
                var lastNavigatedPageEntry = frame.BackStack.ElementAtOrDefault(1);
                if (lastNavigatedPageEntry?.SourcePageType == typeof(Views.ImageViewerPage))
                {
                    frame.BackStack.RemoveAt(1);
                }
            }

            
            if (!_isFirstNavigation)
            {
                _Prev = PrimaryWindowCoreLayout.CurrentNavigationParameters;
                _ = StoreNaviagtionParameterDelayed(e);
            }

            _isFirstNavigation = false;




            // 戻れない設定のページではバックナビゲーションボタンを非表示に切り替え
            var isCanGoBackPage = CanGoBackPageTypes.Contains(e.SourcePageType);
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                isCanGoBackPage
                ? AppViewBackButtonVisibility.Visible
                : AppViewBackButtonVisibility.Collapsed
                ;

            // 戻れない設定のページに到達したら Frame.BackStack から不要なPageEntryを削除する
            if (isCanGoBackPage)
            {
                var oldCacheSize = ContentFrame.CacheSize;
                ContentFrame.CacheSize = 0;
                ContentFrame.CacheSize = oldCacheSize;
            }

            
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



        
        public static INavigationParameters CurrentNavigationParameters { get; set; }




        private INavigationParameters _Prev;

        async Task StoreNaviagtionParameterDelayed(NavigationEventArgs e)
        {
            await Task.Delay(50);
            if (e.NavigationMode == Windows.UI.Xaml.Navigation.NavigationMode.New)
            {
                ForwardParametersStack.Clear();
                var parameters = new NavigationParameters();
                if (_Prev != null)
                {
                    foreach (var pair in _Prev)
                    {
                        parameters.Add(pair.Key, pair.Value);
                    }
                }
                BackParametersStack.Add(parameters);
            }
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
                var backNavigationParameters = BackParametersStack.ElementAtOrDefault(BackParametersStack.Count - 1);
                {
                    var last = BackParametersStack.Last();
                    var current = CurrentNavigationParameters;    // GoBackAsyncを呼ぶとCurrentNavigationParametersが入れ替わる。呼び出し順に注意。
                    var parameters = new NavigationParameters();
                    if (current != null)
                    {
                        foreach (var pair in current)
                        {
                            parameters.Add(pair.Key, pair.Value);
                        }
                    }
                    BackParametersStack.Remove(last);
                    ForwardParametersStack.Add(parameters);
                }
                _ = backNavigationParameters == null
                    ? _navigationService.GoBackAsync()
                    : _navigationService.GoBackAsync(backNavigationParameters)
                    ;

                return true;
            }

            return false;
        }


        bool HandleForwardRequest()
        {
            if (_navigationService.CanGoForward())
            {
                var forwardNavigationParameters = ForwardParametersStack.Last();
                {
                    var last = ForwardParametersStack.Last();
                    var current = CurrentNavigationParameters; // GoForwardAsyncを呼ぶとCurrentNavigationParametersが入れ替わる。呼び出し順に注意。
                    var parameters = new NavigationParameters();
                    if (current != null)
                    {
                        foreach (var pair in current)
                        {
                            parameters.Add(pair.Key, pair.Value);
                        }
                    }
                    ForwardParametersStack.Remove(last);
                    BackParametersStack.Add(parameters);
                }
                _ = forwardNavigationParameters == null
                   ? _navigationService.GoForwardAsync()
                   : _navigationService.GoForwardAsync(forwardNavigationParameters)
                   ;

                return true;
            }

            return false;
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






        #endregion
    }
}
