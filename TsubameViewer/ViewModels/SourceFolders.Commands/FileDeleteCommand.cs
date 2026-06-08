using CommunityToolkit.Mvvm.Messaging;
using I18NPortable;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Contracts.Notification;
using TsubameViewer.Contracts.Services;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using TsubameViewer.Core.Models.SourceFolders;
using Windows.Storage;
using Windows.UI.Xaml.Media;
#nullable enable
namespace TsubameViewer.ViewModels.SourceFolders.Commands;

public sealed class FileDeleteCommand : ImageSourceCommandBase
{
    readonly IMessenger _messenger;
    readonly IFileControlDialogService _fileControlDialogService;
    readonly FileControlSettings _fileControlSettings;

    public FileDeleteCommand(
        IMessenger messenger,
        IFileControlDialogService fileControlDialogService,
        FileControlSettings fileControlSettings
        )
    {
        _messenger = messenger;
        _fileControlDialogService = fileControlDialogService;
        _fileControlSettings = fileControlSettings;
    }

    protected override bool CanExecute(IImageSource imageSource)
    {
        return FlattenAlbamItemInnerImageSource(imageSource) is StorageItemImageSource;
    }

    protected override async void Execute(IImageSource imageSource)
    {
        if (imageSource.StorageItem is IStorageItem item)
        {
            bool isDelete;
            if (_fileControlSettings.StorageItemDeleteDoNotDisplayNextTime)
            {
                isDelete = true;
            }
            else
            {
                (isDelete, var doNotAskTwice) = await _fileControlDialogService.ConfirmFileDeletionAsync(item);
                if (doNotAskTwice)
                {
                    _fileControlSettings.StorageItemDeleteDoNotDisplayNextTime = true;
                }
            }

            if (isDelete)
            {
                try
                {
                    await item.DeleteAsync(StorageDeleteOption.Default);
                    _messenger.Send(new StorageItemNotFoundMessage(item.Path));
                }
                catch (FileNotFoundException)
                {
                    
                }
                catch (Exception)
                {
                    if (item is StorageFile)
                    {
                        _messenger.SendShowTextNotificationMessage("FileDeleteFailed".Translate(item.Name));
                    }
                    else if (item is StorageFolder)
                    {
                        _messenger.SendShowTextNotificationMessage("FolderDeleteFailed".Translate(item.Name));
                    }
                }
            }
        }
    }

    protected override async void Execute(IEnumerable<IImageSource> imageSources)
    {
        if (imageSources.Any(x => x.StorageItem != null))
        {
            var item = imageSources.First(x => x.StorageItem != null).StorageItem;
            bool isDelete;
            if (_fileControlSettings.StorageItemDeleteDoNotDisplayNextTime)
            {
                isDelete = true;
            }
            else
            {
                (isDelete, var doNotAskTwice) = await _fileControlDialogService.ConfirmFileDeletionAsync(item);
                if (doNotAskTwice)
                {
                    _fileControlSettings.StorageItemDeleteDoNotDisplayNextTime = true;
                }
            }

            if (isDelete)
            {
                try
                {
                    await Task.WhenAll(imageSources.Select(x => x.StorageItem.DeleteAsync(StorageDeleteOption.Default).AsTask()));
                    foreach (var deleted in imageSources)
                    {
                        _messenger.Send(new StorageItemNotFoundMessage(deleted.Path));
                    }
                }
                catch (FileNotFoundException)
                {

                }
                catch (Exception)
                {
                    if (item is StorageFile)
                    {
                        _messenger.SendShowTextNotificationMessage("FileDeleteFailed".Translate(item.Name));
                    }
                    else if (item is StorageFolder)
                    {
                        _messenger.SendShowTextNotificationMessage("FolderDeleteFailed".Translate(item.Name));
                    }
                }
            }
        }
    }
}
