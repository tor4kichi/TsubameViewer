using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.IO;
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
#nullable enable
namespace TsubameViewer.ViewModels.PageNavigation.Commands;

public sealed class OpenFolderItemCommand : CommandBase
{
    readonly IMessenger _messenger;
    readonly FolderContainerTypeManager _folderContainerTypeManager;
    readonly DisplaySettingsByPathRepository _displaySettingsByPathRepository;
    readonly SourceChoiceCommand _sourceChoiceCommand;
    readonly AlbamCreateCommand _albamCreateCommand;

    public OpenFolderItemCommand(
        IMessenger messenger,
        FolderContainerTypeManager folderContainerTypeManager,
        DisplaySettingsByPathRepository displaySettingsByPathRepository,
        SourceChoiceCommand sourceChoiceCommand,
        AlbamCreateCommand albamCreateCommand
        )
    {
        _messenger = messenger;
        _folderContainerTypeManager = folderContainerTypeManager;
        _displaySettingsByPathRepository = displaySettingsByPathRepository;
        _sourceChoiceCommand = sourceChoiceCommand;
        _albamCreateCommand = albamCreateCommand;
    }

    public override bool CanExecute(object parameter)
    {
        if (parameter is IStorageItemViewModel itemVM)
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

    public override async void Execute(object parameter)
    {
        if (parameter is IStorageItemViewModel itemVM)
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
                    if (_displaySettingsByPathRepository.GetFolderAndArchiveSettings(Path.GetDirectoryName(imageSource.Path))?.DefaultOpenMode == DefaultFolderOrArchiveOpenMode.Listup)
                    {
                        var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
                        var result = await _messenger.NavigateAsync(nameof(ImageListupPage), parameters);
                    }
                    else
                    {
                        var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
                        var result = await _messenger.NavigateAsync(nameof(ImageViewerPage), parameters);
                    }
                }
            }
            else if (type == StorageItemTypes.EBook)
            {
                var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
                var result = await _messenger.NavigateAsync(nameof(EBookViewerPage), parameters);
            }
            else if (type == StorageItemTypes.Movie)
            {
                var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
                var result = await _messenger.NavigateAsync(nameof(MovieViewerPage), parameters);
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
