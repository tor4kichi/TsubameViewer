using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using CommunityToolkit.WinUI.Helpers;
using DryIoc;
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
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.Navigation;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.Services;
using TsubameViewer.Services.Navigation;
using TsubameViewer.ViewModels;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.ViewModels.SourceFolders;
using TsubameViewer.Views.Helpers;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
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

    async void InitialziePurchase()
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

    private void ShowPurchaseConfirmFlyoutMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
    {
        PurchaseConfirmFlyout.ShowAt(FeedbackButton);
    }

    #endregion

    private readonly AppShellViewModel _vm;
    private readonly IMessenger _messenger;

    private readonly DispatcherQueue _dispatcherQueue;
    private readonly IViewLocator _viewLocator;
    private readonly DispatcherQueueTimer _AnimationCancelTimer;
    private readonly TimeSpan _BusyWallDisplayDelayTime = PageNavigationConstants.BusyWallDisplayDelayTime;
    private readonly List<object> _footerItemsForTop;
    private readonly List<object> _footerItemsForLeft;

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

        _AnimationCancelTimer = _dispatcherQueue.CreateTimer();
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

        InitialziePurchase();

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

    private void AppShell_Loaded(object sender, RoutedEventArgs e)
    {
        CoreApplicationViewTitleBar coreTitleBar =
            CoreApplication.GetCurrentView().TitleBar;
        var appView = ApplicationView.GetForCurrentView();
        UpdateTitleBarDisplay(coreTitleBar.IsVisible, appView.IsFullScreenMode);
    }

    public bool NowShowTitlebarInFullScreen
    {
        get { return (bool)GetValue(NowShowTitlebarInFullScreenProperty); }
        set { SetValue(NowShowTitlebarInFullScreenProperty, value); }
    }

    public static readonly DependencyProperty NowShowTitlebarInFullScreenProperty =
        DependencyProperty.Register(nameof(NowShowTitlebarInFullScreen), typeof(bool), typeof(AppShell), new PropertyMetadata(false));


    private void AppView_VisibleBoundsChanged(ApplicationView sender, object args)
    {
        CoreApplicationViewTitleBar coreTitleBar =
                    CoreApplication.GetCurrentView().TitleBar;
        UpdateTitleBarDisplay(coreTitleBar.IsVisible, sender.IsFullScreenMode);
    }

    private void CoreTitleBar_IsVisibleChanged(CoreApplicationViewTitleBar sender, object args)
    {
        var appView = ApplicationView.GetForCurrentView();
        UpdateTitleBarDisplay(sender.IsVisible, appView.IsFullScreenMode);
    }



    void UpdateTitleBarDisplay(bool isDisplay, bool isFullScreen)
    {
        NowShowTitlebarInFullScreen = isDisplay && isFullScreen;
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
        typeof(EBookViewerPage),
        typeof(MovieViewerPage),
        typeof(SettingsPage),
        typeof(FolderOrArchiveRestructurePage),
    }.ToImmutableHashSet();

    private readonly static ImmutableHashSet<Type> CanGoBackPageTypes = new Type[]
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

    private readonly static ImmutableHashSet<Type> UniqueOnNavigtionStackPageTypes = new Type[]
    {
        typeof(ImageViewerPage),
        typeof(EBookViewerPage),
        typeof(MovieViewerPage),
        typeof(SearchResultPage),
        typeof(FolderOrArchiveRestructurePage),
    }.ToImmutableHashSet();



    private readonly static ImmutableHashSet<Type> SelectionAvairablePageTypes = new Type[]
    {
        typeof(ImageListupPage),
        typeof(FolderListupPage),
    }.ToImmutableHashSet();


    private readonly static ImmutableHashSet<Type> OpenWithViewerFramePageTypes = new Type[]
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
        return OpenWithViewerFramePageTypes.Contains(pageType);
    }

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
            try
            {
                using var lockReleaser = await _navigationLock.LockAsync(CancellationToken.None);

                var (currentNavParam, prevNavParam) = GetNavigationParametersSet();

                if (m.IsForgetNavigaiton)
                {
                    _isForgetNavigationRequested = true;
                }

                if (IsOpenWithViewerPageType(ViewerFrame.Content.GetType())
                    && OpenWithViewerFramePageTypes.Any(x => x.Name.Equals(m.PageName, StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException("ViewerPage can only one exist on app navigation stack.");
                }

                var parameters = m.Parameters ?? new NavigationParameters();
                parameters.SetNavigationMode(NavigationMode.New);
                return await NavigateAsync(m.PageName, parameters, m.IsForgetNavigaiton is false);
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
                await NavigateAsync(HomePageName, navigationParameters, false);
                _messenger.SendShowTextNotificationMessage("Notification_SourceStorageItemNotFound".Translate());
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
    }

    void RefreshBackButton()
    {

    }

    private void InitializeViewerFrameNavigation()
    {
        ViewerFrame.Navigate(typeof(EmptyPage));
        ViewerFrame.Navigated += (s, e) =>
        {
            Debug.WriteLine($"ViewerFrame Navigate to : {e.SourcePageType.Name}");
            var frame = (Frame)s;
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
                SetTitleContentForPrimary(ContentFrame);
                MyNavigationView.Visibility = Visibility.Visible;
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
        }
        Window.Current.SetTitleBar(TitlebarBG);
    }

    private void Frame_Navigated(object sender, NavigationEventArgs e)
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

            static bool IsCurrentPageMatchMenuItem(MenuItemViewModel menuItemVM, string parametersPath)
            {
                if (menuItemVM.Parameters == null && parametersPath == null)
                {
                    return true;
                }

                if ((menuItemVM.Parameters?.TryGetValue(PageNavigationConstants.GeneralPathKey, out string menuItemEscapedPath) ?? false))
                {
                    var (menuItemPath, _) = PageNavigationConstants.ParseStorageItemId(Uri.UnescapeDataString(menuItemEscapedPath));

                    return parametersPath.StartsWith(menuItemPath);
                }           
                else
                {
                    return false;
                }
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
        else if (!GoBackButton.IsEnabled)
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
                        BackParametersStack.RemoveAt(BackParametersStack.Count - 1);
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
                    && (e.SourcePageType == typeof(FolderListupPage) || e.SourcePageType == typeof(ImageListupPage))
                    && frame.BackStack.TakeLast(3).All(x => x.SourcePageType == typeof(FolderListupPage) || x.SourcePageType == typeof(ImageListupPage))
                    && currentNavParam != null && currentNavParam.TryGetValue(PageNavigationConstants.GeneralPathKey, out string currentNavigationPathParameter)
                    && BackParametersStack.TakeLast(3).All(x => x.TryGetValue(PageNavigationConstants.GeneralPathKey, out string backStackEntryPathparameter) && backStackEntryPathparameter == currentNavigationPathParameter)
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

        var ct = RotationNextCancellationTokenSource(viewType);
        var prevPage = frame.Content as Page;
        var options = new FrameNavigationOptions() 
        {
            IsNavigationStackEnabled = isNavigationStackEnabled, 
            TransitionInfoOverride = isNavigationStackEnabled ? PageTransitionHelper.MakeNavigationTransitionInfoFromPageName(pageName) : new SuppressNavigationTransitionInfo() 
        };
        var result = frame.Navigate(viewType, parameters, options.TransitionInfoOverride);

        if (result is false)
        {
            throw new InvalidOperationException($"Failed ContentFrame navigate to {pageName}.");
        }

        var page = frame.Content;
        var currentPage = page as Page;        
        var handleResult = await HandleViewModelNavigation(prevPage?.DataContext as INavigationAware, currentPage?.DataContext as INavigationAware, parameters, ct);
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
    private async Task<NavigationResult> HandleViewModelNavigation(INavigationAware fromPageVM, INavigationAware toPageVM, INavigationParameters parameters, CancellationToken ct)
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

                if (OpenWithViewerFramePageTypes.Any(x => x.Name == currentEntry.PageName))
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
            throw;
        }            
    }

    private async Task ResetNavigationAsync()
    {
        ViewerFrame.Navigate(typeof(EmptyPage));

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

    INavigationParameters? _viewerNavigationParameters;


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

        if (ContentFrame.Content == null)
        {
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
            if (!CanGoBackPageTypes.Contains(currentPageType))
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
                    ViewerFrame.Visibility = Visibility.Collapsed;
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



    private async Task HandleRefreshReqest()
    {
        var parameters = _currentNavigationParameters.Clone();
        parameters.SetNavigationMode(NavigationMode.Refresh);
        var currentPage = ContentFrame.Content as Page;
        var pageViewModel = currentPage?.DataContext as INavigationAware;
        var ct = RotationNextCancellationTokenSource(null);
        await HandleViewModelNavigation(pageViewModel, pageViewModel, parameters, ct);
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

    #region Busy Work

    private void InitializeBusyWorkUI()
    {
        var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _messenger.Register<BusyWallStartRequestMessage>(this, async (r, m) =>
        {
            _AnimationCancelTimer.Start();
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
                        await _messenger.NavigateAsync(nameof(Views.EBookViewerPage), new NavigationParameters((PageNavigationConstants.GeneralPathKey, openStorageItem.Path)));
                    }
                    else if (SupportedFileTypesHelper.IsSupportedMovieFileExtension(fileItem.FileType))
                    {
                        await _messenger.NavigateAsync(nameof(Views.MovieViewerPage), new NavigationParameters((PageNavigationConstants.GeneralPathKey, openStorageItem.Path)));
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
            MyNavigationView.IsPaneVisible = m.Value == Visibility.Visible;
        });
    }


    #endregion




    #region Image codec Extension


    private readonly IImageCodecService _imageCodecService;


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
                        _ = HandleRefreshReqest(); 
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

    private void NavigationViewItem_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.Controls.NavigationViewItemBase menuItem)
        {
            if (menuItem.DataContext is MenuItemViewModel itemVM)
            {
                _ = OpenMenuItemAsync(itemVM);
            }
            else if(menuItem.DataContext is MenuItemInvokeActionViewModel invokedItemVM)
            {
                invokedItemVM.Invoked();
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

    private void ToggleFullScreenKeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
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

    private void ExitViewerKeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
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

    private void MyNavigationView_ItemInvoked(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            _vm.OpenPageCommand.Execute(nameof(SettingsPage));
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
    public DataTemplate Item { get; set; }
    public DataTemplate ItemInvoke { get; set; }
    public DataTemplate SubItem { get; set; }
    public DataTemplate Separator { get; set; }

    protected override DataTemplate SelectTemplateCore(object item)
    {
        return this.SelectTemplateCore(item, null);
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
    public DataTemplate MenuSeparator { get; set; }
    public DataTemplate MenuItem { get; set; }

    protected override DataTemplate SelectTemplateCore(object item)
    {
        return SelectTemplateCore(item, null);
    }
    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
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