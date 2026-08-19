using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.WinUI.Helpers;
using R3;
using R3.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.Navigation;
using TsubameViewer.Services.Navigation;
using TsubameViewer.ViewModels;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.Views;
using TsubameViewer.Views.Helpers;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.WindowManagement;
using Windows.UI.Xaml;
using ApplicationTheme = TsubameViewer.ViewModels.ApplicationTheme;
#nullable enable
namespace TsubameViewer.Services;

public sealed class SecondaryWindowService
{
    public SecondaryWindowService(
        NavigationStackRepository navigationStackRepository,
        ApplicationSettings applicationSettings)
    {
        var appView = ApplicationView.GetForCurrentView();
        _primaryWindow = new PrimaryWindowFacade(appView, CoreApplication.GetCurrentView().TitleBar);
        appView.Consolidated += AppView_Consolidated;
        _navigationStackRepository = navigationStackRepository;
        _applicationSettings = applicationSettings;
        
        _dispoable = _applicationSettings.ObservePropertyChanged(x => x.Theme)
            .Subscribe(this, (x, s) => 
            {
                var _this = s;
                var elementTheme = x switch
                {
                    ApplicationTheme.Light => ElementTheme.Light,
                    ApplicationTheme.Dark => ElementTheme.Dark,
                    ApplicationTheme.Default => ElementTheme.Default,
                    _ => throw new InvalidOperationException()
                };
                
                foreach (var context in _this._appWindows)
                {
                    RefreshTitleBarButtonColors(context.AppWindow, x);
                    context.AppShell.RequestedTheme = elementTheme;
                }
            });
            
    }

    private void AppView_Consolidated(ApplicationView sender, ApplicationViewConsolidatedEventArgs args)
    {        
        foreach (var context in _appWindows)
        {
            _ = context.AppWindow.CloseAsync();
        }
    }

    readonly PrimaryWindowFacade _primaryWindow;
    readonly NavigationStackRepository _navigationStackRepository;
    private readonly ApplicationSettings _applicationSettings;
    private readonly IDisposable _dispoable;
    SecondaryWindowItem? _defaultWindowItem;
    public IWindowManagementAware GetCurentFocusWindow()
    {
        if (_nowCreatingAppWindow != null) { return _nowCreatingAppWindow; }
        else if (_appWindows.FirstOrDefault(x => x.IsFocused) is { } focusdWindow) { return focusdWindow; }
        else { return _primaryWindow; }
    }

    private List<SecondaryWindowItem> _appWindows = new();

    SecondaryWindowItem? _nowCreatingAppWindow;
    
    void RefreshTitleBarButtonColors(AppWindow appWindow, ApplicationTheme theme)
    {
        var actualTheme = theme switch
        {
            ApplicationTheme.Default => SystemThemeHelper.GetSystemTheme(),
            _ => theme,
        };

        var titleBar = appWindow.TitleBar;
        if (actualTheme == ApplicationTheme.Light)
        {
            titleBar.ButtonBackgroundColor = Color.FromArgb(0x55, 0xF6, 0xF8, 0xFB);
            titleBar.ButtonForegroundColor = Color.FromArgb(0xFF, 0x00, 0x00, 0x00);
            titleBar.ButtonHoverBackgroundColor = Color.FromArgb(0xFF, 0xF6, 0xF8, 0xFB);
            titleBar.ButtonHoverForegroundColor = Color.FromArgb(0xFF, 0x00, 0x00, 0x00);
            titleBar.ButtonInactiveBackgroundColor = Color.FromArgb(0x33, 0xF6, 0xF8, 0xFB);
            titleBar.ButtonInactiveForegroundColor = Color.FromArgb(0xFF, 0x79, 0x79, 0x79);
        }
        else
        {
            titleBar.ButtonBackgroundColor = Color.FromArgb(0x55, 0x1F, 0x1F, 0x1F);
            titleBar.ButtonForegroundColor = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
            titleBar.ButtonHoverBackgroundColor = Color.FromArgb(0xFF, 0x2D, 0x2D, 0x2D);
            titleBar.ButtonHoverForegroundColor = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
            titleBar.ButtonInactiveBackgroundColor = Color.FromArgb(0x33, 0x20, 0x20, 0x20);
            titleBar.ButtonInactiveForegroundColor = Color.FromArgb(0xFF, 0x79, 0x79, 0x79);
        }
    }

    async Task<SecondaryWindowItem> CreateNewWindowAsync(string pageName, INavigationParameters navigationParameters)
    {
        var appWindow = await AppWindow.TryCreateAsync();

        const int defaultWidth = 1280;
        const int defaultHeight = 720;
        appWindow.RequestSize(new Windows.Foundation.Size(defaultWidth, defaultHeight));
        appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        RefreshTitleBarButtonColors(appWindow, _applicationSettings.Theme);        
        SecondaryWindowItem context = (App.Current as App).InitializeAppWindow(appWindow);
        context.AppShell._secondaryWindowService = this;
        context.AppShell._windowContext = context;
        context.AppShell.RequestedTheme = _applicationSettings.Theme switch
        {
            ApplicationTheme.Light => ElementTheme.Light,
            ApplicationTheme.Dark => ElementTheme.Dark,
            ApplicationTheme.Default => ElementTheme.Default,
            _ => throw new InvalidOperationException()
        }; ;
        _nowCreatingAppWindow = context;
        try
        {
            context.IsFullScreenMode = _applicationSettings.IsFullScreenOnAppLaunch;
            navigationParameters.SetNavigationMode(Windows.UI.Xaml.Navigation.NavigationMode.New);
            context.IsDisplay = true;
            appWindow.Closed += (s, e) =>
            {                
                _ = context.ClearNavigationAsync();
                context.IsDisplay = false;
                _appWindows.Remove(context);
                if (_appWindows.Count == 0
                    && _defaultWindowItem != null
                    && e.Reason == AppWindowClosedReason.UserInitiated)
                {
                    _defaultWindowItem = null;
                    _navigationStackRepository.ClearViewerNavigationEntry();
                    Debug.WriteLine("_navigationStackRepository.ClearViewerNavigationEntry");
                    _ = _primaryWindow.ShowAsync();
                }
            };

            _appWindows.Add(context);

            await appWindow.TryShowAsync();
            await Observable.NextFrame().WaitAsync(); // ウィンドウのレイアウト完了まで待機したい
            await context.NavigateAsync(pageName, navigationParameters);

            return context;
        }
        finally
        {
            _nowCreatingAppWindow = null;
        }
    }

    async Task<bool> TryNavigatingToWithExistWindowAsync(string pageName, INavigationParameters parameters)
    {
        if (_appWindows.FirstOrDefault() is not { } context) { return false; }
        await context.ClearNavigationAsync();
        parameters.SetNavigationMode(Windows.UI.Xaml.Navigation.NavigationMode.New);
        _nowCreatingAppWindow = context;
        try
        {
            await context.NavigateAsync(pageName, parameters);
        }
        finally
        {
            _nowCreatingAppWindow = null;
        }
        return true;
    }
    
    public async Task OpenViewerAsync(string pageName, INavigationParameters parameters, bool alwaysOpenNewWindow = true)
    {
        if (string.IsNullOrEmpty(pageName)) { throw new InvalidOperationException(); }
        if (!alwaysOpenNewWindow)
        {
            _navigationStackRepository.SetViewerNavigationEntry(new PageEntry(pageName, parameters));
            if (await TryNavigatingToWithExistWindowAsync(pageName, parameters))
            {
                _defaultWindowItem = _appWindows.First();
                await _defaultWindowItem.ShowAsync();
                return;
            }
        }

        var context = await CreateNewWindowAsync(pageName, parameters);
        if (!alwaysOpenNewWindow)
        {
            _defaultWindowItem = context;
        }
    }

    public async Task OpenViewerAsync(IImageSource imageSource, bool alwaysOpenNewWindow = true)
    {
        var itemType = SupportedFileTypesHelper.FileExtensionToStorageItemType(imageSource.Path);
        string pageName = "";
        if ((itemType is StorageItemTypes.Image
            or StorageItemTypes.Archive
            or StorageItemTypes.ArchiveFolder
            or StorageItemTypes.Folder)
            || imageSource.StorageItem is Windows.Storage.StorageFolder)
        {
            pageName = nameof(ImageViewerPage);
        }
        else if (itemType is StorageItemTypes.EBook)
        {
            pageName = nameof(EBookViewerPage);
        }
        else if (itemType is StorageItemTypes.Movie)
        {
            pageName = nameof(MovieViewerPage);
        }

        var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
        await OpenViewerAsync(pageName, parameters, alwaysOpenNewWindow);
    }

    public async Task CloseAsync(IWindowManagementAware context)
    {
        if (context is SecondaryWindowItem secondaryWindow)
        {
            await secondaryWindow.AppWindow.CloseAsync();
            await _primaryWindow.ShowAsync();
        }
    }
}

public interface IWindowManagementAware : INotifyPropertyChanged
{
    bool IsPrimary { get; }
    bool IsSecondary { get; }
    bool TryEnterFullScreenMode();
    void ExitFullScreenMode();
    bool IsFullScreenMode { get; }
    bool NowDisplayTitleBar { get; }

    Task ShowAsync();
}


public sealed partial class PrimaryWindowFacade : ObservableObject, IWindowManagementAware
{
    readonly ApplicationView _appView;
    readonly CoreApplicationViewTitleBar _titleBar;

    public PrimaryWindowFacade(ApplicationView appView, CoreApplicationViewTitleBar titleBar)
    {
        _appView = appView;
        _titleBar = titleBar;
        _titleBar.IsVisibleChanged += _titleBar_IsVisibleChanged;        
    }

    private void _titleBar_IsVisibleChanged(CoreApplicationViewTitleBar sender, object args)
    {
        NowDisplayTitleBar = sender.IsVisible;
        OnPropertyChanged(nameof(NowDisplayTitleBar));
    }

    public bool NowDisplayTitleBar { get; private set; }

    public bool IsPrimary => true;
    public bool IsSecondary => false;

    public bool IsFullScreenMode => _appView.IsFullScreenMode;
    
    public bool TryEnterFullScreenMode()
    {
        var result = _appView.TryEnterFullScreenMode();
        OnPropertyChanged(nameof(IsFullScreenMode));
        return result;
    }

    public void ExitFullScreenMode()
    {
        _appView.ExitFullScreenMode();
        OnPropertyChanged(nameof(IsFullScreenMode));
    }

    public async Task ShowAsync()
    {
        await ApplicationViewSwitcher.TryShowAsStandaloneAsync(_appView.Id);
    }
}

public sealed partial class SecondaryWindowItem : ObservableObject, IWindowManagementAware
{
    #region Impl IWindowManagementAware

    public bool IsPrimary => false;
    public bool IsSecondary => true;
    
    [ObservableProperty]
    bool _isFullScreenMode = false;
    public bool TryEnterFullScreenMode()
    {
        var result = AppWindow.Presenter.RequestPresentation(AppWindowPresentationKind.FullScreen);
        IsFullScreenMode = true;
        return result;
    }

    public void ExitFullScreenMode()
    {
        AppWindow.Presenter.RequestPresentation(AppWindowPresentationKind.Default);
        IsFullScreenMode = false;
    }

    #endregion
    private bool _isFocused;
    public bool IsFocused => _isFocused && IsDisplay;

    public SecondaryWindowItem(AppWindow appWindow, SecondaryAppShell appShell)
    {
        AppWindow = appWindow;
        AppShell = appShell;
        AppWindow.Changed += AppWindow_Changed;
        var config = AppWindow.Presenter.GetConfiguration();
        _isFullScreenMode = config.Kind == AppWindowPresentationKind.FullScreen;
        NowDisplayTitleBar = AppWindow.TitleBar.IsVisible;
        if (appShell != null)
        {
            appShell.GotFocus += (s, e) =>
            {
                _isFocused = true;                
            };

            appShell.LostFocus += (s, e) =>
            {
                _isFocused = false;
            };
        }
    }

    public bool NowDisplayTitleBar { get; private set; }
    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidTitleBarChange)
        {
            NowDisplayTitleBar = sender.TitleBar.IsVisible;
            OnPropertyChanged(nameof(NowDisplayTitleBar));
        }
        else if (args.DidSizeChange)
        {
            IsFullScreenMode = sender.Presenter.GetConfiguration().Kind == AppWindowPresentationKind.FullScreen;
        }
    }

    public void ForceUpdateDisplayStatus()
    {
        NowDisplayTitleBar = AppWindow.TitleBar.IsVisible;
        OnPropertyChanged(nameof(NowDisplayTitleBar));
        IsFullScreenMode = AppWindow.Presenter.GetConfiguration().Kind == AppWindowPresentationKind.FullScreen;
    }

    public bool IsDisplay { get; set; }
    public AppWindow AppWindow { get; }
    public SecondaryAppShell AppShell { get; }

    [ObservableProperty]
    string _title = "";

    partial void OnTitleChanged(string value)
    {
        AppWindow.Title = value;
    }

    internal async Task NavigateAsync(string pageName, INavigationParameters navigationParameters)
    {
        await AppShell.NavigateAsync(pageName, navigationParameters);
    }

    internal async Task ClearNavigationAsync()
    {
        await AppShell.ClearNavigationAsync();
    }

    public async Task ShowAsync()
    {
        await AppWindow.TryShowAsync();
    }
}