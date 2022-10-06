using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation
{
    public sealed class BackNavigationRequestingMessageData
    {
        public bool IsHandled { get; set; }
    }

    public sealed class BackNavigationRequestingMessage : ValueChangedMessage<BackNavigationRequestingMessageData>
    {
        public BackNavigationRequestingMessage(BackNavigationRequestingMessageData value) : base(value)
        {
        }
    }
}
