using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Core.Contracts.Services;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
#nullable enable
namespace TsubameViewer.ViewModels.PageNavigation.Commands;


public sealed class SecondaryTileRemoveCommand : CommandBase
{
    readonly ISecondaryTileManager _secondaryTileManager;

    public SecondaryTileRemoveCommand(ISecondaryTileManager secondaryTileManager)
    {
        _secondaryTileManager = secondaryTileManager;
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

        if (parameter is StorageItemImageSource storageItemImageSource)
        {
            var result = await _secondaryTileManager.RemoveSecondaryTile(storageItemImageSource.StorageItem.Path);
        }
    }
}
