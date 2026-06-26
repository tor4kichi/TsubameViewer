using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using CommunityToolkit.WinUI.Helpers;
using DryIoc;
using DryIoc.ImTools;
using Fluent.Icons;
using I18NPortable;
using Microsoft.Toolkit.Uwp.UI;
using Microsoft.Toolkit.Uwp.UI.Animations;
using R3;
using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Contracts.Navigation;
using TsubameViewer.Contracts.Notification;
using TsubameViewer.Contracts.Services;
using TsubameViewer.Core.Helpers;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.Navigation;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.Helpers;
using TsubameViewer.Services;
using TsubameViewer.Services.Navigation;
using TsubameViewer.ViewModels;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.ViewModels.SourceFolders;
using TsubameViewer.Views.Helpers;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Services.Store;
using Windows.Storage;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
#nullable enable
namespace TsubameViewer.Views;

public interface ITitlebarContentAware
{
    DataTemplate? GetContent();
    R3.Observable<string> ObserveTitleChanged();
}

[ObservableObject]
public sealed partial class AppShell : UserControl
{
    #region Purchase Cheer Addon

    [RelayCommand(CanExecute = nameof(IsStoreAvairable))]
    async Task PurchaseAddonAsync()
    {
        var service = Ioc.Default.GetService<PurchaseAddonService>();
        if (service == null) { return; }
        var result = await service.PurchaseCheerAsync();        
        Debug.WriteLine(result);

        if (result is Windows.Services.Store.StorePurchaseStatus.Succeeded or Windows.Services.Store.StorePurchaseStatus.AlreadyPurchased)
        {
            PurchaseThanksMassageFlyout.ShowAt(FeedbackButton);
        }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PurchaseAddonCommand))]
    bool _isStoreAvairable;

    async Task InitialziePurchase()
    {
        var service = Ioc.Default.GetService<PurchaseAddonService>();
        if (service == null) { return; }

        if (string.IsNullOrEmpty(PurchaseConfirmFlyout_DescTextBlock.Text))
        {
            var info = await service.GetCheerAddonInfoAsync();
            if (info == null) { return; }
            PurchaseConfirmFlyout_DescTextBlock.Text = info?.Description ?? "";
            IsStoreAvairable = info != null;
        }
    }

    void ShowPurchaseConfirmFlyoutMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
    {
        PurchaseConfirmFlyout.ShowAt(FeedbackButton);
    }

    #endregion

    readonly AppShellViewModel _vm;
    readonly IMessenger _messenger;

    readonly DispatcherQueue _dispatcherQueue;
    readonly IViewLocator _viewLocator;
    readonly DispatcherQueueTimer _animationCancelTimer;
    readonly TimeSpan _busyWallDisplayDelayTime = PageNavigationConstants.BusyWallDisplayDelayTime;
    readonly List<object> _footerItemsForTop;
    readonly List<object> _footerItemsForLeft;

    Microsoft.UI.Xaml.Controls.NavigationViewPaneDisplayMode ToPaneDisplayMode(bool isAppMenuShowWithLeft)
    {
        return isAppMenuShowWithLeft 
            ? Microsoft.UI.Xaml.Controls.NavigationViewPaneDisplayMode.Left 
            : Microsoft.UI.Xaml.Controls.NavigationViewPaneDisplayMode.Top;
    }

    public AppShell(
        AppShellViewModel viewModel, 
        IMessenger messenger
        )
    {
        this.InitializeComponent();

        DataContext = _vm = viewModel;
        _messenger = messenger;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _viewLocator = Ioc.Default.GetRequiredService<IViewLocator>();
        InitializeNavigation();
        InitializeViewerFrameNavigation();
        InitializeThemeChangeRequest();
        InitializeSelection();

        _animationCancelTimer = _dispatcherQueue.CreateTimer();
        CancelBusyWorkCommand = new RelayCommand(() => _messenger.Send<BusyWallCanceledMessage>());
        InitializeBusyWorkUI();                    

        _imageCodecService = Ioc.Default.GetRequiredService<IImageCodecService>();
        InitializeImageCodecExtensions();

        InitializeInAppNotification();

        SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Collapsed;

        var coreTitleBar = CoreApplication.GetCurrentView().TitleBar;
        coreTitleBar.ExtendViewIntoTitleBar = true;
        coreTitleBar.IsVisibleChanged += CoreTitleBar_IsVisibleChanged;        

        var appView = ApplicationView.GetForCurrentView();
        appView.VisibleBoundsChanged += AppView_VisibleBoundsChanged;

        Loaded += AppShell_Loaded;

        InitialziePurchase().FireAndForgetSafe();

        _footerItemsForTop = new()
        {
            new MenuItemInvokeActionViewModel()
            {
                Tooltip = "StartMultiSelection".Translate(),
                Invoked = () => _vm.StartSelectionCommand.Execute(null),
                Icon = new FluentIconElement(FluentSymbol.Multiselect24),
            },
            new MenuItemInvokeActionViewModel()
            {
                Tooltip = "AddNewFolder".Translate(),
                Invoked = () => _vm.SourceChoiceCommand.Execute(null),
                Icon = new FluentIconElement(FluentSymbol.ImageAdd24),
            },
            new MenuItemInvokeActionViewModel()
            {
                Tooltip = "RefreshLatest".Translate(),
                Invoked = () => _vm.RefreshNavigationCommand.Execute(null),
                Icon = new FluentIconElement(FluentSymbol.ArrowSync24),
            },
            new MenuItemInvokeActionViewModel()
            {
                Tooltip = "Settings".Translate(),
                Invoked = () => _vm.OpenPageCommand.Execute(nameof(SettingsPage)),
                Icon = new FluentIconElement(FluentSymbol.Settings24),
            }
        };

        _footerItemsForLeft = new()
        {
            new MenuItemInvokeActionViewModel()
            {
                Title = "StartMultiSelection".Translate(),
                Invoked = () => _vm.StartSelectionCommand.Execute(null),
                Icon = new FluentIconElement(FluentSymbol.Multiselect24),
            },
            new MenuItemInvokeActionViewModel()
            {
                Title = "AddNewFolder".Translate(),
                Invoked = () => _vm.SourceChoiceCommand.Execute(null),
                Icon = new FluentIconElement(FluentSymbol.ImageAdd24),
            },
            new MenuItemInvokeActionViewModel()
            {
                Title = "RefreshLatest".Translate(),
                Invoked = () => _vm.RefreshNavigationCommand.Execute(null),
                Icon = new FluentIconElement(FluentSymbol.ArrowSync24),
            },
            new MenuItemInvokeActionViewModel()
            {
                Title = "Settings".Translate(),
                Invoked = () => _vm.OpenPageCommand.Execute(nameof(SettingsPage)),
                Icon = new FluentIconElement(FluentSymbol.Settings24),
            }
        };
    }

    void AppShell_Loaded(object sender, RoutedEventArgs e)
    {
        CoreApplicationViewTitleBar coreTitleBar =
            CoreApplication.GetCurrentView().TitleBar;
        var appView = ApplicationView.GetForCurrentView();
        UpdateTitleBarDisplay(coreTitleBar.IsVisible, appView.IsFullScreenMode);

        CheckAppPackageUpdateAsync().FireAndForgetSafe();
    }

    public bool NowShowTitlebarInFullScreen
    {
        get { return (bool)GetValue(NowShowTitlebarInFullScreenProperty); }
        set { SetValue(NowShowTitlebarInFullScreenProperty, value); }
    }

    public static readonly DependencyProperty NowShowTitlebarInFullScreenProperty =
        DependencyProperty.Register(nameof(NowShowTitlebarInFullScreen), typeof(bool), typeof(AppShell), new PropertyMetadata(false));


    void AppView_VisibleBoundsChanged(ApplicationView sender, object args)
    {
        CoreApplicationViewTitleBar coreTitleBar =
                    CoreApplication.GetCurrentView().TitleBar;
        UpdateTitleBarDisplay(coreTitleBar.IsVisible, sender.IsFullScreenMode);
    }

    void CoreTitleBar_IsVisibleChanged(CoreApplicationViewTitleBar sender, object args)
    {
        var appView = ApplicationView.GetForCurrentView();
        UpdateTitleBarDisplay(sender.IsVisible, appView.IsFullScreenMode);
    }



    void UpdateTitleBarDisplay(bool isDisplay, bool isFullScreen)
    {
        NowShowTitlebarInFullScreen = isDisplay && isFullScreen;
    }

    #region InAppNotification

    void InitializeInAppNotification()
    {
        _messenger.Register<InAppNotificationRequestMessage>(this, (r, m) => 
        {
            ShowNotification(m.Value);
        });

        AnimationBuilder.Create()
            .Opacity(0.0, duration: TimeSpan.FromMilliseconds(1))
            .Start(NotificationContainer);

        TimeSpan animationTuration = TimeSpan.FromSeconds(0.25);
        TimeSpan showingTime = TimeSpan.FromSeconds(3);
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

    AnimationBuilder? _notificationAnimationBuilder;


    readonly Queue<object> _notificationRequestedItems = new Queue<object>();
    void ShowNotification(object content)
    {
        if (NotificationContentControl.Content == null
            && string.IsNullOrEmpty(NotificationTextBlock.Text))
        {
            PushShowingNotificationContent(content);
        }
        else
        {
            _notificationRequestedItems.Enqueue(content);
        }
    }

    
    void PushShowingNotificationContent(object content)
    {
        if (content is string str)
        {
            NotificationTextBlock.Text = str;
        }
        else
        {
            NotificationContentControl.Content = content;
        }
        _notificationAnimationBuilder?.Start(NotificationContainer, () => 
        {
            NotificationContentControl.Content = null;
            NotificationTextBlock.Text = "";
            ShowNextNotificationContent();
        });
    }

    void ShowNextNotificationContent()
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


    readonly static ImmutableHashSet<Type> _menuPaneHiddenPageTypes = new Type[]
    {
        typeof(ImageViewerPage),
        typeof(EBookViewerPage),
        typeof(MovieViewerPage),
        typeof(SettingsPage),
        typeof(FolderOrArchiveRestructurePage),
    }.ToImmutableHashSet();

    readonly static ImmutableHashSet<Type> _canGoBackPageTypes = new Type[]
    {
        typeof(FolderListupPage),
        typeof(ImageListupPage),
        typeof(ImageViewerPage),
        typeof(EBookViewerPage),
        typeof(MovieViewerPage),
        typeof(SearchResultPage),
        typeof(SettingsPage),
        typeof(FolderOrArchiveRestructurePage),
    }.ToImmutableHashSet();

    readonly static ImmutableHashSet<Type> _uniqueOnNavigtionStackPageTypes = new Type[]
    {
        typeof(ImageViewerPage),
        typeof(EBookViewerPage),
        typeof(MovieViewerPage),
        typeof(SearchResultPage),
        typeof(FolderOrArchiveRestructurePage),
    }.ToImmutableHashSet();



    readonly static ImmutableHashSet<Type> _selectionAvairablePageTypes = new Type[]
    {
        typeof(ImageListupPage),
        typeof(FolderListupPage),
    }.ToImmutableHashSet();


    readonly static ImmutableHashSet<Type> _openWithViewerFramePageTypes = new Type[]
    {
        typeof(ImageViewerPage),
        typeof(EBookViewerPage),
        typeof(MovieViewerPage),
        typeof(SettingsPage),
        typeof(FolderOrArchiveRestructurePage),
    }.ToImmutableHashSet();

    bool IsOpenWithViewerPageType(Type? pageType)
    {
        if (pageType == null) { return false; }
        return _openWithViewerFramePageTypes.Contains(pageType);
    }

    readonly Core.AsyncLock _navigationLock = new ();
    bool _isForgetNavigationRequested = false;
    List<INavigationParameters> _backParametersStack = new List<INavigationParameters>();
    List<INavigationParameters> _forwardParametersStack = new List<INavigationParameters>();

    bool _isFirstNavigation = true;
    
    void InitializeNavigation()
    {
        async Task<INavigationResult> NavigationAsyncInternal(NavigationRequestMessage m)
        {
            PerfomanceStopWatch sw = PerfomanceStopWatch.StartNew("NavigationAsyncInternal");
            try
            {
                using var lockReleaser = await _navigationLock.LockAsync(CancellationToken.None);

                var (currentNavParam, prevNavParam) = GetNavigationParametersSet();

                if (m.IsForgetNavigaiton)
                {
                    _isForgetNavigationRequested = true;
                }

                if (IsOpenWithViewerPageType(ViewerFrame.Content.GetType())
                    && _openWithViewerFramePageTypes.Any(x => x.Name.Equals(m.PageName, StringComparison.Ordinal)))
                {                    
                    //throw new InvalidOperationException("ViewerPage can only one exist on app navigation stack.");
                }

                var parameters = m.Parameters ?? new NavigationParameters();
                parameters.SetNavigationMode(NavigationMode.New);
                return await NavigateAsync(m.PageName, parameters, m.TransitionInfo, m.IsForgetNavigaiton is false);
            }
            catch (OperationCanceledException)
            {
                // ロールバックして前のページを表示
                await HandleBackRequestAsync();
                _isForgetNavigationRequested = false;
                throw;
            }
            catch (Exception ex) when (ex is FileNotFoundException or UnauthorizedAccessException)
            {
                // ファイルにアクセス出来ない例外の場合はホームページへ遷移
                var navigationParameters = new NavigationParameters();
                navigationParameters.SetNavigationMode(NavigationMode.New);
                await NavigateAsync(HomePageName, navigationParameters, null, false);
                _messenger.SendShowTextNotificationMessage("Notification_SourceStorageItemNotFound".Translate());
                throw;
            }
            sw.ElapsedWrite("Completed");
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
                HandleBackRequestAsync().FireAndForgetSafe();
            }
        });

        _messenger.Register<RefreshNavigationRequestMessage>(this, (r, m) => 
        {
            RefreshCommand.Execute(null);
        });        
    }

    void RefreshBackButton()
    {

    }

    void InitializeViewerFrameNavigation()
    {
        ViewerFrame.Navigate(typeof(EmptyPage));
        ViewerFrame.Navigated += async (s, e) =>
        {
            Debug.WriteLine($"ViewerFrame Navigate to : {e.SourcePageType.Name}");
            var frame = (Frame)s;
            // 常にEmptyPageに戻るように
            if (e.NavigationMode == NavigationMode.New 
                && frame.BackStack.Count >= 2)
            {
                frame.BackStack.RemoveAt(1);
            }
            frame.ForwardStack.Clear();
            if (IsOpenWithViewerPageType(e.SourcePageType))
            {
                frame.Visibility = Visibility.Visible;
                SetTitleContentForPrimary(frame);
                MyNavigationView.Visibility = Visibility.Collapsed;
                if (e.Content is Page page)
                {
                    page.Focus(FocusState.Programmatic);
                }
            }
            else
            {
                frame.Visibility = Visibility.Collapsed;
                MyNavigationView.Visibility = Visibility.Visible;
                if (ContentFrame.Content == null)
                {
                    await _messenger.NavigateAsync(HomePageName);
                }
                else
                {
                    SetTitleContentForPrimary(ContentFrame);
                }
            }

            GoBackButton.IsEnabled = CanHandleBackRequest();

            if (ViewerFrame.Content is Page viewerPage
                && viewerPage.GetType() is { } viewerPageType
                && IsOpenWithViewerPageType(viewerPageType)
                && _viewerNavigationParameters != null)
            {
                _vm.RestoreNavigationManager.SetViewerNavigationEntry(
                    MakePageEnetry(viewerPageType, _viewerNavigationParameters));
                Debug.WriteLine($"Save viewer page state: {viewerPageType.Name}");
            }
            else
            {
                _vm.RestoreNavigationManager.ClearViewerNavigationEntry();
                Debug.WriteLine($"Clear viewer page state");
            }
        };
    }

    IDisposable? _titlebarContentDisposable;
    void SetTitleContentForPrimary(Frame frame)
    {
        _titlebarContentDisposable?.Dispose();
        _titlebarContentDisposable = null;
        Window.Current.SetTitleBar(null);
        if (frame.Content is ITitlebarContentAware tbContent)
        {
            if (tbContent.GetContent() is { } content)
            {
                TitlebarContent.ContentTemplate = content;
                TitlebarContent.Content = (frame.Content as FrameworkElement)?.DataContext;
                TitlebarContent_Nallow.ContentTemplate = content;
                TitlebarContent_Nallow.Content = (frame.Content as FrameworkElement)?.DataContext;

            }

            if (tbContent.ObserveTitleChanged() is { } observe)
            {
                var page = (Page)frame.Content;
                _titlebarContentDisposable = observe.Subscribe(title =>
                {
                    title ??= "";
                    WindowTitleTextBlock.Text = title;
                    ApplicationView.GetForCurrentView().Title = title;
                });
            }
        }
        else
        {
            TitlebarContent.ContentTemplate = null;
            TitlebarContent.Content = null;
            TitlebarContent_Nallow.ContentTemplate = null;
            TitlebarContent_Nallow.Content = null;
        }
        Window.Current.SetTitleBar(TitlebarBG);
    }

    void Frame_Navigated(object sender, NavigationEventArgs e)
    {
        if (e.NavigationMode == Windows.UI.Xaml.Navigation.NavigationMode.Refresh) { return; }

        var frame = (Frame)sender;

        if (!IsOpenWithViewerPageType(ViewerFrame.Content?.GetType()))
        {
            SetTitleContentForPrimary(frame);
        }

        // アプリメニュー表示の切替
        //MyNavigationView.IsPaneVisible = !MenuPaneHiddenPageTypes.Contains(e.SourcePageType);
        if (MyNavigationView.IsPaneVisible)
        {
            var sourcePageTypeName = e.SourcePageType.Name;
            var currentParameters = GetCurrentNavigationParameter();

            static bool IsCurrentPageMatchMenuItem(MenuItemViewModel menuItemVM, string? parametersPath)
            {
                if (menuItemVM.Parameters == null && parametersPath == null)
                {
                    return true;
                }

                if (!string.IsNullOrEmpty(parametersPath)
                    && (menuItemVM.Parameters?.TryGetValue(PageNavigationConstants.GeneralPathKey, out string menuItemEscapedPath) ?? false))
                {
                    var (menuItemPath, _) = PageNavigationConstants.ParseStorageItemId(Uri.UnescapeDataString(menuItemEscapedPath));
                    return parametersPath!.StartsWith(menuItemPath);
                }
                else { return false; }
            }

            // 幅優先探索でMenuItemsを走査する
            // 
            Queue<MenuItemViewModel> queue = new((MyNavigationView.MenuItemsSource as IList<object>).Cast<MenuItemViewModel>());
            string? parameterPathEscaped = null;
            bool hasParameters = currentParameters?.TryGetValue(PageNavigationConstants.GeneralPathKey, out parameterPathEscaped) ?? false;
            (string? parametersPath, _) = hasParameters ? PageNavigationConstants.ParseStorageItemId(Uri.UnescapeDataString(parameterPathEscaped)) : (null, null);
            while (queue.TryDequeue(out var menuItemVM))
            {
                if (menuItemVM.PageType == sourcePageTypeName
                    && (!hasParameters || IsCurrentPageMatchMenuItem(menuItemVM, parametersPath))
                    )
                {
                    MyNavigationView.SelectedItem = menuItemVM;
                    break;
                }
                
                if (sourcePageTypeName == nameof(ImageListupPage)
                    && hasParameters 
                    && IsCurrentPageMatchMenuItem(menuItemVM, parametersPath)
                    )
                {
                    MyNavigationView.SelectedItem = menuItemVM;
                    break;
                }

                if (menuItemVM is MenuSubItemViewModel subItemVM
                    && subItemVM.Items.Any()
                    )
                {
                    foreach (var childMenuItem in subItemVM.Items)
                    {
                        if (childMenuItem is MenuItemViewModel childMenuItemVM)
                        {
                            queue.Enqueue(childMenuItemVM);
                        }
                    }
                }
            }            
        }

        // 選択中として表示するメニュー項目
        if (e.SourcePageType == typeof(SearchResultPage)
            || frame.BackStack.Any(x => x.SourcePageType == typeof(SearchResultPage)))
        {
            MyNavigationView.SelectedItem = null;
        }


        // 戻れない設定のページではバックナビゲーションボタンを非表示に切り替え
        GoBackButton.IsEnabled = CanHandleBackRequest();
        

        // 戻れない設定のページに到達したら Frame.BackStack から不要なPageEntryを削除する
        if (_isForgetNavigationRequested)
        {
            _isForgetNavigationRequested = false;

            foreach (var entry in ContentFrame.BackStack.ToArray())
            {
                ContentFrame.BackStack.Remove(entry);
            }
            _backParametersStack.Clear();
            foreach (var entry in ContentFrame.ForwardStack.ToArray())
            {
                ContentFrame.ForwardStack.Remove(entry);
            }
            _forwardParametersStack.Clear();

            ContentFrame.BackStack.Add(new PageStackEntry(HomePageType, null, PageTransitionHelper.MakeNavigationTransitionInfoFromPageName(HomePageName)));
            _backParametersStack.Add(new NavigationParameters());

            SaveNaviagtionParameters();
        }
        else if (!GoBackButton.IsEnabled)
        {
            ContentFrame.BackStack.Clear();
            _backParametersStack.Clear();

            if (e.NavigationMode == Windows.UI.Xaml.Navigation.NavigationMode.New)
            {
                ContentFrame.ForwardStack.Clear();
                _forwardParametersStack.Clear();
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
                if (_uniqueOnNavigtionStackPageTypes.Contains(e.SourcePageType)
                    && frame.BackStack.LastOrDefault() is not null and var lastNavigatedPageEntry
                    && e.SourcePageType == lastNavigatedPageEntry.SourcePageType
                    )
                {
                    frame.BackStack.RemoveAt(frame.BackStackDepth - 1);

                    if (_backParametersStack.Any())
                    {
                        _backParametersStack.RemoveAt(_backParametersStack.Count - 1);
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
                        _forwardParametersStack.Clear();
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

                    _backParametersStack.Add(prevParameters);
                }
            }

            // Listup系ページのみ、同一パスのページをひとつずつまでしか持たせないようにする
            // 同一パスをImageListupPageとFolderListupPageで交互に行き来した場合に
            // FolderListupPageが２回目に現れたタイミングで１回目のImageListupPageとFOlderListupPageのバックスタック要素を削除する
            {
                if (e.NavigationMode == Windows.UI.Xaml.Navigation.NavigationMode.New
                    && frame.BackStack.Count >= 1
                    && (e.SourcePageType == typeof(FolderListupPage) || e.SourcePageType == typeof(ImageListupPage))
                    && frame.BackStack.TakeLast(1).All(x => x.SourcePageType == typeof(FolderListupPage) || x.SourcePageType == typeof(ImageListupPage))
                    && currentNavParam != null && currentNavParam.TryGetValue(PageNavigationConstants.GeneralPathKey, out string currentNavigationPathParameter)
                    && _backParametersStack.TakeLast(1).All(x => x.TryGetValue(PageNavigationConstants.GeneralPathKey, out string backStackEntryPathparameter) && backStackEntryPathparameter == currentNavigationPathParameter)
                    )
                {
                    foreach (var remove in frame.BackStack.TakeLast(1).ToArray())
                    {
                        frame.BackStack.Remove(remove);
                        _backParametersStack.RemoveAt(_backParametersStack.Count - 1);
                    }
                }
            }                

            SaveNaviagtionParameters();
        }

        _isFirstNavigation = false;
    }

    async Task<INavigationResult> NavigateAsync(string pageName, INavigationParameters parameters, NavigationTransitionInfo? transitionInfo = null,  bool isNavigationStackEnabled = true)
    {
        PerfomanceStopWatch sw = PerfomanceStopWatch.StartNew("NavigateAsync");
        var viewType = _viewLocator.ResolveView(pageName);
        Frame frame;        
        if (IsOpenWithViewerPageType(viewType))
        {
            frame = ViewerFrame;
            ViewerFrame.Visibility = Visibility.Visible;
            _viewerNavigationParameters = parameters;

        }
        else
        {
            frame = ContentFrame;
            SetCurrentNavigationParameters(parameters);
        }
        sw.ElapsedWrite("Before RotationNextCancellationTokenSource");
        var ct = RotationNextCancellationTokenSource(viewType);
        sw.ElapsedWrite("After RotationNextCancellationTokenSource");
        var prevPage = frame.Content as Page;
        var options = new FrameNavigationOptions() 
        {
            IsNavigationStackEnabled = isNavigationStackEnabled, 
            TransitionInfoOverride = isNavigationStackEnabled ? (transitionInfo ?? PageTransitionHelper.MakeNavigationTransitionInfoFromPageName(pageName)) : new SuppressNavigationTransitionInfo() 
        };
        var result = frame.Navigate(viewType, parameters, options.TransitionInfoOverride);

        if (result is false)
        {
            throw new InvalidOperationException($"Failed ContentFrame navigate to {pageName}.");
        }
        sw.ElapsedWrite("Before HandleViewModelNavigation");
        var page = frame.Content;
        var currentPage = page as Page;        
        var handleResult = await HandleViewModelNavigation(prevPage?.DataContext as INavigationAware, currentPage?.DataContext as INavigationAware, parameters, ct);
        sw.ElapsedWrite("After HandleViewModelNavigation");
        return handleResult;
    }


    CancellationToken RotationNextCancellationTokenSource(Type? pageType)
    {
        if (IsOpenWithViewerPageType(pageType))
        {
            _viewerNavigateCts?.Cancel();
            _viewerNavigateCts?.Dispose();
            _viewerNavigateCts = new CancellationTokenSource();
            return _viewerNavigateCts.Token;
        }
        else
        {
            _navigateCts?.Cancel();
            _navigateCts?.Dispose();
            _navigateCts = new CancellationTokenSource();
            return _navigateCts.Token;
        }
    }

    CancellationTokenSource? _viewerNavigateCts;
    CancellationTokenSource? _navigateCts;
    async Task<NavigationResult> HandleViewModelNavigation(INavigationAware? fromPageVM, INavigationAware? toPageVM, INavigationParameters parameters, CancellationToken ct)
    {
        if (fromPageVM != null)
        {
            fromPageVM.OnNavigatedFrom(parameters);
        }

        if (toPageVM != null)
        {
            toPageVM.OnNavigatedTo(parameters);
            await toPageVM.OnNavigatedToAsync(parameters, ct);
        }

        return new NavigationResult() { IsSuccess = true };
    }

    [RelayCommand]
    async Task Refresh()
    {
        await HandleRefreshReqest();
    }

    #endregion

    #region Back/Forward Navigation



    // デッドロックさせないようにFireAndForgetで実行
    public async Task RestoreNavigationStack()
    {
        var navigationManager = _vm.RestoreNavigationManager;

        PerfomanceStopWatch sw = PerfomanceStopWatch.StartNew("RestoreNavigationStack");
        try
        {
            if (navigationManager.GetViewerNavigationEntry() is { } viewerEntry)
            {
                var viewerNavigationParameters = MakeNavigationParameter(viewerEntry.Parameters);
                if (!viewerNavigationParameters.ContainsKey(PageNavigationConstants.Restored))
                {
                    viewerNavigationParameters.Add(PageNavigationConstants.Restored, string.Empty);
                }

                var viewerResult = await _messenger.NavigateAsync(viewerEntry.PageName, viewerNavigationParameters);
                if (!viewerResult.IsSuccess)
                {
                    await Task.Delay(50);
                    Debug.WriteLine("[NavigationRestore] Failed restore CurrentPage: " + viewerEntry.PageName);
                    await ResetNavigationAsync();
                    return;
                }

                sw.ElapsedWrite("Restore Viewer State");
            }

            //using (await _navigationLock.LockAsync(CancellationToken.None))
            {
                var currentEntry = navigationManager.GetCurrentNavigationEntry();
                if (currentEntry == null)
                {
                    Debug.WriteLine("[NavigationRestore] skip restore page.");
                    await ResetNavigationAsync();
                    return;
                }

                if (_openWithViewerFramePageTypes.Any(x => x.Name == currentEntry.PageName))
                {
                    Debug.WriteLine("[NavigationRestore] skip restore page.");
                    await ResetNavigationAsync();
                    return;
                }

                var backStack = await navigationManager.GetBackNavigationEntriesAsync();
                if (_canGoBackPageTypes.Any(x => x.Name == currentEntry.PageName)
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
                sw.ElapsedWrite("Prev NavigateAsync");
                var result = await _messenger.NavigateAsync(currentEntry.PageName, currentNavParameters);
                if (!result.IsSuccess)
                {
                    await Task.Delay(50);
                    Debug.WriteLine("[NavigationRestore] Failed restore CurrentPage: " + currentEntry.PageName);
                    await ResetNavigationAsync();
                    return;
                }

                sw.ElapsedWrite("After NavigateAsync");

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
                    _backParametersStack.Add(parameters);

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

                sw.ElapsedWrite("Completed");
            }
        }
        catch
        {
            Debug.WriteLine("[NavigationRestore] failed restore current page. ");
            throw;
        }            
    }

    async Task ResetNavigationAsync()
    {
        ViewerFrame.Navigate(typeof(EmptyPage));

        _backParametersStack.Clear();
        _forwardParametersStack.Clear();
        ContentFrame.BackStack.Clear();
        ContentFrame.ForwardStack.Clear();

        await _messenger.NavigateAsync(HomePageName);
        SaveNaviagtionParameters().FireAndForgetSafe();
    }

    // デッドロックを防ぐために常にFireAndForgetで実行させる
    async Task SaveNaviagtionParameters()
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
                PageEntry[] backNavigationPageEntries = new PageEntry[_backParametersStack.Count];
                for (var backStackIndex = 0; backStackIndex < _backParametersStack.Count; backStackIndex++)
                {
                    var parameters = _backParametersStack[backStackIndex];
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
    
    void SetCurrentNavigationParameters(INavigationParameters? parameters)
    {
        if (parameters?.GetNavigationMode() == NavigationMode.Refresh) { return; }

        _prevNavigationParameters = _currentNavigationParameters;
        _currentNavigationParameters = parameters;
    }

    private (INavigationParameters? Current, INavigationParameters? Prev) GetNavigationParametersSet()
    {
        return (_currentNavigationParameters, _prevNavigationParameters);
    }

    INavigationParameters? _viewerNavigationParameters;


    INavigationParameters? _prevNavigationParameters;
    INavigationParameters? _currentNavigationParameters;

    public NavigationParameters GetCurrentNavigationParameter()
    {
        return _currentNavigationParameters?.Clone() ?? new NavigationParameters();
    }


    static PageEntry MakePageEnetry(Type pageType, INavigationParameters? parameters)
    {
        return new PageEntry(pageType.Name, parameters ?? Enumerable.Empty<KeyValuePair<string, object>>());
    }

    bool CanHandleBackRequest()
    {
        if (NowShowingBusyWork)
        {
            CancelBusyWorkCommand.Execute(null);
            return false;
        }

        if (ViewerFrame.Content?.GetType() is { } viewerPageType
            && IsOpenWithViewerPageType(viewerPageType))
        {
            return true;
        }
        else
        {
            var currentPageType = ContentFrame.Content.GetType();
            if (!_canGoBackPageTypes.Contains(currentPageType))
            {
                Debug.WriteLine($"{currentPageType.Name} からの戻る操作をブロック");
                return false;
            }

            var data = new BackNavigationRequestingMessageData();
            _messenger.Send<BackNavigationRequestingMessage>(new(data));
            if (data.IsHandled) { return false; }

            return true;
        }
    }

    async Task HandleBackRequestAsync()
    {
        bool isRequestReset = false;
        {
            var lockReleaser = await _navigationLock.LockAsync(CancellationToken.None);
            try
            {
                if (ViewerFrame.Content?.GetType() is { } viewerPageType
                    && IsOpenWithViewerPageType(viewerPageType))
                {
                    var prevPage = ViewerFrame.Content as Page;
                    try
                    {
                        ViewerFrame.GoBack();
                        var currentPage = ViewerFrame.Content as Page;
                        var np = new NavigationParameters();
                        np.SetNavigationMode(NavigationMode.Back);
                        var ct = RotationNextCancellationTokenSource(viewerPageType);
                        await HandleViewModelNavigation(prevPage?.DataContext as INavigationAware, currentPage?.DataContext as INavigationAware, np, ct);
                    }
                    catch
                    {
                        isRequestReset = true;
                    }
                }
                else if (ContentFrame.CanGoBack)
                {
                    if (_isForgetNavigationRequested)
                    {
                        _isForgetNavigationRequested = false;
                    }
                    var lastNavigationParameters = _backParametersStack.LastOrDefault();

                    if (lastNavigationParameters != null)
                    {
                        var lastNavigationParametersSet = GetNavigationParametersSet();
                        var parameters = GetCurrentNavigationParameter();    // GoBackAsyncを呼ぶとCurrentNavigationParametersが入れ替わる。呼び出し順に注意。

                        _currentNavigationParameters = lastNavigationParameters;
                        _prevNavigationParameters = _backParametersStack.Count >= 2 ? _backParametersStack.TakeLast(2).FirstOrDefault() : null;

                        _backParametersStack.Remove(lastNavigationParameters);
                        _forwardParametersStack.Add(parameters);
                        var prevPage = ContentFrame.Content as Page;
                        try
                        {
                            ContentFrame.GoBack();
                            lastNavigationParameters.SetNavigationMode(NavigationMode.Back);
                            var currentPage = ContentFrame.Content as Page;
                            var ct = RotationNextCancellationTokenSource(null);
                            await HandleViewModelNavigation(prevPage?.DataContext as INavigationAware, currentPage?.DataContext as INavigationAware, lastNavigationParameters, ct);
                        }
                        catch
                        {
                            _currentNavigationParameters = lastNavigationParametersSet.Current;
                            _prevNavigationParameters = lastNavigationParametersSet.Prev;
                            throw;
                        }
                    }
                    else
                    {
                        isRequestReset = true;
                    }
                }
                else
                {
                    isRequestReset = true;
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
            var forwardNavigationParameters = _forwardParametersStack.Last();
            var lastNavigationParametersSet = GetNavigationParametersSet();
            var parameters = GetCurrentNavigationParameter(); // GoForwardAsyncを呼ぶとCurrentNavigationParametersが入れ替わる。呼び出し順に注意。

            {
                _forwardParametersStack.Remove(forwardNavigationParameters);
                _backParametersStack.Add(parameters);
            }

            _currentNavigationParameters = forwardNavigationParameters;
            _prevNavigationParameters = lastNavigationParametersSet.Current;

            var prevPage = ContentFrame.Content as Page;
            try
            {
                ContentFrame.GoForward();
                forwardNavigationParameters.SetNavigationMode(NavigationMode.Forward);
                var currentPage = ContentFrame.Content as Page;
                var ct = RotationNextCancellationTokenSource(null);
                await HandleViewModelNavigation(prevPage?.DataContext as INavigationAware, currentPage?.DataContext as INavigationAware, forwardNavigationParameters, ct);
            }
            catch
            {
                _currentNavigationParameters = lastNavigationParametersSet.Current;
                _prevNavigationParameters = lastNavigationParametersSet.Prev;
                throw;
            }
        }
    }



    async Task HandleRefreshReqest()
    {
        var parameters = _currentNavigationParameters?.Clone() ?? new NavigationParameters();
        parameters.SetNavigationMode(NavigationMode.Refresh);
        var currentPage = ContentFrame.Content as Page;
        var pageViewModel = currentPage?.DataContext as INavigationAware;
        var ct = RotationNextCancellationTokenSource(null);
        await HandleViewModelNavigation(pageViewModel, pageViewModel, parameters, ct);
    }

    void CoreWindow_PointerPressed(CoreWindow sender, PointerEventArgs args)
    {
        if (args.KeyModifiers == Windows.System.VirtualKeyModifiers.None
            && args.CurrentPoint.Properties.IsXButton1Pressed
            )
        {
            if (CanHandleBackRequest())
            {
                args.Handled = true;
                Debug.WriteLine("back navigated with Pointer Back pressed");

                HandleBackRequestAsync().FireAndForgetSafe();
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

                HandleForwardRequest().FireAndForgetSafe();
            }
        }
    }

    void CoreWindow_KeyDown(CoreWindow sender, KeyEventArgs args)
    {
        if (args.VirtualKey == Windows.System.VirtualKey.GoBack)
        {
            if (CanHandleBackRequest())
            {
                args.Handled = true;
                Debug.WriteLine("back navigated with VirtualKey.Back pressed");

                HandleBackRequestAsync().FireAndForgetSafe();
            }
        }
        else if (args.VirtualKey == Windows.System.VirtualKey.GoForward)
        {
            if (CanHandleForwardRequest())
            {
                args.Handled = true;
                Debug.WriteLine("forward navigated with VirtualKey.Back pressed");

                HandleForwardRequest().FireAndForgetSafe();
            }
        }
    }

    void App_BackRequested(object sender, BackRequestedEventArgs e)
    {
        if (CanHandleBackRequest())
        {
            Debug.WriteLine("back navigated with SystemNavigationManager.BackRequested");
            
            HandleBackRequestAsync().FireAndForgetSafe();
        }

        // Note: 強制的にハンドルしないとXboxOneやタブレットでアプリを閉じる動作に繋がってしまう
        e.Handled = true;
    }





    #endregion

    #region Busy Work

    void InitializeBusyWorkUI()
    {
        var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _messenger.Register<BusyWallStartRequestMessage>(this, async (r, m) =>
        {
            _animationCancelTimer.Start();
            if (m.Value.IsCanCancel)
            {
                VisualStateManager.GoToState(this, VS_ShowBusyWall.Name, true);
            }
            else
            {
                VisualStateManager.GoToState(this, VS_ShowBusyWall_WithoutCancel.Name, true);
            }
        });

        _messenger.Register<BusyWallExitRequestMessage>(this, (r, m) =>
        {
            VisualStateManager.GoToState(this, VS_HideBusyWall.Name, true);
        });

        // 
        _animationCancelTimer.IsRepeating = false;
        _animationCancelTimer.Interval = _busyWallDisplayDelayTime;
        _animationCancelTimer.Tick += (_, _) =>
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

    void InitializeThemeChangeRequest()
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

        var titleBar = ApplicationView.GetForCurrentView().TitleBar;
        if ((this.RequestedTheme == ElementTheme.Default && GetWindowsTheme() == Windows.UI.Xaml.ApplicationTheme.Light)
            || this.RequestedTheme is ElementTheme.Light
            )
        {
            titleBar.ButtonBackgroundColor = "#F6F8FB".ToColor();
            titleBar.ButtonForegroundColor = "#000000".ToColor();
            titleBar.ButtonHoverBackgroundColor = "#E9E9E9".ToColor();
            titleBar.ButtonHoverForegroundColor = "#000000".ToColor();
            titleBar.ButtonInactiveBackgroundColor = "#F3F3F3".ToColor();
            titleBar.ButtonInactiveForegroundColor = "#797979".ToColor();
        }
        else
        {
            titleBar.ButtonBackgroundColor = "#1F1F1F".ToColor();
            titleBar.ButtonForegroundColor = "#FFFFFF".ToColor();
            titleBar.ButtonHoverBackgroundColor = "#2d2d2d".ToColor();
            titleBar.ButtonHoverForegroundColor = "#FFFFFF".ToColor();
            titleBar.ButtonInactiveBackgroundColor = "#202020".ToColor();
            titleBar.ButtonInactiveForegroundColor = "#797979".ToColor();
        }

    }

    public static Windows.UI.Xaml.ApplicationTheme GetWindowsTheme()
    {
        var DefaultTheme = new Windows.UI.ViewManagement.UISettings();
        var uiTheme = DefaultTheme.GetColorValue(Windows.UI.ViewManagement.UIColorType.Background).ToString();
        if (uiTheme == "#FF000000")
        {
            return Windows.UI.Xaml.ApplicationTheme.Dark;
        }
        else if (uiTheme == "#FFFFFFFF")
        {
            return Windows.UI.Xaml.ApplicationTheme.Light;
        }
        else
        {
            return Windows.UI.Xaml.ApplicationTheme.Light;
        }
    }

    #endregion


    #region Drop Action

    void Grid_DragEnter(object sender, DragEventArgs e)
    {
        var deferral = Disposable.Create(e.GetDeferral(), deferral => deferral.Complete());
        AsyncTaskErrorHandler.Handle((this, e, deferral), static async (s) =>
        {
            var (_this, e, deferral) = s;
            using (deferral)
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
        });
    }

    void Grid_Drop(object sender, DragEventArgs e)
    {
        var deferral = Disposable.Create(e.GetDeferral(), deferral => deferral.Complete());
        AsyncTaskErrorHandler.Handle((this, e, deferral), static async (s) =>
        {
            var (_this, e, deferral) = s;
            var _messenger = _this._messenger;
            using (deferral)
            {
                string? token = null;
                IStorageItem? openStorageItem = null;
                if (!e.DataView.Contains(StandardDataFormats.StorageItems)) { return; }

                var dropItems = await e.DataView.GetStorageItemsAsync();
                foreach (var storageItem in dropItems)
                {
                    token = await _this._vm.SourceStorageItemsRepository.AddFileTemporaryAsync(storageItem, SourceOriginConstants.DragAndDrop);

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
                            await _messenger.NavigateAsync(nameof(Views.EBookViewerPage), new NavigationParameters((PageNavigationConstants.GeneralPathKey, openStorageItem.Path)));
                        }
                        else if (SupportedFileTypesHelper.IsSupportedMovieFileExtension(fileItem.FileType))
                        {
                            await _messenger.NavigateAsync(nameof(Views.MovieViewerPage), new NavigationParameters((PageNavigationConstants.GeneralPathKey, openStorageItem.Path)));
                        }
                    }
                }
            }
        });
    }

    #endregion


    #region Selection


    void InitializeSelection()
    {
        //_messenger.Register<MenuDisplayMessage>(this, (r, m) => 
        //{
        //    MyNavigationView.IsPaneVisible = m.Value == Visibility.Visible;
        //});
    }


    private void NavigationViewItem_DragEnter(object sender, DragEventArgs e)
    {
        var hostUI = (FrameworkElement)sender;
        if (e.DataView.Properties.TryGetValue("MyCustomDroppedItems", out object itemsRaw) is false) { return; }
        var items = (itemsRaw as List<object>);
        if (items is null or {Count: 0 }) { return; }
        var deferral = R3.Disposable.Create(e.GetDeferral(), deferral => deferral.Complete());
        AsyncTaskErrorHandler.Handle((this, hostUI, e, deferral, items), static async (s) =>
        {
            var (_this, hostUI, e, deferral, items) = s;
            using (deferral)
            {
                foreach (var item in items)
                {
                    if (item is not IStorageItemViewModel myItem)
                    {
                        System.Diagnostics.Debug.WriteLine($"処理できないドラッグされたアイテム: {item?.GetType().Name}");
                        return;
                    }
                }

                if (hostUI.DataContext is MenuItemViewModel menuItemVM)
                {
                    e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
                    e.DragUIOverride.Caption = "MoveToFolder_WithFolderName".Translate(menuItemVM.Title);
                }
            }
        });
    }

    private void NavigationViewItem_Drop(object sender, DragEventArgs e)
    {
        var hostUI = (FrameworkElement)sender;
        if (e.DataView.Properties.TryGetValue("MyCustomDroppedItems", out object itemsRaw) is false) { return; }
        var items = (itemsRaw as List<object>).ToList();
        AsyncTaskErrorHandler.Handle((this, hostUI, e, items), static async (s) =>
        {
            var (_this, hostUI, e, items) = s;
            if (items is null or { Count: 0 }) { return; }

            if (hostUI.DataContext is MenuItemViewModel menuItemVM
                && (menuItemVM.Parameters?.TryGetValue(PageNavigationConstants.GeneralPathKey, out var pathValue) ?? false)
                && pathValue is string escapedPath)                
            {
                var path = Uri.UnescapeDataString(escapedPath);
                // 2. ドラッグ開始時にパッケージされたデータを非同期で取得
                var (token, storageItem) = await _this._vm.SourceStorageItemsRepository.GetSourceStorageItem(path);
                if (storageItem is not StorageFolder hostFolder) { return; }

                var messenger = Ioc.Default.GetRequiredService<IMessenger>();
                await _this.MoveItemsToAsync(hostFolder, items.Cast<IStorageItemViewModel>().Select(x => x.Item.StorageItem), default);
                messenger.SendShowTextNotificationMessage(items.Count == 1 
                    ? "MoveToFolder_Completed_Single".Translate(((IStorageItemViewModel)items[0]).Name, hostFolder.Name)
                    : "MoveToFolder_Completed_Multi".Translate(items.Count, hostFolder.Name));

                foreach (var item in items.Cast<IStorageItemViewModel>())
                {
                    messenger.Send(new StorageItemNotFoundMessage(item.Path));
                }
            }

        });

        // TODO: インスタントな「元に戻す」UIの表示
    }

    private async Task MoveItemsToAsync(Windows.Storage.StorageFolder targetFolder, IEnumerable<Windows.Storage.IStorageItem> items, CancellationToken ct)
    {
        foreach (var item in items)
        {
            Debug.WriteLine($"Move to {targetFolder.Path}: {item.Name}");
            List<Windows.Storage.IStorageItem> failedItems = [];
            if (item is Windows.Storage.StorageFile file)
            {
                try
                {
                    await file.MoveAsync(targetFolder, file.Name, Windows.Storage.NameCollisionOption.FailIfExists).AsTask(ct);
                }
                catch
                {
                    failedItems.Add(item);
                }
            }
            else if (item is Windows.Storage.StorageFolder folder)
            {
                await folder.MoveAsync(targetFolder, Windows.Storage.CreationCollisionOption.OpenIfExists, Windows.Storage.NameCollisionOption.FailIfExists);
            }
        }
    }

    #endregion




    #region Image codec Extension


    readonly IImageCodecService _imageCodecService;


    bool _nowShowingCodecExtentionAnnounce;
    void InitializeImageCodecExtensions()
    {
        _messenger.Register<RequireInstallImageCodecExtensionMessage>(this, async (r, m) =>
        {
            // zip内に対応可能な拡張子のファイルがあった場合に大量にツールチップが表示されないようにする
            if (_nowShowingCodecExtentionAnnounce) { return; }

            _nowShowingCodecExtentionAnnounce = true;

            var codecSupportInfo = await _imageCodecService.GetSupportedCodecsAsync();
            if (!SupportedFileTypesHelper.IsSupportedFileExtension(m.Value) 
                || !codecSupportInfo.Any(x => x.IsContainFileType(m.Value))
            )
            {
                return;
            }

            void TeachTooltip_Closed(Microsoft.UI.Xaml.Controls.TeachingTip sender, Microsoft.UI.Xaml.Controls.TeachingTipClosedEventArgs args)
            {
                sender.Closed -= TeachTooltip_Closed;
                sender.ActionButtonClick -= TeachTooltip_ActionButtonClick;
                RootGrid.Children.Remove(sender);
                _nowShowingCodecExtentionAnnounce = false;
            }

            async void TeachTooltip_ActionButtonClick(Microsoft.UI.Xaml.Controls.TeachingTip sender, object args)
            {
                sender.IsOpen = false;

                if (await _imageCodecService.OpenImageCodecExtensionStorePageAsync(m.Value))
                {
                    _nowShowingCodecExtentionAnnounce = true;
                    var teachTooltip = new Microsoft.UI.Xaml.Controls.TeachingTip()
                    {
                        PreferredPlacement = Microsoft.UI.Xaml.Controls.TeachingTipPlacementMode.Top
                    };
                    teachTooltip.Content = new TextBlock()
                    {
                        Text = "RefreshAfterCodecExtensionInstalled".Translate(m.Value),
                        Margin = new Thickness(16),
                        TextWrapping = TextWrapping.Wrap,
                    };
                    teachTooltip.Closed += (s, e) => RootGrid.Children.Remove(s);
                    teachTooltip.ActionButtonClick += (s, e) => 
                    {
                        s.IsOpen = false;
                        HandleRefreshReqest().FireAndForgetSafe(); 
                    };
                    teachTooltip.ActionButtonContent = "Refresh".Translate();
                    teachTooltip.CloseButtonContent = "Cancel".Translate();
                    RootGrid.Children.Add(teachTooltip);
                    teachTooltip.IsOpen = true;
                }                
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

    void NavigationViewItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.Controls.NavigationViewItemBase menuItem)
        {
            if (menuItem.DataContext is MenuItemViewModel itemVM)
            {
                OpenMenuItemAsync(itemVM).FireAndForgetSafe();
            }
            else if(menuItem.DataContext is MenuItemInvokeActionViewModel invokedItemVM)
            {
                invokedItemVM.Invoked?.Invoke();
            }
        }
    }



    [RelayCommand]
    async Task OpenMenuItemAsync(object item)
    {
        if (item is MenuItemViewModel menuItem)
        {
            // Note: メニューから選択したページはフォルダ管理ページに戻るようにしたい
            // Note: ハック気味で壊れやすそう。
            _isForgetNavigationRequested = true;
            if (await _messenger.NavigateAsync(menuItem.PageType, menuItem.Parameters) is { } result
            && result.IsSuccess)
            {
                
            }
        }
    }

    void RefreshPageKeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (!IsOpenWithViewerPageType(ViewerFrame.Content?.GetType()))
        {
            _vm.RefreshNavigationCommand.Execute(null);
        }
    }

    void ToggleFullScreenKeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        try
        {
            var appView = ApplicationView.GetForCurrentView();
            if (appView.IsFullScreenMode)
            {
                appView.ExitFullScreenMode();
            }
            else
            {
                appView.TryEnterFullScreenMode();
            }
        }
        catch { }
    }

    void ExitViewerKeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        try
        {
            if (IsOpenWithViewerPageType(ViewerFrame.Content?.GetType()))
            {
                _messenger.Send<BackNavigationRequestMessage>();
            }
        }
        catch { }
    }

    void MyNavigationView_ItemInvoked(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            _vm.OpenPageCommand.Execute(nameof(SettingsPage));
        }
    }


    #region App PackageUpdate

    [ObservableProperty]
    bool _nowAvairableUpdate;


    async Task CheckAppPackageUpdateAsync()
    {
        UpdatedNotify();
        Update = await CheckUpdateAsync();
        NowAvairableUpdate = Update.HasAppUpdate;
    }

    [ObservableProperty]
    CheckUpdateResult? _update;

    public async Task<CheckUpdateResult> CheckUpdateAsync(CancellationToken ct = default)
    {
        var storeContext = StoreContext.GetDefault();
        IReadOnlyList<StorePackageUpdate> updates = await storeContext.GetAppAndOptionalStorePackageUpdatesAsync();
        return new CheckUpdateResult(storeContext, updates);
    }

    void UpdatedNotify()
    {        
        var systemInfo = Microsoft.Toolkit.Uwp.Helpers.SystemInformation.Instance;
        if (systemInfo .IsAppUpdated)
        {
            _messenger.SendShowTextNotificationMessage("ApplicationUpdated".Translate(systemInfo.ApplicationVersion.ToFormattedString(3)));
        }
    }

    [RelayCommand]
    async Task ShowPackageUpdateUI()
    {
        try
        {
            PackageUpdateDialogBGWall.Visibility = Visibility.Visible;
            PackageUpdateProgressRing.IsActive = true;
            var result= await PackageUpdateDialog.ShowAsync(ContentDialogPlacement.InPlace);
            if (result == ContentDialogResult.Primary)
            {
                var context = StoreContext.GetDefault();
                IReadOnlyList<StorePackageUpdate> updates =
                    await context.GetAppAndOptionalStorePackageUpdatesAsync();
                var dlResult = await Update!.DownloadAndInstallAllUpdatesAsync();
            }
        }
        finally
        {
            PackageUpdateDialogBGWall.Visibility = Visibility.Collapsed;
            PackageUpdateProgressRing.IsActive = false;
        }
    }

    void PackageUpdateDialogBGWall_Tapped(object sender, TappedRoutedEventArgs e)
    {
        PackageUpdateDialog.Hide();
    }

    #endregion
}



public class CheckUpdateResult
{
    readonly StoreContext _storeContext;
    readonly IReadOnlyList<StorePackageUpdate> _updates;

    public CheckUpdateResult(StoreContext storeContext, IReadOnlyList<StorePackageUpdate> updates)
    {
        _storeContext = storeContext;
        _updates = updates;
    }

    public bool HasAppUpdate
    {
        get
        {
            if (AppUpdate is { } appUpdate)
            {
                var currentAppVersion = Windows.ApplicationModel.AppInfo.Current.Package.Id.Version;
                var updateVer = appUpdate.Package.Id.Version;
                return currentAppVersion.Major < updateVer.Major
                || currentAppVersion.Minor < updateVer.Minor
                || currentAppVersion.Build < updateVer.Build
                || currentAppVersion.Revision < updateVer.Revision
                ;
            }
            else
            {
                return false;
            }
        }
    }

    public bool CanDownloadSilently => _storeContext.CanSilentlyDownloadStorePackageUpdates;

    public StorePackageUpdate? AppUpdate => _updates.FirstOrDefault(x => x.Package.DisplayName == nameof(TsubameViewer));

    public IAsyncOperationWithProgress<StorePackageUpdateResult, StorePackageUpdateStatus> DownloadAndInstallAllUpdatesAsync()
    {
        if (CanDownloadSilently)
        {
            return _storeContext.TrySilentDownloadAndInstallStorePackageUpdatesAsync(_updates);
        }
        else
        {
            return _storeContext.RequestDownloadAndInstallStorePackageUpdatesAsync(_updates);
        }
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

public sealed class MenuItemDateTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Item { get; set; }
    public DataTemplate? ItemInvoke { get; set; }
    public DataTemplate? SubItem { get; set; }
    public DataTemplate? Separator { get; set; }

    protected override DataTemplate SelectTemplateCore(object item)
    {
        return this.SelectTemplateCore(item, null!);
    }
    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        if (item is MenuSubItemViewModel)
        {
            return SubItem ?? base.SelectTemplateCore(item, container);
        }        
        else if (item is MenuItemViewModel)
        {
            return Item ?? base.SelectTemplateCore(item, container);
        }
        else if (item is MenuItemInvokeActionViewModel)
        {
            return ItemInvoke ?? base.SelectTemplateCore(item, container);
        }
        else if (item is MenuSeparatorViewModel)
        {
            return Separator ?? base.SelectTemplateCore(item, container);
        }
        else
        {
            return base.SelectTemplateCore(item, container);
        }
    }
}

public sealed class MenuItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate? MenuSeparator { get; set; }
    public DataTemplate? MenuItem { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        return SelectTemplateCore(item, null!);
    }
    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
    {
        return item switch
        {
            MenuItemViewModel _ => MenuItem,
            MenuSeparatorViewModel _ => MenuSeparator,
            _ => throw new NotSupportedException(),
        };
    }
}

public sealed class InPageSearchRequestMessage : ValueChangedMessage<string>
{
    public InPageSearchRequestMessage(string value) : base(value)
    {

    }
}

public sealed class SearchQuerySubmitedRequestMessage : ValueChangedMessage<string>
{
    public SearchQuerySubmitedRequestMessage(string value) : base(value)
    {

    }
}

public sealed class CurrentInPageSearchTextRequestMessage : RequestMessage<string>
{

}