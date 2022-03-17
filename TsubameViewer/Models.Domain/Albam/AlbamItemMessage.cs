using Microsoft.Toolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace TsubameViewer.Models.Domain.Albam
{
    public record AlbamItemChangedMessageValue(Guid AlbamId, string Path, AlbamItemType ItemType);

    public sealed class AlbamItemRemovedMessage : ValueChangedMessage<AlbamItemChangedMessageValue>
    {
        public AlbamItemRemovedMessage(Guid albamId, string path, AlbamItemType itemType) : base(new (albamId, path, itemType))
        {
        }
    }

    public sealed class AlbamItemAddedMessage : ValueChangedMessage<AlbamItemChangedMessageValue>
    {
        public AlbamItemAddedMessage(Guid albamId, string path, AlbamItemType itemType) : base(new (albamId, path, itemType))
        {
        }
    }
}
