using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain.Albam;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Models.UseCase;
using TsubameViewer.Presentation.ViewModels.PageNavigation;

namespace TsubameViewer.Presentation.ViewModels.Albam.Commands
{
    public class FavoriteRemoveCommand : DelegateCommandBase
    {
        private readonly FavoriteAlbam _favoriteAlbam;

        public FavoriteRemoveCommand(FavoriteAlbam favoriteAlbam)
        {
            _favoriteAlbam = favoriteAlbam;
        }
        protected override bool CanExecute(object parameter)
        {
            if (parameter is StorageItemViewModel itemVM)
            {
                parameter = itemVM.Item;
            }

            return parameter is IImageSource;
        }

        protected override void Execute(object parameter)
        {
            if (parameter is StorageItemViewModel itemVM)
            {
                parameter = itemVM.Item;
            }

            if (parameter is IImageSource imageSource)
            {
                _favoriteAlbam.DeleteFavoriteItem(imageSource.Path);
            }
        }
    }
}
