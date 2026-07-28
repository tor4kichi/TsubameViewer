using CommunityToolkit.Mvvm.Messaging;
using I18NPortable;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Contracts.Notification;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using Windows.Storage;

namespace TsubameViewer.ViewModels.SourceFolders.Commands;

internal class ResetThumbnailImageCommand : ImageSourceCommandBase
{
    private readonly IMessenger _messenger;
    private readonly ThumbnailImageManager _thumbnailManager;

    public ResetThumbnailImageCommand(
        IMessenger messenger, 
        ThumbnailImageManager thumbnailManager)
    {
        _messenger = messenger;
        _thumbnailManager = thumbnailManager;
    }

    protected override bool CanExecute(IImageSource imageSource)
    {
        return imageSource is StorageItemImageSource;
    }
    protected override async void Execute(IImageSource imageSource)
    {
        try
        {
            await _thumbnailManager.ResetFolderThumbnailImageAsync(imageSource);
            //_messenger.SendShowTextNotificationMessage("ThumbnailImageChanged".Translate());
            _messenger.Send(new ThumbnailImageUpdateRequestMessage(imageSource.Path));
        }
        catch
        {
            //_messenger.SendShowTextNotificationMessage("ThumbnailImageChanged".Translate());
            throw;
        }
    }
}
