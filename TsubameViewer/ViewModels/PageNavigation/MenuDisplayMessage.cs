using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Text;
using Windows.UI.Xaml;

namespace TsubameViewer.ViewModels.PageNavigation
{
    public sealed class MenuDisplayMessage : ValueChangedMessage<Visibility>
    {
        public MenuDisplayMessage(Visibility value) : base(value)
        {
        }
    }
}
