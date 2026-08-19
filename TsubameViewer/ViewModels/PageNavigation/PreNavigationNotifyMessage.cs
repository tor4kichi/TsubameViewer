using CommunityToolkit.Mvvm.Messaging.Messages;
using R3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TsubameViewer.ViewModels.PageNavigation;

public sealed class PreNavigationNotifyMessage : ValueChangedMessage<Unit>
{
    public PreNavigationNotifyMessage() : base(Unit.Default)
    {
    }
}
