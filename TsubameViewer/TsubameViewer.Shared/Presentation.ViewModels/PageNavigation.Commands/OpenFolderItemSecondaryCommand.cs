using Microsoft.Toolkit.Mvvm.Messaging;
using Microsoft.Xaml.Interactivity;
using Prism.Commands;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Windows.Input;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using TsubameViewer.Presentation.Views;
using TsubameViewer.Presentation.Views.SourceFolders.Commands;
using Windows.Storage;
using Windows.UI.Xaml.Media.Animation;
using StorageItemTypes = TsubameViewer.Models.Domain.StorageItemTypes;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation.Commands
{
    public sealed class OpenFolderItemSecondaryCommand : DelegateCommandBase
    {
        private readonly IMessenger _messenger;
        private readonly FolderContainerTypeManager _folderContainerTypeManager;
        private readonly SourceChoiceCommand _sourceChoiceCommand;

        public OpenFolderItemSecondaryCommand(
            IMessenger messenger,
            FolderContainerTypeManager folderContainerTypeManager,
            SourceChoiceCommand sourceChoiceCommand
            )
        {
            _messenger = messenger;
            _folderContainerTypeManager = folderContainerTypeManager;
            _sourceChoiceCommand = sourceChoiceCommand;
        }

        protected override bool CanExecute(object parameter)
        {
            return parameter is StorageItemViewModel;
        }

        protected override async void Execute(object parameter)
        {
            if (parameter is StorageItemViewModel item)
            {
                if (item.Type == StorageItemTypes.Image || item.Type == StorageItemTypes.Archive)
                {
                    var parameters = StorageItemViewModel.CreatePageParameter(item);
                    var result = await _messenger.NavigateAsync(nameof(ImageListupPage), parameters);
                }
                else if (item.Type == StorageItemTypes.Folder)
                {
                    var containerType = await _messenger.WorkWithBusyWallAsync(async ct => await _folderContainerTypeManager.GetLatestFolderContainerTypeAndUpdateCacheAsync((item.Item as StorageItemImageSource).StorageItem as StorageFolder, ct), CancellationToken.None);
                    if (containerType == FolderContainerType.Other)
                    {
                        var parameters = StorageItemViewModel.CreatePageParameter(item);
                        var result = await _messenger.NavigateAsync(nameof(FolderListupPage), parameters);
                    }
                    else
                    {
                        var parameters = StorageItemViewModel.CreatePageParameter(item);
                        var result = await _messenger.NavigateAsync(nameof(ImageListupPage), parameters);
                    }
                }
                else if (item.Type == StorageItemTypes.EBook)
                {
                    var parameters = StorageItemViewModel.CreatePageParameter(item);
                    var result = await _messenger.NavigateAsync(nameof(EBookReaderPage), parameters);
                }
                else if (item.Type == StorageItemTypes.None)
                {
                    ((ICommand)_sourceChoiceCommand).Execute(null);
                }
            }
        }
    }
}
