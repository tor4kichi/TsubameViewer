using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using TsubameViewer.Presentation.Services.UWP;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation.Commands
{
    public sealed class SecondaryTileAddCommand : DelegateCommandBase
    {
        private readonly SecondaryTileManager _secondaryTileManager;

        public SecondaryTileAddCommand(SecondaryTileManager secondaryTileManager)
        {
            _secondaryTileManager = secondaryTileManager;
        }

        protected override bool CanExecute(object parameter)
        {
            return parameter is StorageItemViewModel;
        }

        protected override async void Execute(object parameter)
        {
            if (parameter is StorageItemViewModel itemVM)
            {
                if (itemVM.Item is StorageItemImageSource storageItemImageSource)
                {
                    var subtractPath = await StorageItemViewModel.GetRawSubtractPath(itemVM);
                    var result = await _secondaryTileManager.AddSecondaryTile(itemVM.Token, subtractPath, itemVM.Name, storageItemImageSource.StorageItem);
                }
            }
        }
    }
}
