using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace TsubameViewer.Presentation.Services.UWP
{
    public static class ApplicationLifecycleObservable 
    {        
        public static IObservable<bool> VisibilityChanged()
        {
            return Observable.FromEventPattern<WindowVisibilityChangedEventHandler, VisibilityChangedEventArgs>(h => Window.Current.VisibilityChanged += h, h => Window.Current.VisibilityChanged -= h)
                .Select(args => args.EventArgs.Visible);
        }

        public static IObservable<bool> WindowActivationStateChanged()
        {
            return Observable.FromEventPattern<WindowActivatedEventHandler, WindowActivatedEventArgs>(h => Window.Current.Activated += h, h => Window.Current.Activated -= h)
                .Select(args => args.EventArgs.WindowActivationState != CoreWindowActivationState.Deactivated)
                .DistinctUntilChanged();
        }
    }
}
