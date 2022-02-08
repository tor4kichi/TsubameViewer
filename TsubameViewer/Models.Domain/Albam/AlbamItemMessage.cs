using Microsoft.Toolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace TsubameViewer.Models.Domain.Albam
{
    public sealed class AlbamItemRemovedMessage : ValueChangedMessage<(Guid AlbamId, string Path)>
    {
        public AlbamItemRemovedMessage(Guid albamId, string path) : base((albamId, path))
        {
        }
    }

    public sealed class AlbamItemAddedMessage : ValueChangedMessage<(Guid AlbamId, string Path)>
    {
        public AlbamItemAddedMessage(Guid albamId, string path) : base((albamId, path))
        {
        }
    }
}
