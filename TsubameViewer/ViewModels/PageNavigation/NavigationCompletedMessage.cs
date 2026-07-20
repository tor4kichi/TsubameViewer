using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Navigation;

namespace TsubameViewer.ViewModels.PageNavigation;

public sealed class NavigationCompletedMessage : ValueChangedMessage<NavigationEventArgs>
{
    public NavigationCompletedMessage(NavigationEventArgs value) : base(value)
    {
    }
}
