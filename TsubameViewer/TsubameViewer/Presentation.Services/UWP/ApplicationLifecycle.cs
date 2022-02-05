using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using Windows.UI.Core;

namespace TsubameViewer.Presentation.Services.UWP
{
    public static class ApplicationLifecycleObservable 
    {        
        public static IObservable<bool> VisibilityChanged()
        {
            return WindowsObservable.FromEventPattern<object, WindowVisibilityChangedEventArgs>(h => App.Current.Window.VisibilityChanged += h, h => App.Current.Window.VisibilityChanged -= h)
                .Select(args => args.EventArgs.Visible);
        }

        public static IObservable<bool> WindowActivationStateChanged()
        {
            return WindowsObservable.FromEventPattern<object, Microsoft.UI.Xaml.WindowActivatedEventArgs>(h => App.Current.Window.Activated += h, h => App.Current.Window.Activated -= h)
                .Select(args => args.EventArgs.WindowActivationState != WindowActivationState.Deactivated)
                .DistinctUntilChanged();
        }
    }
}
