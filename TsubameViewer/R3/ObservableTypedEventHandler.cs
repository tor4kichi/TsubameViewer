using R3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;

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
}
