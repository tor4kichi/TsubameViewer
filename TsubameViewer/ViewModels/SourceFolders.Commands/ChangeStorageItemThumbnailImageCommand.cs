using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using I18NPortable;
using System.IO;
using TsubameViewer.Contracts.Notification;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.ViewModels.PageNavigation;


#nullable enable
namespace TsubameViewer.ViewModels.SourceFolders.Commands;

public sealed class ThumbnailImageUpdateRequestMessage : ValueChangedMessage<string>
{
    public ThumbnailImageUpdateRequestMessage(string value) : base(value)
    {
    }
}

public sealed class ChangeStorageItemThumbnailImageCommand : CommandBase
{
    private readonly IMessenger _messenger;
    private readonly ThumbnailImageManager _thumbnailManager;


    public bool IsArchiveThumbnailSetToFile { get; set; }

    public ChangeStorageItemThumbnailImageCommand(
        IMessenger messenger,
        ThumbnailImageManager thumbnailManager
        ) 
    {
        _messenger = messenger;
        _thumbnailManager = thumbnailManager;
    }

    public override bool CanExecute(object parameter)
    {
        if (parameter is IStorageItemViewModel itemVM)
        {
            parameter = itemVM.Item;
        }

        return parameter is IImageSource;
    }

    public override async void Execute(object parameter)
    {
        if (parameter is IStorageItemViewModel itemVM)
        {
            parameter = itemVM.Item;
        }

        if (parameter is IImageSource imageSource)
        {
            try
            {
                await _thumbnailManager.SetParentThumbnailImageAsync(imageSource, IsArchiveThumbnailSetToFile);
                _messenger.SendShowTextNotificationMessage("ThumbnailImageChanged".Translate());
                _messenger.Send(new ThumbnailImageUpdateRequestMessage(Path.GetDirectoryName(imageSource.Path)));
            }
            catch
            {
                //_messenger.SendShowTextNotificationMessage("ThumbnailImageChanged".Translate());
                throw;
            }
        }
    }
}
