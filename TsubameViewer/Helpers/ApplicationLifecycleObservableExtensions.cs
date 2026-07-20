using R3;
using System;
using System.Collections.Generic;
using System.Text;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace TsubameViewer.Helpers;

public static class ApplicationLifecycleObservableExtensions 
{        
    public static R3.Observable<bool> VisibilityChanged(this Window window)
    {            
        return Observable.FromEvent<WindowVisibilityChangedEventHandler, VisibilityChangedEventArgs>(
            conversion => (sender, args) => conversion(args),
            h => window.VisibilityChanged += h, 
            h => window.VisibilityChanged -= h)
            .Select(args => args.Visible);
    }

    public static R3.Observable<bool> WindowActivationStateChanged(this Window window)
    {
        return R3.Observable.FromEvent<WindowActivatedEventHandler, WindowActivatedEventArgs>(
            conversion => (sender, args) => conversion(args),
            h => window.Activated += h,
            h => window.Activated -= h)
            .Select(args => args.WindowActivationState != CoreWindowActivationState.Deactivated)
            .DistinctUntilChanged();
    }
}
