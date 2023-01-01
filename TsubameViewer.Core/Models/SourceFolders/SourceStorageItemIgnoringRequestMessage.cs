using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace TsubameViewer.Core.Models.SourceFolders;

public class SourceStorageItemIgnoringRequestMessage : ValueChangedMessage<string>
{
    public SourceStorageItemIgnoringRequestMessage(string value) : base(value)
    {
    }
}


