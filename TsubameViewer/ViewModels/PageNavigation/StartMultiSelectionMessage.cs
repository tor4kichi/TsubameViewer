using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace TsubameViewer.ViewModels.PageNavigation
{
    public sealed class StartMultiSelectionMessage : ValueChangedMessage<int>
    {
        public StartMultiSelectionMessage() : base(0)
        {
        }
    }
}
