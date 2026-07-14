using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using R3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TsubameViewer.Core;

public static class MessengerObservableExtensions
{
    public static Observable<TMessageValue> CreateObservable<TMessage, TMessageValue>(this IMessenger messenger) where TMessage : ValueChangedMessage<TMessageValue>
    {
        return new ValueChangedMessageObservable<TMessage, TMessageValue>(messenger);
    }

    sealed class ValueChangedMessageObservable<TMessage, TMessageValue> : Observable<TMessageValue> where TMessage : ValueChangedMessage<TMessageValue>
    {
        private readonly IMessenger _messenger;

        public ValueChangedMessageObservable(IMessenger messenger)
        {
            _messenger = messenger;
        }

        protected override IDisposable SubscribeCore(Observer<TMessageValue> observer)
        {
            return new ValueChangedMessageObserver(_messenger, observer);
        }


        public sealed class ValueChangedMessageObserver : IDisposable                
        {
            private readonly IMessenger _messenger;
            private readonly Observer<TMessageValue> _observer;

            public ValueChangedMessageObserver(IMessenger messenger, Observer<TMessageValue> observer)
            {
                _messenger = messenger;
                _observer = observer;
                _messenger.Register<TMessage>(this, (r, m) => _observer.OnNext(m.Value));
            }

            public void Dispose()
            {
                _messenger.Unregister<TMessage>(this);
                _observer.OnCompleted();
                (_observer as IDisposable)?.Dispose();
            }
        }
    }
}
