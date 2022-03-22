using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using TsubameViewer.Presentation.Services;
using TsubameViewer.Presentation.Services.UWP;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation.Commands
{
    public sealed class SecondaryTileAddCommand : CommandBase
    {
        private readonly ISecondaryTileManager _secondaryTileManager;

        public SecondaryTileAddCommand(ISecondaryTileManager secondaryTileManager)
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

            if (parameter is IImageSource imageSource)
            {
                if (imageSource is StorageItemImageSource storageItemImageSource)
                {
                    var tileArguments = new SecondaryTileArguments()
                    {
                        Path = imageSource.Path,
                    };

                    var result = await _secondaryTileManager.AddSecondaryTile(
                        tileArguments, 
                        imageSource.Name, 
                        storageItemImageSource.StorageItem
                        );
                }
            }
        }
    }
}
