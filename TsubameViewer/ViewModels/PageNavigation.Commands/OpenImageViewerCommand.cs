using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Views;
using Windows.UI.Xaml.Media.Animation;
#nullable enable
namespace TsubameViewer.ViewModels.PageNavigation.Commands;

public sealed class OpenImageViewerCommand : CommandBase
{
    readonly IMessenger _messenger;
    private readonly DisplaySettingsByPathRepository _displaySettingsByPathRepository;

    public OpenImageViewerCommand(
        IMessenger messenger,
        DisplaySettingsByPathRepository displaySettingsByPathRepository
        )
    {
        _messenger = messenger;
        _displaySettingsByPathRepository = displaySettingsByPathRepository;
    }

    public override bool CanExecute(object parameter)
    {
        if (parameter is IStorageItemViewModel itemVM)
        {
            parameter = itemVM.Item;
        }

        return parameter is IImageSource;
    }

    public override async void Execute(object parameter)
    {
        if (parameter is IStorageItemViewModel itemVM)
        {
            parameter = itemVM.Item;
        }

        if (parameter is IImageSource imageSource)
        {
            await imageSource.ThrowIfImageSourceStorageItemNotFound(_messenger);

            var type = SupportedFileTypesHelper.StorageItemToStorageItemTypes(imageSource);
            if (type is StorageItemTypes.Image or StorageItemTypes.Archive or StorageItemTypes.Folder or StorageItemTypes.Albam or StorageItemTypes.AlbamImage)
            {
                var parameters = PageTransitionHelper.CreatePageParameter(imageSource);
                var result = await _messenger.NavigateAsync(nameof(ImageViewerPage), parameters);
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
        }
    }
}
