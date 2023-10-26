using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Core.Contracts.Services;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;

namespace TsubameViewer.ViewModels.PageNavigation.Commands
{
    
    public sealed class SecondaryTileRemoveCommand : CommandBase
    {
        private readonly ISecondaryTileManager _secondaryTileManager;

        public SecondaryTileRemoveCommand(ISecondaryTileManager secondaryTileManager)
        {
            _secondaryTileManager = secondaryTileManager;
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

            if (parameter is StorageItemImageSource storageItemImageSource)
            {
                var result = await _secondaryTileManager.RemoveSecondaryTile(storageItemImageSource.StorageItem.Path);
            }
        }
    }
}
