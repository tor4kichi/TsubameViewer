using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using TsubameViewer.Models.Domain.SourceFolders;
using TsubameViewer.Presentation.Services;
using Windows.Storage;

namespace TsubameViewer.Presentation.ViewModels.SourceFolders.Commands
{
    public sealed class FileDeleteCommand : ImageSourceCommandBase
    {
        private readonly FileControlDialogService _fileControlDialogService;
        private readonly FileControlSettings _fileControlSettings;

        public FileDeleteCommand(
            FileControlDialogService fileControlDialogService,
            FileControlSettings fileControlSettings
            )
        {
            _fileControlDialogService = fileControlDialogService;
            _fileControlSettings = fileControlSettings;
        }

        protected override bool CanExecute(IImageSource imageSource)
        {
            return imageSource is StorageItemImageSource;
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

                        //item.
                    }
                    catch (FileNotFoundException)
                    {

                    }
                }
            }
        }
    }
}
