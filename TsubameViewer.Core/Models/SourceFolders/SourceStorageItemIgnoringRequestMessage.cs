using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace TsubameViewer.Core.Models.SourceFolders;

public class SourceStorageItemIgnoringRequestMessage : AsyncRequestMessage<StorageItemDeletionResult>
{
    public SourceStorageItemIgnoringRequestMessage(string path)
    {
        Path = path;
    }

    public string Path { get; }
}

public class StorageItemDeletionResult
{
    
}
