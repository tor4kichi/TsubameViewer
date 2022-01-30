using Microsoft.Toolkit.Mvvm.Messaging;
using Prism.Commands;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Presentation.Views;
using Windows.UI.Xaml.Media.Animation;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation.Commands
{
    public sealed class OpenFolderListupCommand : DelegateCommandBase
    {
        private readonly IMessenger _messenger;

        public OpenFolderListupCommand(
            IMessenger messenger
            )
        {
            _messenger = messenger;
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
                var type = SupportedFileTypesHelper.StorageItemToStorageItemTypes(imageSource);
                if (type is StorageItemTypes.Archive or StorageItemTypes.Folder or StorageItemTypes.ArchiveFolder)
                {
                    var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
                    var result = await _messenger.NavigateAsync(nameof(FolderListupPage), parameters);
                }
            }
        }
    }
}
