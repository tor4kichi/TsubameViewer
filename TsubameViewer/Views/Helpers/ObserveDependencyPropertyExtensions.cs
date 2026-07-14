
﻿using System;
#if WINDOWS_UWP
using Windows.UI.Core;
using Windows.UI.Xaml;
#else
using System.ComponentModel;
using System.Linq;
using System.Windows;
#endif

namespace R3.Extensions;

/// <summary>
/// DependencyObject extension methods.
/// </summary>
public static class DependencyObjectExtensions
{
    /// <summary>
    /// Observe DependencyProperty
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="self"></param>
    /// <param name="dp"></param>
    /// <returns></returns>
    public static Observable<Unit> ObserveDependencyProperty<T>(this T self, DependencyProperty dp)
        where T : DependencyObject
    {
        return Observable.Create<Unit>(ox =>
        {
#if WINDOWS_UWP
            void h(DependencyObject _, DependencyProperty __) => ox.OnNext(Unit.Default);
            var token = self.RegisterPropertyChangedCallback(dp, h);
            return Disposable.Create(() => self.UnregisterPropertyChangedCallback(dp, token));
#else
            void h(object _, EventArgs __) => ox.OnNext(Unit.Default);
            var descriptor = DependencyPropertyDescriptor.FromProperty(dp, typeof(T));
            descriptor.AddValueChanged(self, h);
            return Disposable.Create(() => descriptor.RemoveValueChanged(self, h));
#endif
        });
    }

    public static R3.Observable<SizeChangedEventArgs> ObserveSizeChanged(this FrameworkElement element)
    {
        return R3.Observable.FromEvent<SizeChangedEventHandler, SizeChangedEventArgs>(
            h => (s, e) => h(e),
            h => element.SizeChanged += h,
            h => element.SizeChanged -= h);
    }
}
