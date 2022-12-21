using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Windows.Input;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using TsubameViewer.ViewModels.Albam.Commands;
using TsubameViewer.ViewModels.SourceFolders.Commands;
using TsubameViewer.Views;
using Windows.Storage;
using Windows.UI.Xaml.Media.Animation;
using StorageItemTypes = TsubameViewer.Core.Models.StorageItemTypes;

namespace TsubameViewer.ViewModels.PageNavigation.Commands
{
    public sealed class OpenFolderItemCommand : CommandBase
    {
        private readonly IMessenger _messenger;
        private readonly FolderContainerTypeManager _folderContainerTypeManager;
        private readonly SourceChoiceCommand _sourceChoiceCommand;
        private readonly AlbamCreateCommand _albamCreateCommand;

        public OpenFolderItemCommand(
            IMessenger messenger,
            FolderContainerTypeManager folderContainerTypeManager,
            SourceChoiceCommand sourceChoiceCommand,
            AlbamCreateCommand albamCreateCommand
            )
        {
            _messenger = messenger;
            _folderContainerTypeManager = folderContainerTypeManager;
            _sourceChoiceCommand = sourceChoiceCommand;
            _albamCreateCommand = albamCreateCommand;
        }

        protected override bool CanExecute(object parameter)
        {
            if (parameter is StorageItemViewModel itemVM)
            {
                parameter = itemVM.Item;

                if (itemVM.Type == StorageItemTypes.AddFolder)
                {
                    return true;
                }
                else if (itemVM.Type == StorageItemTypes.AddAlbam)
                {
                    return true;
                }
            }

            return parameter is IImageSource;
        }

        protected override async void Execute(object parameter)
        {
            if (parameter is StorageItemViewModel itemVM)
            {
                parameter = itemVM.Item;

                if (itemVM.Type == StorageItemTypes.AddFolder)
                {
                    ((ICommand)_sourceChoiceCommand).Execute(null);
                    return;
                }
                else if (itemVM.Type == StorageItemTypes.AddAlbam)
                {
                    ((ICommand)_albamCreateCommand).Execute(null);
                    return;
                }
            }

            if (parameter is IImageSource imageSource)
            {
                var type = SupportedFileTypesHelper.StorageItemToStorageItemTypes(imageSource);
                if (type is StorageItemTypes.Image or StorageItemTypes.Archive or StorageItemTypes.ArchiveFolder or StorageItemTypes.AlbamImage)
                {
                    var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
                    var result = await _messenger.NavigateAsync(nameof(ImageViewerPage), parameters);
                }
                else if (type is StorageItemTypes.Albam)
                {
                    var albamImageSource = imageSource as AlbamImageSource;
                    if (await albamImageSource.IsExistFolderOrArchiveFileAsync())
                    {
                        var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
                        var result = await _messenger.NavigateAsync(nameof(FolderListupPage), parameters);
                    }
                    else
                    {
                        var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
                        var result = await _messenger.NavigateAsync(nameof(ImageListupPage), parameters);
                    }
                }
                else if (type == StorageItemTypes.Folder)
                {
                    var containerType = await _messenger.WorkWithBusyWallAsync(async ct => await _folderContainerTypeManager.GetFolderContainerTypeWithCacheAsync((imageSource.FlattenAlbamItemInnerImageSource() as StorageItemImageSource).StorageItem as StorageFolder, ct), CancellationToken.None);
                    if (containerType == FolderContainerType.Other)
                    {
                        var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
                        var result = await _messenger.NavigateAsync(nameof(FolderListupPage), parameters);
                    }
                    else
                    {
                        var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
                        var result = await _messenger.NavigateAsync(nameof(ImageViewerPage), parameters);
                    }
                }
                else if (type == StorageItemTypes.EBook)
                {
                    var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
                    var result = await _messenger.NavigateAsync(nameof(EBookReaderPage), parameters);
                }
                else if (type == StorageItemTypes.AddFolder)
                {
                    ((ICommand)_sourceChoiceCommand).Execute(null);
                }
                else if (type == StorageItemTypes.AddAlbam)
                {
                    ((ICommand)_albamCreateCommand).Execute(null);
                }
            }
        }
    }
}
