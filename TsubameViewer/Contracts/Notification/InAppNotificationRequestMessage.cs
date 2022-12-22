using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TsubameViewer.Contracts.Notification
{
    public sealed class InAppNotificationRequestMessage : ValueChangedMessage<object>
    {
        public InAppNotificationRequestMessage(object value) : base(value)
        {
        }
    }

    public static class InAppNotificationRequestMessageExtensions
    {
        public static void SendShowTextNotificationMessage(this IMessenger messenger, string content)
        {
            messenger.Send(new InAppNotificationRequestMessage(content));
        }
    }

}
