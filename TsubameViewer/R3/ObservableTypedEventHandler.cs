using R3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace R3.Extensions;

public static class ObservableEventExtensions
{
    /// <summary>
    /// TypedEventHandler を R3 の Observable<EventPattern> に変換します。
    /// </summary>
    public static Observable<EventPattern<TSender, TEventArgs>> FromTypedEvent<TSender, TEventArgs>(
        Action<TypedEventHandler<TSender, TEventArgs>> addHandler,
        Action<TypedEventHandler<TSender, TEventArgs>> removeHandler)
    {
        return Observable.FromEvent<TypedEventHandler<TSender, TEventArgs>, EventPattern<TSender, TEventArgs>>(
            conversion => (sender, args) => conversion(new EventPattern<TSender, TEventArgs>(sender, args)),
            addHandler,
            removeHandler
        );
    }

    public static Observable<EventPattern<MouseDevice, MouseEventArgs>> ObserveMouseMoved(this MouseDevice mouseDevice)
    {
        return ObservableEventExtensions.FromTypedEvent<MouseDevice, MouseEventArgs>(
            h => mouseDevice.MouseMoved += h,
            h => mouseDevice.MouseMoved -= h
            );
    }

    public static Observable<WindowActivatedEventArgs> ObserveActivated(this Window window)
    {
        return Observable.FromEvent<WindowActivatedEventHandler, WindowActivatedEventArgs>(
            conversion => (sender, args) => conversion(args),
            h => window.Activated += h,
            h => window.Activated -= h);
    }

    public static Observable<PointerRoutedEventArgs> ObservePointerMoved(this FrameworkElement control)
    {
        return Observable.FromEvent<PointerEventHandler, PointerRoutedEventArgs>(
            conversion => (sender, args) => conversion(args),
            h => control.PointerMoved += h,
            h => control.PointerMoved -= h);
    }

    public static Observable<PointerRoutedEventArgs> ObservePointerEntered(this FrameworkElement control)
    {
        return Observable.FromEvent<PointerEventHandler, PointerRoutedEventArgs>(
            conversion => (sender, args) => conversion(args),
            h => control.PointerEntered += h,
            h => control.PointerEntered -= h);
    }

    public static Observable<PointerRoutedEventArgs> ObservePointerExited(this FrameworkElement control)
    {
        return Observable.FromEvent<PointerEventHandler, PointerRoutedEventArgs>(
            conversion => (sender, args) => conversion(args),
            h => control.PointerExited += h,
            h => control.PointerExited -= h);
    }



}
