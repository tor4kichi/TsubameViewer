using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Views;
using Windows.UI.Xaml.Media.Animation;

namespace TsubameViewer.ViewModels.PageNavigation.Commands
{
    public sealed class OpenImageListupCommand : CommandBase
    {
        private readonly IMessenger _messenger;

        public OpenImageListupCommand(
            IMessenger messenger
            )
        {
            _messenger = messenger;
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
                var type = SupportedFileTypesHelper.StorageItemToStorageItemTypes(imageSource);
                if (type is StorageItemTypes.Archive or StorageItemTypes.Folder or StorageItemTypes.Albam)
                {
                    var result = await _messenger.NavigateAsync(nameof(ImageListupPage), PageTransitionHelper.CreatePageParameter(imageSource));
                }
            }
        }
    }
}
