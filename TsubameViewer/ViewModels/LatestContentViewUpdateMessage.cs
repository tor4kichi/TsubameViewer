using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TsubameViewer.ViewModels;

public sealed class LatestContentViewUpdateMessage : ValueChangedMessage<string>
{
    public LatestContentViewUpdateMessage(string value) : base(value)
    {
    }
}
