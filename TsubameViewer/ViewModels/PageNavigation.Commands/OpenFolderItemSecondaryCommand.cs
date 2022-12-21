﻿using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Windows.Input;
using TsubameViewer.Core.Models;
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
    public sealed class OpenFolderItemSecondaryCommand : CommandBase
    {
        private readonly IMessenger _messenger;
        private readonly FolderContainerTypeManager _folderContainerTypeManager;
        private readonly SourceChoiceCommand _sourceChoiceCommand;
        private readonly AlbamCreateCommand _albamCreateCommand;

        public OpenFolderItemSecondaryCommand(
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
                if (type is StorageItemTypes.Image or StorageItemTypes.Archive)
                {
                    var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
                    var result = await _messenger.NavigateAsync(nameof(ImageListupPage), parameters);
                }
                else if (type is StorageItemTypes.Folder)
                {
                    var containerType = await _messenger.WorkWithBusyWallAsync(async ct => await _folderContainerTypeManager.GetLatestFolderContainerTypeAndUpdateCacheAsync((imageSource as StorageItemImageSource).StorageItem as StorageFolder, ct), CancellationToken.None);
                    if (containerType == FolderContainerType.Other)
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
                else if (type is StorageItemTypes.EBook)
                {
                    var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
                    var result = await _messenger.NavigateAsync(nameof(EBookReaderPage), parameters);
                }
                else if (type is StorageItemTypes.AddFolder)
                {
                    ((ICommand)_sourceChoiceCommand).Execute(null);
                }
                else if (type is StorageItemTypes.AddAlbam)
                {
                    ((ICommand)_albamCreateCommand).Execute(null);
                }
            }
        }
    }
}
