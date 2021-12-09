using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Mvvm.Messaging;
using Microsoft.Toolkit.Uwp.UI;
using Prism.Commands;
using Prism.Navigation;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.RestoreNavigation;
using TsubameViewer.Models.Domain.SourceFolders;
using TsubameViewer.Presentation.ViewModels;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Text.Core;
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
        private readonly PrimaryWindowCoreLayoutViewModel _viewModel;
        private readonly IMessenger _messenger;

        public PrimaryWindowCoreLayout(
            PrimaryWindowCoreLayoutViewModel viewModel, 
            IMessenger messenger
            )
        {
            this.InitializeComponent();

            DataContext = _viewModel = viewModel;
            _messenger = messenger;

            // Navigation Handling
            ContentFrame.Navigated += Frame_Navigated;
            SystemNavigationManager.GetForCurrentView().BackRequested += App_BackRequested;
            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
            Window.Current.CoreWindow.PointerPressed += CoreWindow_PointerPressed;

            _backNavigationEventSubscriber = _viewModel.EventAggregator.GetEvent<BackNavigationRequestEvent>()
                .Subscribe(() => HandleBackRequest(), keepSubscriberReferenceAlive: true);

            _refreshNavigationEventSubscriber = _viewModel.EventAggregator.GetEvent<RefreshNavigationRequestEvent>()
                .Subscribe(() => RefreshCommand.Execute(), keepSubscriberReferenceAlive: true);

            _themeChangeRequestEventSubscriber = _viewModel.EventAggregator.GetEvent<ThemeChangeRequestEvent>()
                .Subscribe(theme => SetTheme(theme), keepSubscriberReferenceAlive: true);

            SetTheme(_viewModel.ApplicationSettings.Theme);

            AutoSuggestBox.Loaded += PrimaryWindowCoreLayout_Loaded;

            _messenger.Register<BusyWallStartRequestMessage>(this, (r, m) => 
            {
                VisualStateManager.GoToState(this, VS_ShowBusyWall.Name, true);
            });

            _messenger.Register<BusyWallExitRequestMessage>(this, (r, m) =>
            {
                VisualStateManager.GoToState(this, VS_HideBusyWall.Name, true);
            });


            CancelBusyWorkCommand = new RelayCommand(() => _messenger.Send<BusyWallCanceledMessage>());
        }

        private bool NowShowingBusyWork => BusyWall.IsHitTestVisible;

        private RelayCommand CancelBusyWorkCommand { get; }
        

        private void PrimaryWindowCoreLayout_Loaded(object sender, RoutedEventArgs e)
        {
            var textBox = AutoSuggestBox.FindDescendant<TextBox>();
            textBox.TextCompositionStarted += TextBox_TextCompositionStarted;
            textBox.TextCompositionEnded += TextBox_TextCompositionEnded;
            textBox.TextChanged += TextBox_TextChanged;
        }

        bool _isInputIncomplete;
        private void TextBox_TextCompositionStarted(TextBox sender, TextCompositionStartedEventArgs args)
        {
            _isInputIncomplete = true;
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInputIncomplete == false)
            {
                (DataContext as PrimaryWindowCoreLayoutViewModel).UpdateAutoSuggestCommand.Execute(AutoSuggestBox.Text);
            }
        }

        private void TextBox_TextCompositionEnded(TextBox sender, TextCompositionEndedEventArgs args)
        {
            _isInputIncomplete = false;
            (DataContext as PrimaryWindowCoreLayoutViewModel).UpdateAutoSuggestCommand.Execute(AutoSuggestBox.Text);
        }

        IDisposable _backNavigationEventSubscriber;
        IDisposable _refreshNavigationEventSubscriber;
        IDisposable _themeChangeRequestEventSubscriber;

        private Type[] MenuPaneHiddenPageTypes = new Type[] 
        {
            typeof(ImageViewerPage),
            typeof(EBookReaderPage),
            typeof(SettingsPage),
        };

        private Type[] CanGoBackPageTypes = new Type[] 
        {
            typeof(FolderListupPage),
            typeof(ImageListupPage),
            typeof(ImageViewerPage),
            typeof(EBookReaderPage),
            typeof(SettingsPage),
        };

        private Type[] ForgetOwnNavigationPageTypes = new Type[]
        {
            typeof(ImageViewerPage),
            typeof(EBookReaderPage),
            typeof(SearchResultPage),
        };



        private List<INavigationParameters> BackParametersStack = new List<INavigationParameters>();
        private List<INavigationParameters> ForwardParametersStack = new List<INavigationParameters>();

        bool _isFirstNavigation = true;
        private void Frame_Navigated(object sender, NavigationEventArgs e)
        {
            if (e.NavigationMode == Windows.UI.Xaml.Navigation.NavigationMode.Refresh) { return; }

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
                // ここのFrame_Navigatedが呼ばれた後にViewModel側のNavigatingToが呼ばれる
                // 順序重要
                _Prev = PrimaryWindowCoreLayout.CurrentNavigationParameters;


                // ビューワー系ページはバックスタックに積まれないようにする
                // ビューワー系ページを開いてる状態でアプリ外部からビューワー系ページを開く操作があり得る
                bool rememberBackStack = true;
                if (frame.BackStack.LastOrDefault() is not null and var lastNavigatedPageEntry)
                {
                    if (ForgetOwnNavigationPageTypes.Any(type => type == e.SourcePageType))
                    {
                        if (ForgetOwnNavigationPageTypes.Any(type => type == lastNavigatedPageEntry.SourcePageType)
                            && e.SourcePageType == lastNavigatedPageEntry.SourcePageType
                            )
                        {
                            frame.BackStack.RemoveAt(frame.BackStackDepth - 1);
                            rememberBackStack = false;
                        }
                    }
                }

                if (e.NavigationMode != Windows.UI.Xaml.Navigation.NavigationMode.New)
                {
                    rememberBackStack = false;
                }

                if (rememberBackStack)
                { 
                    ForwardParametersStack.Clear();
                    var prevParameters = new NavigationParameters();
                    if (_Prev != null)
                    {
                        foreach (var pair in _Prev)
                        {
                            if (pair.Key == PageNavigationConstants.Restored) { continue; }

                            prevParameters.Add(pair.Key, pair.Value);
                        }
                    }

                    BackParametersStack.Add(prevParameters);
                }

                _ = StoreNaviagtionParameterDelayed();
            }

            MyNavigtionView.SelectionChanged += MyNavigtionView_SelectionChanged;
            // 選択中として表示するメニュー項目
            if (e.SourcePageType == typeof(SearchResultPage) 
                ||  frame.BackStack.Any(x => x.SourcePageType == typeof(SearchResultPage)))
            {
                MyNavigtionView.SelectedItem = null;
            }

            _isFirstNavigation = false;
        }

        private void MyNavigtionView_SelectionChanged(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem != null)
            {
                _viewModel.OpenMenuItemCommand.Execute(args.SelectedItem);
            }
        }

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

        private DelegateCommand _RefreshCommand;
        public DelegateCommand RefreshCommand =>
            _RefreshCommand ??= new DelegateCommand(
                () => _ = _navigationService?.RefreshAsync()
                );



        #region Back/Forward Navigation

        public async Task RestoreNavigationStack()
        {
            var navigationManager = _viewModel.RestoreNavigationManager;
            try
            {
                var currentEntry = navigationManager.GetCurrentNavigationEntry();
                if (currentEntry == null)
                {
                    Debug.WriteLine("[NavvigationRestore] skip restore page.");
                    await _navigationService.NavigateAsync(nameof(SourceStorageItemsPage), PageTransisionHelper.MakeNavigationTransitionInfoFromPageName(nameof(SourceStorageItemsPage)));
                    return;
                }

                var parameters = MakeNavigationParameter(currentEntry.Parameters);
                if (!parameters.ContainsKey(PageNavigationConstants.Restored))
                {
                    parameters.Add(PageNavigationConstants.Restored, string.Empty);
                }
                var result = await _navigationService.NavigateAsync(currentEntry.PageName, parameters, PageTransisionHelper.MakeNavigationTransitionInfoFromPageName(currentEntry.PageName));
                if (!result.Success)
                {
                    await Task.Delay(50);
                    Debug.WriteLine("[NavvigationRestore] Failed restore CurrentPage: " + currentEntry.PageName);
                    await _navigationService.NavigateAsync(nameof(SourceStorageItemsPage), PageTransisionHelper.MakeNavigationTransitionInfoFromPageName(nameof(SourceStorageItemsPage)));
                    return;
                }

                Debug.WriteLine("[NavvigationRestore] Restored CurrentPage: " + currentEntry.PageName);

                if (currentEntry.PageName == nameof(Views.SourceStorageItemsPage))
                {
                    return;
                }
            }
            catch
            {
                Debug.WriteLine("[NavvigationRestore] failed restore current page. ");

                BackParametersStack.Clear();
                ForwardParametersStack.Clear();
                ContentFrame.BackStack.Clear();
                ContentFrame.ForwardStack.Clear();

                await StoreNaviagtionParameterDelayed();
                await _navigationService.NavigateAsync(nameof(SourceStorageItemsPage), PageTransisionHelper.MakeNavigationTransitionInfoFromPageName(nameof(SourceStorageItemsPage)));
                return;
            }


            {
                var backStack = await navigationManager.GetBackNavigationEntriesAsync();
                foreach (var backNavItem in backStack)
                {
                    var pageType = Type.GetType($"TsubameViewer.Presentation.Views.{backNavItem.PageName}");
                    var parameters = MakeNavigationParameter(backNavItem.Parameters);
                    ContentFrame.BackStack.Add(new PageStackEntry(pageType, parameters, PageTransisionHelper.MakeNavigationTransitionInfoFromPageName(backNavItem.PageName)));
                    BackParametersStack.Add(parameters);
                    Debug.WriteLine("[NavvigationRestore] Restored BackStackPage: " + backNavItem.PageName);
                }
            }
            /*
            {
                var forwardStack = await navigationManager.GetForwardNavigationEntriesAsync();
                foreach (var forwardNavItem in forwardStack)
                {
                    var pageType = Type.GetType($"TsubameViewer.Presentation.Views.{forwardNavItem.PageName}");
                    var parameters = MakeNavigationParameter(forwardNavItem.Parameters);
                    ContentFrame.ForwardStack.Add(new PageStackEntry(pageType, parameters, new SuppressNavigationTransitionInfo()));
                    ForwardParametersStack.Add(parameters);
                    Debug.WriteLine("[NavvigationRestore] Restored BackStackPage: " + forwardNavItem.PageName);
                }
            }
            */
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
        
        public static void SetCurrentNavigationParameters(INavigationParameters parameters)
        {
            if (parameters.GetNavigationMode() == Prism.Navigation.NavigationMode.Refresh) { return; }

            CurrentNavigationParameters = parameters;
        }

        public static INavigationParameters CurrentNavigationParameters { get; private set; }

        public static NavigationParameters GetCurrentNavigationParameter()
        {
            return CurrentNavigationParameters?.Clone() ?? new NavigationParameters();
        }


        // NavigationManager.BackRequestedによる戻るを一時的に防止する
        // ビューワー系ページでコントローラー操作でバックナビゲーションを手動で行うことが目的
        public static bool IsPreventSystemBackNavigation { get; set; }
        public CoreTextEditContext _context { get; private set; }

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
                await _viewModel.RestoreNavigationManager.SetBackNavigationEntriesAsync(backNavigationPageEntries);
            }
            /*
            {
                PageEntry[] forwardNavigationPageEntries = new PageEntry[ForwardParametersStack.Count];
                for (var forwardStackIndex = 0; forwardStackIndex < ForwardParametersStack.Count; forwardStackIndex++)
                {
                    var parameters = ForwardParametersStack[forwardStackIndex];
                    var stackEntry = ContentFrame.ForwardStack[forwardStackIndex];
                    forwardNavigationPageEntries[forwardStackIndex] = MakePageEnetry(stackEntry.SourcePageType, parameters);
                    Debug.WriteLine("[NavvigationRestore] Save ForwardStackPage: " + forwardNavigationPageEntries[forwardStackIndex].PageName);
                }
                await _viewModel.RestoreNavigationManager.SetForwardNavigationEntriesAsync(forwardNavigationPageEntries);
            }
            */
        }

        static PageEntry MakePageEnetry(Type pageType, INavigationParameters parameters)
        {
            return new PageEntry(pageType.Name, parameters);
        }


        bool HandleBackRequest()
        {
            if (NowShowingBusyWork)
            {
                CancelBusyWorkCommand.Execute(null);
                return false;
            }

            var currentPageType = ContentFrame.Content?.GetType();
            if (!CanGoBackPageTypes.Contains(ContentFrame.Content.GetType()))
            {
                Debug.WriteLine($"{currentPageType.Name} からの戻る操作をブロック");
                return false;
            }

            if (_navigationService.CanGoBack())
            {
                var parameters = GetCurrentNavigationParameter();    // GoBackAsyncを呼ぶとCurrentNavigationParametersが入れ替わる。呼び出し順に注意。
                if (parameters.TryGetValue(PageNavigationConstants.Path, out string currentPath)
                    && parameters.ContainsKey(PageNavigationConstants.ArchiveFolderName) is false
                    )
                {
                    while (BackParametersStack.SkipLast(1).LastOrDefault() is not null and var lastNavigationParameters
                        && lastNavigationParameters.TryGetValue(PageNavigationConstants.Path, out string lastPath)
                        && currentPath == lastPath
                        )
                    {
                        ContentFrame.BackStack.RemoveAt(ContentFrame.BackStackDepth - 1);
                        BackParametersStack.Remove(lastNavigationParameters);
                    }
                }
                {
                    var lastNavigationParameters = BackParametersStack.LastOrDefault();
                    if (lastNavigationParameters != null)
                    {
                        BackParametersStack.Remove(lastNavigationParameters);
                        ForwardParametersStack.Add(parameters);
                        _ = lastNavigationParameters == null
                            ? _navigationService.GoBackAsync()
                            : _navigationService.GoBackAsync(lastNavigationParameters)
                            ;
                    }
                    else
                    {
                        _navigationService.NavigateAsync(nameof(Views.SourceStorageItemsPage));
                    }
                }
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
                    var parameters = GetCurrentNavigationParameter(); // GoForwardAsyncを呼ぶとCurrentNavigationParametersが入れ替わる。呼び出し順に注意。
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
            if (IsPreventSystemBackNavigation) { return; }

            if (HandleBackRequest())
            {
                Debug.WriteLine("back navigated with SystemNavigationManager.BackRequested");
            }

            // Note: 強制的にハンドルしないとXboxOneやタブレットでアプリを閉じる動作に繋がってしまう
            e.Handled = true;
        }






        #endregion


        #region Theme

        public void SetTheme(Models.Domain.ApplicationTheme applicationTheme)
        {
#if WINDOWS_UWP
            if (applicationTheme == Models.Domain.ApplicationTheme.Default)
            {
                applicationTheme = SystemThemeHelper.GetSystemTheme();
            }
#endif

            this.RequestedTheme = applicationTheme switch
            {
                Models.Domain.ApplicationTheme.Light => ElementTheme.Light,
                Models.Domain.ApplicationTheme.Dark => ElementTheme.Dark,
                Models.Domain.ApplicationTheme.Default => ElementTheme.Default,
                _ => throw new NotSupportedException(),
            };
        }

        #endregion


        #region Drop Action

        private async void Grid_DragEnter(object sender, DragEventArgs e)
        {
            var deferral = e.GetDeferral();
            try
            {
                if (e.DataView.Contains(StandardDataFormats.StorageItems))
                {
                    var items = await e.DataView.GetStorageItemsAsync();
                    var isAllAcceptableItem = items.All(item => item is StorageFolder 
                    || (item is StorageFile file && SupportedFileTypesHelper.IsSupportedFileExtension(file.FileType))
                    );
                    if (isAllAcceptableItem)
                    {
                        e.AcceptedOperation = DataPackageOperation.Link;
                    }
                }
            }
            finally
            {
                deferral.Complete();
            }
        }

        private async void Grid_Drop(object sender, DragEventArgs e)
        {
            var defferal = e.GetDeferral();

            try
            {
                string token = null;
                IStorageItem openStorageItem = null;
                var dropItems = await e.DataView.GetStorageItemsAsync();
                foreach (var storageItem in dropItems)
                {
                    if (storageItem is StorageFolder)
                    {
                        token = await _viewModel.SourceStorageItemsRepository.AddItemPersistantAsync(storageItem, SourceOriginConstants.DragAndDrop);
                    }
                    else if (storageItem is StorageFile file)
                    {
                        token = await _viewModel.SourceStorageItemsRepository.AddFileTemporaryAsync(file, SourceOriginConstants.DragAndDrop);
                    }

                    openStorageItem = storageItem;
                }
                
                if (dropItems.Count == 1)
                {
                    if (openStorageItem is StorageFolder)
                    {
                        await _viewModel.NavigationService.NavigateAsync(nameof(Views.FolderListupPage), new NavigationParameters((PageNavigationConstants.Path, openStorageItem.Path)), PageTransisionHelper.MakeNavigationTransitionInfoFromPageName(nameof(FolderListupPage)));
                    }
                    else if (openStorageItem is StorageFile fileItem)
                    {
                        if (SupportedFileTypesHelper.IsSupportedArchiveFileExtension(fileItem.FileType)
                            || SupportedFileTypesHelper.IsSupportedImageFileExtension(fileItem.FileType)
                            )
                        {
                            await _viewModel.NavigationService.NavigateAsync(nameof(Views.ImageViewerPage), new NavigationParameters((PageNavigationConstants.Path, openStorageItem.Path)), PageTransisionHelper.MakeNavigationTransitionInfoFromPageName(nameof(ImageViewerPage)));
                        }
                        else if (SupportedFileTypesHelper.IsSupportedEBookFileExtension(fileItem.FileType))
                        {
                            await _viewModel.NavigationService.NavigateAsync(nameof(Views.EBookReaderPage), new NavigationParameters((PageNavigationConstants.Path, openStorageItem.Path)), PageTransisionHelper.MakeNavigationTransitionInfoFromPageName(nameof(EBookReaderPage)));
                        }
                    }
                }
            }
            finally
            {
                defferal.Complete();
            }
        }

        #endregion

        private void MyNavigtionView_PaneOpening(Microsoft.UI.Xaml.Controls.NavigationView sender, object args)
        {
            sender.IsPaneOpen = false;
        }
    }


    public static class NavigationParametersExtensions
    {
        public static NavigationParameters Clone(this INavigationParameters parameters)
        {
            var clone = new NavigationParameters();
            foreach (var pair in parameters)
            {
                clone.Add(pair.Key, pair.Value);
            }

            return clone;
        }
    }
}
