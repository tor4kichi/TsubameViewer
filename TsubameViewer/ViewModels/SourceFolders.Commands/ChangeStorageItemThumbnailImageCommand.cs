using CommunityToolkit.Mvvm.Messaging;
using I18NPortable;
using TsubameViewer.Contracts.Notification;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.ViewModels.PageNavigation;

namespace TsubameViewer.ViewModels.SourceFolders.Commands
{
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

        protected override bool CanExecute(object parameter)
        {
            if (parameter is IStorageItemViewModel itemVM)
            {
                parameter = itemVM.Item;
            }

            return parameter is IImageSource;
        }

        protected override async void Execute(object parameter)
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
