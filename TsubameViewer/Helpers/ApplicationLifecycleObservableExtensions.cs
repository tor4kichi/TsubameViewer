using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace TsubameViewer.Helpers;

public static class ApplicationLifecycleObservableExtensions 
{        
    public static IObservable<bool> VisibilityChanged(this Window window)
    {
        return Observable.FromEventPattern<WindowVisibilityChangedEventHandler, VisibilityChangedEventArgs>(h => window.VisibilityChanged += h, h => window.VisibilityChanged -= h)
            .Select(args => args.EventArgs.Visible);
    }

    public static IObservable<bool> WindowActivationStateChanged(this Window window)
    {
        return Observable.FromEventPattern<WindowActivatedEventHandler, WindowActivatedEventArgs>(h => window.Activated += h, h => window.Activated -= h)
            .Select(args => args.EventArgs.WindowActivationState != CoreWindowActivationState.Deactivated)
            .DistinctUntilChanged();
    }
}
