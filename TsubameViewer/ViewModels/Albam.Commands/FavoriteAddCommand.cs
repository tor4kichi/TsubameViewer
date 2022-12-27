using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.UseCases;
using TsubameViewer.ViewModels.PageNavigation;

namespace TsubameViewer.ViewModels.Albam.Commands
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
            _favoriteAlbam.AddFavoriteItem(imageSource);
        }
    }
}
