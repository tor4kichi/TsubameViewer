using I18NPortable;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.Contracts.Notification;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.Core.Contracts.Services;

namespace TsubameViewer.ViewModels.SourceFolders.Commands
{
    public sealed class ChangeStorageItemThumbnailImageCommand : CommandBase
    {
        private readonly IMessenger _messenger;
        private readonly IThumbnailImageService _thumbnailManager;


        public bool IsArchiveThumbnailSetToFile { get; set; }

        public ChangeStorageItemThumbnailImageCommand(
            IMessenger messenger,
            IThumbnailImageService thumbnailManager
            ) 
        {
            _messenger = messenger;
            _thumbnailManager = thumbnailManager;
        }

        protected override bool CanExecute(object parameter)
        {
            if (parameter is StorageItemViewModel itemVM)
            {
                parameter = itemVM.Item;
            }

            return parameter is IImageSource;
        }

        protected override async void Execute(object parameter)
        {
            if (parameter is StorageItemViewModel itemVM)
            {
                parameter = itemVM.Item;
            }

            if (parameter is IImageSource imageSource)
            {
                try
                {
                    await _thumbnailManager.SetParentThumbnailImageAsync(imageSource, IsArchiveThumbnailSetToFile);
                    _messenger.SendShowTextNotificationMessage("ThumbnailImageChanged".Translate());
                }
                catch
                {
                    //_messenger.SendShowTextNotificationMessage("ThumbnailImageChanged".Translate());
                    throw;
                }
            }
        }
    }
}
