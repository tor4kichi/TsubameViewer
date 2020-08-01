using Prism.Commands;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain.RestoreNavigation;
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
using Windows.UI.Xaml.Media.Animation;
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

        bool _isFirstNavigation = true;
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
            
            // 戻れない設定のページではバックナビゲーションボタンを非表示に切り替え
            var isCanGoBackPage = CanGoBackPageTypes.Contains(e.SourcePageType);
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                isCanGoBackPage
                ? AppViewBackButtonVisibility.Visible
                : AppViewBackButtonVisibility.Collapsed
                ;

            // 戻れない設定のページに到達したら Frame.BackStack から不要なPageEntryを削除する
            if (!isCanGoBackPage)
            {
                ContentFrame.BackStack.Clear();
                BackParametersStack.Clear();

                if (e.NavigationMode == Windows.UI.Xaml.Navigation.NavigationMode.New)
                {
                    ContentFrame.ForwardStack.Clear();
                    ForwardParametersStack.Clear();
                }

                _ = StoreNaviagtionParameterDelayed();
            }
            else if (!_isFirstNavigation)
            {
                // 順序重要
                _Prev = PrimaryWindowCoreLayout.CurrentNavigationParameters;

                if (e.NavigationMode == Windows.UI.Xaml.Navigation.NavigationMode.New)
                {
                    ForwardParametersStack.Clear();
                    var parameters = new NavigationParameters();
                    if (_Prev != null)
                    {
                        foreach (var pair in _Prev)
                        {
                            if (pair.Key == "__restored") { continue; }

                            parameters.Add(pair.Key, pair.Value);
                        }
                    }
                    BackParametersStack.Add(parameters);
                }

                _ = StoreNaviagtionParameterDelayed();
            }

            _isFirstNavigation = false;
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

        public async Task RestoreNavigationStack()
        {
            var navigationManager = _viewModel.RestoreNavigationManager;
            var currentEntry = navigationManager.GetCurrentNavigationEntry();
            if (currentEntry == null) 
            {
                Debug.WriteLine("[NavvigationRestore] skip restore page.");
                await _navigationService.NavigateAsync(nameof(SourceStorageItemsPage));
                return;
            }

            try
            {
                var parameters = MakeNavigationParameter(currentEntry.Parameters);
                parameters.Add("__restored", string.Empty);
                var result = await _navigationService.NavigateAsync(currentEntry.PageName, parameters);
                if (!result.Success)
                {
                    await Task.Delay(50);
                    Debug.WriteLine("[NavvigationRestore] Failed restore CurrentPage: " + currentEntry.PageName);
                    await _navigationService.NavigateAsync(nameof(SourceStorageItemsPage));
                    return;
                }
            }
            catch
            {
                BackParametersStack.Clear();
                ForwardParametersStack.Clear();
                ContentFrame.BackStack.Clear();
                ContentFrame.ForwardStack.Clear();

                await StoreNaviagtionParameterDelayed();
                await _navigationService.NavigateAsync(nameof(SourceStorageItemsPage));
                return;
            }

            Debug.WriteLine("[NavvigationRestore] Restored CurrentPage: " + currentEntry.PageName);

            {
                var backStack = navigationManager.GetBackNavigationEntries();
                foreach (var backNavItem in backStack)
                {
                    var pageType = Type.GetType($"TsubameViewer.Presentation.Views.{backNavItem.PageName}");
                    var parameters = MakeNavigationParameter(backNavItem.Parameters);
                    ContentFrame.BackStack.Add(new PageStackEntry(pageType, parameters, new SuppressNavigationTransitionInfo()));
                    BackParametersStack.Add(parameters);
                    Debug.WriteLine("[NavvigationRestore] Restored BackStackPage: " + backNavItem.PageName);
                }
            }

            {
                var forwardStack = navigationManager.GetForwardNavigationEntries();
                foreach (var forwardNavItem in forwardStack)
                {
                    var pageType = Type.GetType($"TsubameViewer.Presentation.Views.{forwardNavItem.PageName}");
                    var parameters = MakeNavigationParameter(forwardNavItem.Parameters);
                    ContentFrame.ForwardStack.Add(new PageStackEntry(pageType, parameters, new SuppressNavigationTransitionInfo()));
                    ForwardParametersStack.Add(parameters);
                    Debug.WriteLine("[NavvigationRestore] Restored BackStackPage: " + forwardNavItem.PageName);
                }
            }
        }

        static INavigationParameters MakeNavigationParameter(IEnumerable<KeyValuePair<string, string>> parameters)
        {
            var np = new NavigationParameters();
            if (parameters == null) { return np; }
            foreach (var item in parameters)
            {
                np.Add(item.Key, item.Value);
            }

            return np;
        }
        
        public static INavigationParameters CurrentNavigationParameters { get; set; }




        private INavigationParameters _Prev;

        async Task StoreNaviagtionParameterDelayed()
        {
            await Task.Delay(50);

            // ナビゲーション状態の保存
            Debug.WriteLine("[NavvigationRestore] Save CurrentPage: " + ContentFrame.CurrentSourcePageType.Name);
            _viewModel.RestoreNavigationManager.SetCurrentNavigationEntry(MakePageEnetry(ContentFrame.CurrentSourcePageType, CurrentNavigationParameters));
            {
                PageEntry[] backNavigationPageEntries = new PageEntry[BackParametersStack.Count];
                for (var backStackIndex = 0; backStackIndex < BackParametersStack.Count; backStackIndex++)
                {
                    var parameters = BackParametersStack[backStackIndex];
                    var stackEntry = ContentFrame.BackStack[backStackIndex];
                    backNavigationPageEntries[backStackIndex] = MakePageEnetry(stackEntry.SourcePageType, parameters);
                    Debug.WriteLine("[NavvigationRestore] Save BackStackPage: " + backNavigationPageEntries[backStackIndex].PageName);
                }
                _viewModel.RestoreNavigationManager.SetBackNavigationEntries(backNavigationPageEntries);
            }
            {
                PageEntry[] forwardNavigationPageEntries = new PageEntry[ForwardParametersStack.Count];
                for (var forwardStackIndex = 0; forwardStackIndex < ForwardParametersStack.Count; forwardStackIndex++)
                {
                    var parameters = ForwardParametersStack[forwardStackIndex];
                    var stackEntry = ContentFrame.ForwardStack[forwardStackIndex];
                    forwardNavigationPageEntries[forwardStackIndex] = MakePageEnetry(stackEntry.SourcePageType, parameters);
                    Debug.WriteLine("[NavvigationRestore] Save ForwardStackPage: " + forwardNavigationPageEntries[forwardStackIndex].PageName);
                }
                _viewModel.RestoreNavigationManager.SetForwardNavigationEntries(forwardNavigationPageEntries);
            }

        }

        static PageEntry MakePageEnetry(Type pageType, INavigationParameters parameters)
        {
            return new PageEntry(pageType.Name, parameters);
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
            else
            {
                _navigationService.NavigateAsync(nameof(Views.SourceStorageItemsPage));
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
