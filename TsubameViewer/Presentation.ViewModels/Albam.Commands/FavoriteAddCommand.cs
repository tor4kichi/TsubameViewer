using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain.Albam;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Models.UseCase;
using TsubameViewer.Presentation.ViewModels.PageNavigation;

namespace TsubameViewer.Presentation.ViewModels.Albam.Commands
{
    public sealed class FavoriteAddCommand : ImageSourceCommandBase
    {
        private readonly FavoriteAlbam _favoriteAlbam;

        public FavoriteAddCommand(FavoriteAlbam favoriteAlbam)
        {
            _favoriteAlbam = favoriteAlbam;
        }
        
        protected override void Execute(IImageSource imageSource)
        {
            _favoriteAlbam.AddFavoriteItem(imageSource.Path, imageSource.Name);
        }
    }
}
