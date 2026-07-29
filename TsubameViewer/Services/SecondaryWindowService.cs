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
        else if (_primaryWindowActivated) { return _primaryWindow; }
        else { throw new InvalidOperationException(); }
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

public interface IWindowManagementAware
{
    bool IsPrimary { get; }
    bool IsSecondary { get; }
    bool TryEnterFullScreenMode();
    void ExitFullScreenMode();
    bool IsFullScreenMode { get; }

    Task ShowAsync();
}

public sealed class PrimaryWindowFacade : IWindowManagementAware
{
    readonly ApplicationView _appView;

    public PrimaryWindowFacade()
    {
        _appView = ApplicationView.GetForCurrentView();
    }

    public bool IsPrimary => true;
    public bool IsSecondary => false;

    public bool IsFullScreenMode => _appView.IsFullScreenMode;

    public bool TryEnterFullScreenMode()
    {
        return _appView.TryEnterFullScreenMode();
    }

    public void ExitFullScreenMode()
    {
        _appView.ExitFullScreenMode();
    }

    public async Task ShowAsync()
    {
        await ApplicationViewSwitcher.SwitchAsync(_appView.Id);
    }
}

public sealed class SecondaryWindowItem : IWindowManagementAware
{
    #region Impl IWindowManagementAware

    public bool IsPrimary => false;
    public bool IsSecondary => true;

    public bool IsFullScreenMode => AppWindow.Presenter.GetConfiguration().Kind == AppWindowPresentationKind.FullScreen;

    public bool TryEnterFullScreenMode()
    {
        return AppWindow.Presenter.RequestPresentation(AppWindowPresentationKind.FullScreen);        
    }

    public void ExitFullScreenMode()
    {
        AppWindow.Presenter.RequestPresentation(AppWindowPresentationKind.Default);
    }

    #endregion
    private bool _isFocused;
    public bool IsFocused => _isFocused && IsDisplay;

    public SecondaryWindowItem(AppWindow appWindow, SecondaryAppShell appShell)
    {
        AppWindow = appWindow;
        AppShell = appShell;

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