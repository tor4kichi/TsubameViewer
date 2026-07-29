using R3.Extensions;
using R3;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Services.Navigation;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.Views;
using Windows.ApplicationModel.Activation;
using Windows.UI.ViewManagement;
using Windows.UI.WindowManagement;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Windows.ApplicationModel.Core;

namespace TsubameViewer.Services;

public sealed class SecondaryWindowService
{
    public SecondaryWindowService()
    {
        _disposable = Windows.UI.Xaml.Window.Current.ObserveActivated()
            .Subscribe(x => _primaryWindowActivated = x.WindowActivationState != Windows.UI.Core.CoreWindowActivationState.Deactivated);
        _primaryWindow = new PrimaryWindowFacade();
    }

    IDisposable _disposable;
    private readonly PrimaryWindowFacade _primaryWindow;
    bool _primaryWindowActivated;
    public IWindowManagementAware GetCurentFocusWindow()
    {
        if (_nowCreatingAppWindow != null) { return _nowCreatingAppWindow; }
        else if (_appWindows.FirstOrDefault(x => x.IsFocused) is { } focusdWindow) { return focusdWindow; }
        else { return _primaryWindow; }
    }

    private List<SecondaryWindowItem> _appWindows = new();

    SecondaryWindowItem? _nowCreatingAppWindow;

    async Task CreateNewWindowAsync(string pageName, INavigationParameters navigationParameters)
    {
        var appWindow = await AppWindow.TryCreateAsync();
        appWindow.Title = "TsubameViewer";

        const int defaultWidth = 1280;
        const int defaultHeight = 720;
        appWindow.RequestSize(new Windows.Foundation.Size(defaultWidth, defaultHeight));

        var context = (App.Current as App).InitializeAppWindow(appWindow);
        context.AppShell._secondaryWindowService = this;
        context.AppShell._windowContext = context;
        _nowCreatingAppWindow = context;
        try
        {
            navigationParameters.SetNavigationMode(Windows.UI.Xaml.Navigation.NavigationMode.New);
            appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            context.IsDisplay = true;
            appWindow.Closed += (s, e) =>
            {
                _ = context.ClearNavigationAsync();
                context.IsDisplay = false;
                _appWindows.Remove(context);
            };

            _appWindows.Add(context);

            await appWindow.TryShowAsync();
            await context.NavigateAsync(pageName, navigationParameters);

        }
        finally
        {
            _nowCreatingAppWindow = null;
        }
    }

    public async Task OpenViewerAsync(IImageSource imageSource)
    {
        var itemType = SupportedFileTypesHelper.FileExtensionToStorageItemType(imageSource.Path);
        if ((itemType is StorageItemTypes.Image
            or StorageItemTypes.Archive
            or StorageItemTypes.ArchiveFolder
            or StorageItemTypes.Folder)
            || imageSource.StorageItem is Windows.Storage.StorageFolder)
        {
            var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
            await CreateNewWindowAsync(nameof(ImageViewerPage), parameters);
        }
        else if (itemType is StorageItemTypes.EBook)
        {
            var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
            await CreateNewWindowAsync(nameof(EBookViewerPage), parameters);
        }
        else if (itemType is StorageItemTypes.Movie)
        {
            var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
            await CreateNewWindowAsync(nameof(MovieViewerPage), parameters);
        }
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

    public PrimaryWindowFacade()
    {
        _appView = ApplicationView.GetForCurrentView();
        _titleBar = CoreApplication.GetCurrentView().TitleBar;
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
        await ApplicationViewSwitcher.SwitchAsync(_appView.Id);
    }
}

public sealed partial class SecondaryWindowItem : ObservableObject, IWindowManagementAware
{
    #region Impl IWindowManagementAware

    public bool IsPrimary => false;
    public bool IsSecondary => true;

    public bool IsFullScreenMode => _nowFullScreen;

    bool _nowFullScreen = false;
    public bool TryEnterFullScreenMode()
    {
        var result = AppWindow.Presenter.RequestPresentation(AppWindowPresentationKind.FullScreen);
        _nowFullScreen = true;
        OnPropertyChanged(nameof(IsFullScreenMode));
        return result;
    }

    public void ExitFullScreenMode()
    {
        AppWindow.Presenter.RequestPresentation(AppWindowPresentationKind.Default);
        _nowFullScreen = false;
        OnPropertyChanged(nameof(IsFullScreenMode));
    }

    #endregion
    private bool _isFocused;
    public bool IsFocused => _isFocused && IsDisplay;

    public SecondaryWindowItem(AppWindow appWindow, SecondaryAppShell appShell)
    {
        AppWindow = appWindow;
        AppShell = appShell;
        AppWindow.Changed += AppWindow_Changed;
        _nowFullScreen = AppWindow.Presenter.GetConfiguration().Kind == AppWindowPresentationKind.FullScreen;
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
    }

    public bool IsDisplay { get; set; }
    public AppWindow AppWindow { get; }
    public SecondaryAppShell AppShell { get; }

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