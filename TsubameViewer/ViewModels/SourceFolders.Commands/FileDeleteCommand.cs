using I18NPortable;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.Services;
using TsubameViewer.ViewModels.Notification;
using Windows.Storage;

namespace TsubameViewer.ViewModels.SourceFolders.Commands
{
    public sealed class FileDeleteCommand : ImageSourceCommandBase
    {
        private readonly IMessenger _messenger;
        private readonly FileControlDialogService _fileControlDialogService;
        private readonly FileControlSettings _fileControlSettings;

        public FileDeleteCommand(
            IMessenger messenger,
            FileControlDialogService fileControlDialogService,
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
                    }
                    catch (FileNotFoundException)
                    {
                        
                    }
                    catch (Exception ex)
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
}
