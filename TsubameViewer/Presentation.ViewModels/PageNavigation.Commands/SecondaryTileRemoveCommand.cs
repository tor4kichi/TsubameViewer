using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using TsubameViewer.Presentation.Services.UWP;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation.Commands
{
    
    public sealed class SecondaryTileRemoveCommand : CommandBase
    {
        private readonly SecondaryTileManager _secondaryTileManager;

        public SecondaryTileRemoveCommand(SecondaryTileManager secondaryTileManager)
        {
            _secondaryTileManager = secondaryTileManager;
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

            if (parameter is StorageItemImageSource storageItemImageSource)
            {
                var result = await _secondaryTileManager.RemoveSecondaryTile(storageItemImageSource.StorageItem.Path);
            }
        }
    }
}
