﻿using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using I18NPortable;
using Microsoft.Toolkit.Uwp.UI;
using Microsoft.Toolkit.Uwp.UI.Animations;
using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Contracts.Navigation;
using TsubameViewer.Contracts.Notification;
using TsubameViewer.Contracts.Services;
using TsubameViewer.Core.Contracts.Services;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.Services.Navigation;
using TsubameViewer.ViewModels;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.ViewModels.SourceFolders;
using TsubameViewer.Views.Helpers;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace TsubameViewer.Views;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class PrimaryWindowCoreLayout : Page
{        
    private readonly PrimaryWindowCoreLayoutViewModel _vm;
    private readonly IMessenger _messenger;

    private readonly DispatcherQueue _dispatcherQueue;
    private readonly IViewLocator _viewLocator;
    private readonly DispatcherQueueTimer _AnimationCancelTimer;
    private readonly TimeSpan _BusyWallDisplayDelayTime = PageNavigationConstants.BusyWallDisplayDelayTime;

    public PrimaryWindowCoreLayout(
        PrimaryWindowCoreLayoutViewModel viewModel, 
        IMessenger messenger
        )
    {
        this.InitializeComponent();

        DataContext = _vm = viewModel;
        _messenger = messenger;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _viewLocator = Ioc.Default.GetService<IViewLocator>();
        InitializeNavigation();
        InitializeThemeChangeRequest();
        InitializeSearchBox();
        InitializeSelection();

        _AnimationCancelTimer = _dispatcherQueue.CreateTimer();
        CancelBusyWorkCommand = new RelayCommand(() => _messenger.Send<BusyWallCanceledMessage>());
        InitializeBusyWorkUI();                    

        _imageCodecService = Ioc.Default.GetService<IImageCodecService>();
        InitializeImageCodecExtensions();


        InitializeInAppNotification();
    }

    #region InAppNotification

    private void InitializeInAppNotification()
    {
        _messenger.Register<InAppNotificationRequestMessage>(this, (r, m) => 
        {
            ShowNotification(m.Value);
        });

        AnimationBuilder.Create()
            .Opacity(0.0, duration: TimeSpan.FromMilliseconds(1))
            .Start(NotificationContainer);

        TimeSpan animationTuration = TimeSpan.FromSeconds(0.25);
        TimeSpan showingTime = TimeSpan.FromSeconds(2);
        TimeSpan hideTiming = animationTuration + showingTime + animationTuration;
        _notificationAnimationBuilder = AnimationBuilder.Create()
            .Opacity()
                .TimedKeyFrames(b => b
                    .KeyFrame(TimeSpan.FromSeconds(0), 0.0)
                    .KeyFrame(animationTuration, 1.0)
                    .KeyFrame(showingTime, 1.0)
                    .KeyFrame(hideTiming, 0.0)
                    )
            .Translation()
                .TimedKeyFrames(b => b
                    .KeyFrame(TimeSpan.FromSeconds(0), new Vector3(16, 0, 0))
                    .KeyFrame(animationTuration, new Vector3(0, 0, 0))
                    .KeyFrame(showingTime, new Vector3(0, 0, 0))
                    .KeyFrame(hideTiming, new Vector3(-16, 0, 0))                
                    )
            ;                
    }

    private AnimationBuilder _notificationAnimationBuilder;


    private readonly Queue<object> _notificationRequestedItems = new Queue<object>();
    private void ShowNotification(object content)
    {
        if (NotificationContentControl.Content == null)
        {
            PushShowingNotificationContent(content);
        }
        else
        {
            _notificationRequestedItems.Enqueue(content);
        }
    }

    
    private void PushShowingNotificationContent(object content)
    {
        NotificationContentControl.Content = content;
        _notificationAnimationBuilder.Start(NotificationContainer, () => 
        {
            NotificationContentControl.Content = null;
            ShowNextNotificationContent();
        });
    }

    private void ShowNextNotificationContent()
    {
        if (_notificationRequestedItems.TryDequeue(out object content))
        {
            PushShowingNotificationContent(content);
        }
    }

    #endregion InAppNotification


    #region Navigation

    public readonly static Type HomePageType = typeof(SourceStorageItemsPage);

    public static string HomePageName => HomePageType.Name;


    private readonly static ImmutableHashSet<Type> MenuPaneHiddenPageTypes = new Type[]
    {
        typeof(ImageViewerPage),
        typeof(EBookReaderPage),
        typeof(SettingsPage),
        typeof(FolderOrArchiveRestructurePage),
    }.ToImmutableHashSet();

    private readonly static ImmutableHashSet<Type> CanGoBackPageTypes = new Type[]
    {
        typeof(FolderListupPage),
        typeof(ImageListupPage),
        typeof(ImageViewerPage),
        typeof(EBookReaderPage),
        typeof(SearchResultPage),
        typeof(SettingsPage),
        typeof(FolderOrArchiveRestructurePage),
    }.ToImmutableHashSet();

    private readonly static ImmutableHashSet<Type> UniqueOnNavigtionStackPageTypes = new Type[]
    {
        typeof(ImageViewerPage),
        typeof(EBookReaderPage),
        typeof(SearchResultPage),
        typeof(FolderOrArchiveRestructurePage),
    }.ToImmutableHashSet();



    private readonly static ImmutableHashSet<Type> SelectionAvairablePageTypes = new Type[]
    {
        typeof(ImageListupPage),
        typeof(FolderListupPage),
    }.ToImmutableHashSet();

    private readonly Core.AsyncLock _navigationLock = new ();
    private bool _isForgetNavigationRequested = false;
    private List<INavigationParameters> BackParametersStack = new List<INavigationParameters>();
    private List<INavigationParameters> ForwardParametersStack = new List<INavigationParameters>();

    private bool _isFirstNavigation = true;
    private bool _nowChangingMenuItem = false;

    private void InitializeNavigation()
    {
        async Task<INavigationResult> NavigationAsyncInternal(NavigationRequestMessage m)
        {
            using var lockReleaser = await _navigationLock.LockAsync(CancellationToken.None);

            var (currentNavParam, prevNavParam) = GetNavigationParametersSet();

            try
            {
                if (m.IsForgetNavigaiton)
                {
                    _isForgetNavigationRequested = true;
                }

                var parameters = m.Parameters ?? new NavigationParameters();
                parameters.SetNavigationMode(NavigationMode.New);
                return await NavigateAsync(m.PageName, parameters, m.IsForgetNavigaiton is false);
            }
            catch
            {
                SetCurrentNavigationParameters(prevNavParam);
                SetCurrentNavigationParameters(currentNavParam);
                _isForgetNavigationRequested = false;
                throw;
            }
        }

        _messenger.Register<NavigationRequestMessage>(this, (r, m) => 
        {
            m.Reply(NavigationAsyncInternal(m));
        });

        ContentFrame.Navigated += Frame_Navigated;
        SystemNavigationManager.GetForCurrentView().BackRequested += App_BackRequested;

        // Navigation Handling
        Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
        Window.Current.CoreWindow.PointerPressed += CoreWindow_PointerPressed;

        _messenger.Register<BackNavigationRequestMessage>(this, (r, m) => 
        {                
            if (CanHandleBackRequest())
            {
                _ = HandleBackRequestAsync();
            }
        });

        _messenger.Register<RefreshNavigationRequestMessage>(this, (r, m) => 
        {
            RefreshCommand.Execute(null);
        });

        // ItemInvoke が動作しないことのワークアラウンドとして選択変更を使用
        MyNavigtionView.SelectionChanged += MyNavigtionView_SelectionChanged;
    }

    private void Frame_Navigated(object sender, NavigationEventArgs e)
    {
        if (e.NavigationMode == Windows.UI.Xaml.Navigation.NavigationMode.Refresh) { return; }

        var frame = (Frame)sender;


        // アプリメニュー表示の切替
        MyNavigtionView.IsPaneVisible = !MenuPaneHiddenPageTypes.Contains(e.SourcePageType);
        if (MyNavigtionView.IsPaneVisible)
        {
            var sourcePageTypeName = e.SourcePageType.Name;
            if (e.SourcePageType == typeof(FolderListupPage))
            {
                sourcePageTypeName = nameof(Views.SourceStorageItemsPage);
            }
            if (GetCurrentNavigationParameter().TryGetValue(PageNavigationConstants.AlbamPathKey, out _))
            {
                sourcePageTypeName = nameof(Views.AlbamListupPage);
            }
            var selectedMeuItemVM = ((List<object>)MyNavigtionView.MenuItemsSource).FirstOrDefault(x => (x as MenuItemViewModel)?.PageType == sourcePageTypeName);
            if (selectedMeuItemVM != null)
            {
                // 選択位置を変える際に選択時のコマンドが実行されないようにする
                _nowChangingMenuItem = true;
                try
                {
                    // 選択変更時にフォーカスがページコンテンツからメニュー項目に奪われないようにする
                    MyNavigtionView.IsEnabled = false;
                    MyNavigtionView.SelectedItem = selectedMeuItemVM;
                    MyNavigtionView.IsEnabled = true;
                }
                catch { }
                _nowChangingMenuItem = false;
            }
        }

        // 選択中として表示するメニュー項目
        if (e.SourcePageType == typeof(SearchResultPage)
            || frame.BackStack.Any(x => x.SourcePageType == typeof(SearchResultPage)))
        {
            MyNavigtionView.SelectedItem = null;
        }

        // 複数選択ボタンの有効無効
        SelectionStartButton.IsEnabled = SelectionAvairablePageTypes.Contains(e.SourcePageType);

        // 戻れない設定のページではバックナビゲーションボタンを非表示に切り替え
        var isCanGoBackPage = CanGoBackPageTypes.Contains(e.SourcePageType);
        SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
            isCanGoBackPage
            ? AppViewBackButtonVisibility.Visible
            : AppViewBackButtonVisibility.Collapsed
            ;
        
        //BackCommand.NotifyCanExecuteChanged();


        // 戻れない設定のページに到達したら Frame.BackStack から不要なPageEntryを削除する
        if (_isForgetNavigationRequested)
        {
            _isForgetNavigationRequested = false;

            foreach (var entry in ContentFrame.BackStack.ToArray())
            {
                ContentFrame.BackStack.Remove(entry);
            }
            BackParametersStack.Clear();
            foreach (var entry in ContentFrame.ForwardStack.ToArray())
            {
                ContentFrame.ForwardStack.Remove(entry);
            }
            ForwardParametersStack.Clear();

            ContentFrame.BackStack.Add(new PageStackEntry(HomePageType, null, PageTransitionHelper.MakeNavigationTransitionInfoFromPageName(HomePageName)));
            BackParametersStack.Add(new NavigationParameters());

            SaveNaviagtionParameters();
        }
        else if (!isCanGoBackPage)
        {
            ContentFrame.BackStack.Clear();
            BackParametersStack.Clear();

            if (e.NavigationMode == Windows.UI.Xaml.Navigation.NavigationMode.New)
            {
                ContentFrame.ForwardStack.Clear();
                ForwardParametersStack.Clear();
            }

            

            SaveNaviagtionParameters();
        }
        else if (!_isFirstNavigation)
        {
            // ここのFrame_Navigatedが呼ばれた後にViewModel側のNavigatingToが呼ばれる
            // 順序重要
            var (currentNavParam, prevNavParam) = GetNavigationParametersSet();

           
            // ナビゲーションスタック上に一つしか存在してはいけないページの場合
            {
                bool rememberBackStack = true;
                if (UniqueOnNavigtionStackPageTypes.Contains(e.SourcePageType)
                    && frame.BackStack.LastOrDefault() is not null and var lastNavigatedPageEntry
                    && e.SourcePageType == lastNavigatedPageEntry.SourcePageType
                    )
                {
                    frame.BackStack.RemoveAt(frame.BackStackDepth - 1);

                    if (BackParametersStack.Any())
                    {
                        BackParametersStack.RemoveAt(frame.BackStackDepth - 1);
                    }

                    rememberBackStack = false;
                }


                if (e.NavigationMode is not Windows.UI.Xaml.Navigation.NavigationMode.New)
                {
                    rememberBackStack = false;
                }

                if (rememberBackStack)
                {
                    if (e.NavigationMode is Windows.UI.Xaml.Navigation.NavigationMode.New)
                    {
                        ForwardParametersStack.Clear();
                    }

                    var prevParameters = new NavigationParameters();
                    if (prevNavParam != null)
                    {
                        foreach (var pair in prevNavParam)
                        {
                            if (pair.Key == PageNavigationConstants.Restored) { continue; }
                            if (pair.Key == TsubameViewer.Services.Navigation.NavigationParametersExtensions.NavigationModeKey) { continue; }

                            prevParameters.Add(pair.Key, pair.Value);
                        }
                    }

                    BackParametersStack.Add(prevParameters);
                }
            }

            // Listup系ページのみ、同一パスのページをひとつずつまでしか持たせないようにする
            // 同一パスをImageListupPageとFolderListupPageで交互に行き来した場合に
            // FolderListupPageが２回目に現れたタイミングで１回目のImageListupPageとFOlderListupPageのバックスタック要素を削除する
            {
                if (e.NavigationMode == Windows.UI.Xaml.Navigation.NavigationMode.New
                    && frame.BackStack.Count >= 3
                    && e.SourcePageType == typeof(FolderListupPage)
                    && frame.BackStack.TakeLast(2).All(x => x.SourcePageType == typeof(FolderListupPage) || x.SourcePageType == typeof(ImageListupPage))
                    && currentNavParam != null && currentNavParam.TryGetValue(PageNavigationConstants.GeneralPathKey, out string currentNavigationPathParameter)
                    && BackParametersStack.TakeLast(2).All(x => x.TryGetValue(PageNavigationConstants.GeneralPathKey, out string backStackEntryPathparameter) && backStackEntryPathparameter == currentNavigationPathParameter)
                    )
                {
                    foreach (var remove in frame.BackStack.TakeLast(2).ToArray())
                    {
                        frame.BackStack.Remove(remove);
                        BackParametersStack.RemoveAt(BackParametersStack.Count - 1);
                    }
                }
            }                

            SaveNaviagtionParameters();
        }

        _isFirstNavigation = false;
    }

    private async Task<INavigationResult> NavigateAsync(string pageName, INavigationParameters parameters, bool isNavigationStackEnabled = true)
    {
        var viewType = _viewLocator.ResolveView(pageName);

        SetCurrentNavigationParameters(parameters);

        var prevPage = ContentFrame.Content as Page;
        var options = new FrameNavigationOptions() { IsNavigationStackEnabled = isNavigationStackEnabled, TransitionInfoOverride = PageTransitionHelper.MakeNavigationTransitionInfoFromPageName(pageName) };
        var result = ContentFrame.Navigate(viewType, parameters, options.TransitionInfoOverride);

        if (result)
        {
            var currentPage = ContentFrame.Content as Page;
            return await HandleViewModelNavigation(prevPage?.DataContext as INavigationAware, currentPage?.DataContext as INavigationAware, parameters);
        }
        else
        {
            return new NavigationResult() { IsSuccess = false, Exception = new Exception("failed navigation with unknown error. also check Xaml.") };
        }
    }

    private async Task<NavigationResult> HandleViewModelNavigation(INavigationAware fromPageVM, INavigationAware toPageVM, INavigationParameters parameters)
    {
        try
        {
            if (fromPageVM != null)
            {
                fromPageVM.OnNavigatedFrom(parameters);
            }
        }
        catch (Exception ex)
        {
            return new NavigationResult() { IsSuccess = false, Exception = ex };
        }

        try
        {
            if (toPageVM != null)
            {
                toPageVM.OnNavigatedTo(parameters);
                await toPageVM.OnNavigatedToAsync(parameters);
            }
        }
        catch (Exception ex)
        {
            return new NavigationResult() { IsSuccess = false, Exception = ex };
        }

        return new NavigationResult() { IsSuccess = true };
    }


    private void MyNavigtionView_SelectionChanged(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewSelectionChangedEventArgs args)
    {
        if (_nowChangingMenuItem) { return; }

        if (args.SelectedItem != null)
        {
            _vm.OpenMenuItemCommand.Execute(args.SelectedItem);
        }
    }

    private RelayCommand _RefreshCommand;
    public RelayCommand RefreshCommand =>
        _RefreshCommand ??= new RelayCommand(
            () => _ = HandleRefreshReqest()
            );


    #endregion

    #region Back/Forward Navigation

    // デッドロックさせないようにFireAndForgetで実行
    public async void RestoreNavigationStack()
    {
        var navigationManager = _vm.RestoreNavigationManager;


        try
        {
            //using (await _navigationLock.LockAsync(CancellationToken.None))
            {
                var currentEntry = navigationManager.GetCurrentNavigationEntry();
                if (currentEntry == null)
                {
                    Debug.WriteLine("[NavigationRestore] skip restore page.");
                    await ResetNavigationAsync();
                    return;
                }

                var backStack = await navigationManager.GetBackNavigationEntriesAsync();
                if (CanGoBackPageTypes.Any(x => x.Name == currentEntry.PageName)
                    && (backStack == null || backStack.Length == 0))
                {
                    // 戻るナビゲーションが必要なページでバックナビゲーションパラメータが存在しなかった場合はホーム画面に戻れるようにしておく
                    backStack = new PageEntry[] { new PageEntry(HomePageName) };
                }

                var currentNavParameters = MakeNavigationParameter(currentEntry.Parameters);
                if (!currentNavParameters.ContainsKey(PageNavigationConstants.Restored))
                {
                    currentNavParameters.Add(PageNavigationConstants.Restored, string.Empty);
                }

                var result = await _messenger.NavigateAsync(currentEntry.PageName, currentNavParameters);
                if (!result.IsSuccess)
                {
                    await Task.Delay(50);
                    Debug.WriteLine("[NavigationRestore] Failed restore CurrentPage: " + currentEntry.PageName);
                    await ResetNavigationAsync();
                    return;
                }

                Debug.WriteLine($"[NavigationRestore] Restored CurrentPage: {currentEntry.PageName} {string.Join(',', currentEntry.Parameters?.Select(x => $"{x.Key}={x.Value}") ?? Enumerable.Empty<string>())}");

                if (currentEntry.PageName == HomePageName)
                {
                    return;
                }

                foreach (var backNavItem in backStack)
                {
                    var pageType = Type.GetType($"TsubameViewer.Views.{backNavItem.PageName}");
                    var parameters = MakeNavigationParameter(backNavItem.Parameters);
                    ContentFrame.BackStack.Add(new PageStackEntry(pageType, parameters, PageTransitionHelper.MakeNavigationTransitionInfoFromPageName(backNavItem.PageName)));
                    BackParametersStack.Add(parameters);

                    Debug.WriteLine($"[NavigationRestore] Restored BackStackPage: {backNavItem.PageName} {string.Join(',', backNavItem.Parameters.Select(x => $"{x.Key}={x.Value}"))}");
                }

                //var forwardStack = await navigationManager.GetForwardNavigationEntriesAsync();
                //{
                //    if (forwardStack != null)
                //    {
                //        foreach (var forwardNavItem in forwardStack)
                //        {
                //            var pageType = Type.GetType($"TsubameViewer.Views.{forwardNavItem.PageName}");
                //            var parameters = MakeNavigationParameter(forwardNavItem.Parameters);
                //            ContentFrame.ForwardStack.Add(new PageStackEntry(pageType, parameters, new SuppressNavigationTransitionInfo()));
                //            ForwardParametersStack.Add(parameters);
                //            Debug.WriteLine("[NavigationRestore] Restored BackStackPage: " + forwardNavItem.PageName);
                //        }
                //    }
                //}
            }
        }
        catch
        {
            Debug.WriteLine("[NavigationRestore] failed restore current page. ");

            await ResetNavigationAsync();
        }            
    }

    private async Task ResetNavigationAsync()
    {
        BackParametersStack.Clear();
        ForwardParametersStack.Clear();
        ContentFrame.BackStack.Clear();
        ContentFrame.ForwardStack.Clear();

        await _messenger.NavigateAsync(HomePageName);
        SaveNaviagtionParameters();
    }

    // デッドロックを防ぐために常にFireAndForgetで実行させる
    async void SaveNaviagtionParameters()
    {
        using (await _navigationLock.LockAsync(CancellationToken.None))
        {
            var currentNavigationParameter = _currentNavigationParameters?.Clone();
            // ナビゲーション状態の保存
#if DEBUG
            if (currentNavigationParameter is not null)
            {
                Debug.WriteLine($"[NavigationRestore] Save CurrentPage: {ContentFrame.CurrentSourcePageType.Name} {string.Join(',', currentNavigationParameter.Select(x => $"{x.Key}={x.Value}"))}");
            }
            else
            {
                Debug.WriteLine($"[NavigationRestore] Save CurrentPage: {ContentFrame.CurrentSourcePageType.Name}");
            }
#endif

            _vm.RestoreNavigationManager.SetCurrentNavigationEntry(MakePageEnetry(ContentFrame.CurrentSourcePageType, currentNavigationParameter));
            {
                PageEntry[] backNavigationPageEntries = new PageEntry[BackParametersStack.Count];
                for (var backStackIndex = 0; backStackIndex < BackParametersStack.Count; backStackIndex++)
                {
                    var parameters = BackParametersStack[backStackIndex];
                    var stackEntry = ContentFrame.BackStack[backStackIndex];
                    backNavigationPageEntries[backStackIndex] = MakePageEnetry(stackEntry.SourcePageType, parameters);
                    Debug.WriteLine($"[NavigationRestore] Save BackStackPage: {backNavigationPageEntries[backStackIndex].PageName} {string.Join(',', backNavigationPageEntries[backStackIndex].Parameters.Select(x => $"{x.Key}={x.Value}"))}");
                }
                await _vm.RestoreNavigationManager.SetBackNavigationEntriesAsync(backNavigationPageEntries);
            }
            /*
            {
                PageEntry[] forwardNavigationPageEntries = new PageEntry[ForwardParametersStack.Count];
                for (var forwardStackIndex = 0; forwardStackIndex < ForwardParametersStack.Count; forwardStackIndex++)
                {
                    var parameters = ForwardParametersStack[forwardStackIndex];
                    var stackEntry = ContentFrame.ForwardStack[forwardStackIndex];
                    forwardNavigationPageEntries[forwardStackIndex] = MakePageEnetry(stackEntry.SourcePageType, parameters);
                    Debug.WriteLine("[NavigationRestore] Save ForwardStackPage: " + forwardNavigationPageEntries[forwardStackIndex].PageName);
                }
                await _viewModel.RestoreNavigationManager.SetForwardNavigationEntriesAsync(forwardNavigationPageEntries);
            }
            */
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
    
    private void SetCurrentNavigationParameters(INavigationParameters parameters)
    {
        if (parameters?.GetNavigationMode() == NavigationMode.Refresh) { return; }

        _prevNavigationParameters = _currentNavigationParameters;
        _currentNavigationParameters = parameters;
    }

    private (INavigationParameters Current, INavigationParameters Prev) GetNavigationParametersSet()
    {
        return (_currentNavigationParameters, _prevNavigationParameters);
    }


    INavigationParameters _prevNavigationParameters;
    INavigationParameters _currentNavigationParameters;

    public NavigationParameters GetCurrentNavigationParameter()
    {
        return _currentNavigationParameters?.Clone() ?? new NavigationParameters();
    }


    static PageEntry MakePageEnetry(Type pageType, INavigationParameters parameters)
    {
        return new PageEntry(pageType.Name, parameters);
    }

    bool CanHandleBackRequest()
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

        var data = new BackNavigationRequestingMessageData();
        _messenger.Send<BackNavigationRequestingMessage>(new(data));            
        if (data.IsHandled) { return false; }

        return ContentFrame.CanGoBack;
    }

    async Task HandleBackRequestAsync()
    {
        bool isRequestReset = false;
        {
            var lockReleaser = await _navigationLock.LockAsync(CancellationToken.None);
            try
            {
                if (ContentFrame.CanGoBack)
                {
                    if (_isForgetNavigationRequested)
                    {
                        _isForgetNavigationRequested = false;
                    }
                    var lastNavigationParameters = BackParametersStack.LastOrDefault();

                    if (lastNavigationParameters != null)
                    {
                        var lastNavigationParametersSet = GetNavigationParametersSet();
                        var parameters = GetCurrentNavigationParameter();    // GoBackAsyncを呼ぶとCurrentNavigationParametersが入れ替わる。呼び出し順に注意。

                        _currentNavigationParameters = lastNavigationParameters;
                        _prevNavigationParameters = BackParametersStack.Count >= 2 ? BackParametersStack.TakeLast(2).FirstOrDefault() : null;

                        BackParametersStack.Remove(lastNavigationParameters);
                        ForwardParametersStack.Add(parameters);
                        var prevPage = ContentFrame.Content as Page;
                        try
                        {
                            ContentFrame.GoBack();
                            lastNavigationParameters.SetNavigationMode(NavigationMode.Back);
                            var currentPage = ContentFrame.Content as Page;
                            await HandleViewModelNavigation(prevPage?.DataContext as INavigationAware, currentPage?.DataContext as INavigationAware, lastNavigationParameters);
                        }
                        catch
                        {
                            _currentNavigationParameters = lastNavigationParametersSet.Current;
                            _prevNavigationParameters = lastNavigationParametersSet.Prev;
                        }
                    }
                    else
                    {
                        isRequestReset = true;
                    }
                }
            }
            catch
            {
                isRequestReset = true;
            }
            finally
            {
                lockReleaser.Dispose();
            }
        }
        
        if (isRequestReset)
        {
            await ResetNavigationAsync();
        }
    }

    bool CanHandleForwardRequest()
    {
        if (NowShowingBusyWork)
        {
            CancelBusyWorkCommand.Execute(null);
            return false;
        }

        return ContentFrame.CanGoForward;
    }

    async Task HandleForwardRequest()
    {
        using var lockReleaser = await _navigationLock.LockAsync(CancellationToken.None);

        if (ContentFrame.CanGoForward)
        {
            var forwardNavigationParameters = ForwardParametersStack.Last();
            var lastNavigationParametersSet = GetNavigationParametersSet();
            var parameters = GetCurrentNavigationParameter(); // GoForwardAsyncを呼ぶとCurrentNavigationParametersが入れ替わる。呼び出し順に注意。

            {
                ForwardParametersStack.Remove(forwardNavigationParameters);
                BackParametersStack.Add(parameters);
            }

            _currentNavigationParameters = forwardNavigationParameters;
            _prevNavigationParameters = lastNavigationParametersSet.Current;

            var prevPage = ContentFrame.Content as Page;
            try
            {
                ContentFrame.GoForward();
                forwardNavigationParameters.SetNavigationMode(NavigationMode.Forward);
                var currentPage = ContentFrame.Content as Page;
                await HandleViewModelNavigation(prevPage?.DataContext as INavigationAware, currentPage?.DataContext as INavigationAware, forwardNavigationParameters);
            }
            catch
            {
                _currentNavigationParameters = lastNavigationParametersSet.Current;
                _prevNavigationParameters = lastNavigationParametersSet.Prev;
            }
        }
    }



    private async Task HandleRefreshReqest()
    {
        var parameters = _currentNavigationParameters.Clone();
        parameters.SetNavigationMode(NavigationMode.Refresh);
        var currentPage = ContentFrame.Content as Page;
        await HandleViewModelNavigation(null, currentPage?.DataContext as INavigationAware, parameters);
    }

    private void CoreWindow_PointerPressed(CoreWindow sender, PointerEventArgs args)
    {
        if (args.KeyModifiers == Windows.System.VirtualKeyModifiers.None
            && args.CurrentPoint.Properties.IsXButton1Pressed
            )
        {
            if (CanHandleBackRequest())
            {
                args.Handled = true;
                Debug.WriteLine("back navigated with Pointer Back pressed");

                _ = HandleBackRequestAsync();
            }
        }
        else if (args.KeyModifiers == Windows.System.VirtualKeyModifiers.None
            && args.CurrentPoint.Properties.IsXButton2Pressed
            )
        {
            if (CanHandleForwardRequest())
            {
                args.Handled = true;
                Debug.WriteLine("forward navigated with Pointer Forward pressed");

                _ = HandleForwardRequest();
            }
        }
    }

    private void CoreWindow_KeyDown(CoreWindow sender, KeyEventArgs args)
    {
        if (args.VirtualKey == Windows.System.VirtualKey.GoBack)
        {
            if (CanHandleBackRequest())
            {
                args.Handled = true;
                Debug.WriteLine("back navigated with VirtualKey.Back pressed");

                _ = HandleBackRequestAsync();
            }
        }
        else if (args.VirtualKey == Windows.System.VirtualKey.GoForward)
        {
            if (CanHandleForwardRequest())
            {
                args.Handled = true;
                Debug.WriteLine("forward navigated with VirtualKey.Back pressed");

                _ = HandleForwardRequest();
            }
        }
    }

    private void App_BackRequested(object sender, BackRequestedEventArgs e)
    {
        if (CanHandleBackRequest())
        {
            Debug.WriteLine("back navigated with SystemNavigationManager.BackRequested");
            
            _ = HandleBackRequestAsync();
        }

        // Note: 強制的にハンドルしないとXboxOneやタブレットでアプリを閉じる動作に繋がってしまう
        e.Handled = true;
    }





    #endregion

    #region Search Box

    private void InitializeSearchBox()
    {
        AutoSuggestBox.Loaded += PrimaryWindowCoreLayout_Loaded;
    }


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



    private void AutoSuggestBox_AccessKeyInvoked(UIElement sender, AccessKeyInvokedEventArgs args)
    {
        (sender as Control).Focus(FocusState.Keyboard);
        args.Handled = true;
    }

    private void KeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        (args.Element as Control).Focus(FocusState.Keyboard);
        args.Handled = true;
    }

    #endregion

    #region Busy Work

    private void InitializeBusyWorkUI()
    {
        _messenger.Register<BusyWallStartRequestMessage>(this, (r, m) =>
        {
            _AnimationCancelTimer.Start();
            VisualStateManager.GoToState(this, VS_ShowBusyWall.Name, true);
        });

        _messenger.Register<BusyWallExitRequestMessage>(this, (r, m) =>
        {
            VisualStateManager.GoToState(this, VS_HideBusyWall.Name, true);
        });

        // 
        _AnimationCancelTimer.IsRepeating = false;
        _AnimationCancelTimer.Interval = _BusyWallDisplayDelayTime;
        _AnimationCancelTimer.Tick += (_, _) =>
        {
            var animation = ConnectedAnimationService.GetForCurrentView().GetAnimation(PageTransitionHelper.ImageJumpConnectedAnimationName);
            if (animation != null)
            {
                animation.Cancel();
            }
        };
    }



    private bool NowShowingBusyWork => BusyWall.IsHitTestVisible;

    private RelayCommand CancelBusyWorkCommand { get; }



    #endregion

    #region Theme

    private void InitializeThemeChangeRequest()
    {
        _messenger.Register<ThemeChangeRequestMessage>(this, (r, m) => 
        {
            SetTheme(m.Value);
        });

        SetTheme(_vm.ApplicationSettings.Theme);
    }

    public void SetTheme(Core.Models.ApplicationTheme applicationTheme)
    {
#if WINDOWS_UWP
        if (applicationTheme == Core.Models.ApplicationTheme.Default)
        {
            applicationTheme = SystemThemeHelper.GetSystemTheme();
        }
#endif

        this.RequestedTheme = applicationTheme switch
        {
            Core.Models.ApplicationTheme.Light => ElementTheme.Light,
            Core.Models.ApplicationTheme.Dark => ElementTheme.Dark,
            Core.Models.ApplicationTheme.Default => ElementTheme.Default,
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
                    || (item is StorageFile file && string.IsNullOrEmpty(file.Path) is false && SupportedFileTypesHelper.IsSupportedFileExtension(file.FileType))
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
                token = await _vm.SourceStorageItemsRepository.AddFileTemporaryAsync(storageItem, SourceOriginConstants.DragAndDrop);

                openStorageItem = storageItem;
            }
            
            if (dropItems.Count == 1)
            {
                if (openStorageItem is StorageFolder folder)
                {
                    FolderContainerTypeManager folderContainerTypeManager = Ioc.Default.GetRequiredService<FolderContainerTypeManager>();
                    var containerType = await _messenger.WorkWithBusyWallAsync(async ct => await folderContainerTypeManager.GetFolderContainerTypeWithCacheAsync(folder, ct), CancellationToken.None);
                    if (containerType == FolderContainerType.Other)
                    {
                        var result = await _messenger.NavigateAsync(nameof(FolderListupPage), new NavigationParameters((PageNavigationConstants.GeneralPathKey, openStorageItem.Path)));
                    }
                    else
                    {
                        var result = await _messenger.NavigateAsync(nameof(ImageListupPage), new NavigationParameters((PageNavigationConstants.GeneralPathKey, openStorageItem.Path)));
                    }
                }
                else if (openStorageItem is StorageFile fileItem)
                {
                    if (SupportedFileTypesHelper.IsSupportedArchiveFileExtension(fileItem.FileType)
                        || SupportedFileTypesHelper.IsSupportedImageFileExtension(fileItem.FileType)
                        )
                    {
                        await _messenger.NavigateAsync(nameof(Views.ImageViewerPage), new NavigationParameters((PageNavigationConstants.GeneralPathKey, openStorageItem.Path)));
                    }
                    else if (SupportedFileTypesHelper.IsSupportedEBookFileExtension(fileItem.FileType))
                    {
                        await _messenger.NavigateAsync(nameof(Views.EBookReaderPage), new NavigationParameters((PageNavigationConstants.GeneralPathKey, openStorageItem.Path)));
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


    #region Selection


    void InitializeSelection()
    {
        _messenger.Register<MenuDisplayMessage>(this, (r, m) => 
        {
            MyNavigtionView.IsPaneVisible = m.Value == Visibility.Visible;
        });
    }


    #endregion




    #region Image codec Extension


    private readonly IImageCodecService _imageCodecService;

    void InitializeImageCodecExtensions()
    {
        _messenger.Register<RequireInstallImageCodecExtensionMessage>(this, (r, m) =>
        {
            void TeachTooltip_Closed(Microsoft.UI.Xaml.Controls.TeachingTip sender, Microsoft.UI.Xaml.Controls.TeachingTipClosedEventArgs args)
            {
                sender.Closed -= TeachTooltip_Closed;
                sender.ActionButtonClick -= TeachTooltip_ActionButtonClick;
                RootGrid.Children.Remove(sender);
            }

            void TeachTooltip_ActionButtonClick(Microsoft.UI.Xaml.Controls.TeachingTip sender, object args)
            {
                _ = _imageCodecService.OpenImageCodecExtensionStorePageAsync(m.Value);

                sender.IsOpen = false;
                MenuRightCommandBar.IsOpen = true;
            }

            var teachTooltip = new Microsoft.UI.Xaml.Controls.TeachingTip()
            {
                PreferredPlacement = Microsoft.UI.Xaml.Controls.TeachingTipPlacementMode.Top
            };
            teachTooltip.Content = new TextBlock()
            {
                Text = "OpenImageCodecExtensionStorePageWithFileType".Translate(m.Value),
                Margin = new Thickness(16),
                TextWrapping = TextWrapping.Wrap,
            };
            teachTooltip.Closed += TeachTooltip_Closed;
            teachTooltip.ActionButtonClick += TeachTooltip_ActionButtonClick;
            teachTooltip.ActionButtonContent = "Open".Translate();
            teachTooltip.CloseButtonContent = "Cancel".Translate();
            RootGrid.Children.Add(teachTooltip);
            teachTooltip.IsOpen = true;
        });
    }

    #endregion

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
