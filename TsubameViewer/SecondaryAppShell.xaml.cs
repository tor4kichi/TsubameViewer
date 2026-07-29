using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI.Animations;
using DryIoc;
using R3;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Contracts.Navigation;
using TsubameViewer.Core.Helpers;
using TsubameViewer.Services;
using TsubameViewer.Services.Navigation;
using TsubameViewer.ViewModels.PageNavigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

#nullable enable
namespace TsubameViewer.Views;

public sealed partial class SecondaryAppShell : UserControl
{
    public SecondaryAppShell(IViewLocator _viewLocator)
    {
        this.InitializeComponent();
        this._viewLocator = _viewLocator;
        MyFrame.Navigate(typeof(EmptyPage));        
    }


    public async Task<INavigationResult> NavigateAsync(string pageName, INavigationParameters parameters, NavigationTransitionInfo? transitionInfo = null, bool isNavigationStackEnabled = true)
    {
        PerfomanceStopWatch sw = PerfomanceStopWatch.StartNew("SecondaryWindow NavigateAsync");
        var viewType = _viewLocator.ResolveView(pageName);
        Frame frame = MyFrame;
        SetCurrentNavigationParameters(parameters);

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

        if (currentPage is ITitlebarContentAware titleBar)
        {
            TitleBarContent.ContentTemplate = titleBar.GetContent();
            TitleBarContent.Content = currentPage?.DataContext;
            titleBar.ObserveTitleChanged()
                .Subscribe(x => TitleText.Text = !string.IsNullOrEmpty(x) ? x : "TsubameViewer")
                .RegisterTo(ct);
        }
        else
        {
            TitleBarContent.ContentTemplate = null;
            TitleBarContent.Content = null;
            TitleText.Text = "TsubameViewer";
        }
        return handleResult;
    }


    CancellationToken RotationNextCancellationTokenSource(Type? pageType)
    {
        _navigateCts?.Cancel();
        _navigateCts?.Dispose();
        _navigateCts = new CancellationTokenSource();
        return _navigateCts.Token;
    }

    INavigationParameters? _prevNavigationParameters;
    INavigationParameters? _currentNavigationParameters;

    void SetCurrentNavigationParameters(INavigationParameters? parameters)
    {
        if (parameters?.GetNavigationMode() == NavigationMode.Refresh) { return; }

        _prevNavigationParameters = _currentNavigationParameters;
        _currentNavigationParameters = parameters;
    }


    CancellationTokenSource? _navigateCts;
    private readonly IViewLocator _viewLocator;
    internal SecondaryWindowService _secondaryWindowService;
    internal IWindowManagementAware _windowContext;

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

    public async Task ClearNavigationAsync()
    {        
        var parameters = new NavigationParameters();
        parameters.SetNavigationMode(NavigationMode.Back);
        await NavigateAsync(nameof(EmptyPage), parameters);
        MyFrame.BackStack.Clear();
    }



    void ToggleFullScreenKeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        try
        {
            if (_windowContext.IsFullScreenMode)
            {
                _windowContext.ExitFullScreenMode();
            }
            else
            {
                _windowContext.TryEnterFullScreenMode();
            }
        }
        catch { }
    }

    void ExitViewerKeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        _ = _secondaryWindowService.CloseAsync(_windowContext);
    }
}
