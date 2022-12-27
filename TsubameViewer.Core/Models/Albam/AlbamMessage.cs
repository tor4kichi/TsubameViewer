using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Core.Models.Albam;

namespace TsubameViewer.Core.Models.Albam;

public sealed class AlbamCreatedMessage : ValueChangedMessage<AlbamEntry>
{
    public AlbamCreatedMessage(AlbamEntry value) : base(value)
    {
    }
}

public sealed class AlbamDeletedMessage : ValueChangedMessage<Guid>
{
    public AlbamDeletedMessage(Guid value) : base(value)
    {
    }
}

public sealed class AlbamEditedMessage : ValueChangedMessage<AlbamEntry>
{
    public AlbamEditedMessage(AlbamEntry value) : base(value)
    {
    }
}
