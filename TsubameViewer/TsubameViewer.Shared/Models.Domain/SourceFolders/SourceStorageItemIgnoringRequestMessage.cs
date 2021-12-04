using Microsoft.Toolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace TsubameViewer.Models.Domain.SourceFolders
{
    public class SourceStorageItemIgnoringRequestMessage : ValueChangedMessage<string>
    {
        public SourceStorageItemIgnoringRequestMessage(string value) : base(value)
        {
        }
    }

    
}
