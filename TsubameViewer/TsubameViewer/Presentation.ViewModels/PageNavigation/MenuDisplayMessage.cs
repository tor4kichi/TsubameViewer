using Microsoft.Toolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Text;
using Microsoft.UI.Xaml;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation
{
    public sealed class MenuDisplayMessage : ValueChangedMessage<Visibility>
    {
        public MenuDisplayMessage(Visibility value) : base(value)
        {
        }
    }
}
