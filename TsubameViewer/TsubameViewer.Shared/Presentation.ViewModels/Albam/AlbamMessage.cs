using Microsoft.Toolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain.Albam;

namespace TsubameViewer.Presentation.ViewModels.Albam
{
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
}
