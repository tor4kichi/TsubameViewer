using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.UseCases;
using TsubameViewer.ViewModels.PageNavigation;

namespace TsubameViewer.ViewModels.Albam.Commands
{
    public class FavoriteRemoveCommand : ImageSourceCommandBase
    {
        private readonly FavoriteAlbam _favoriteAlbam;

        public FavoriteRemoveCommand(FavoriteAlbam favoriteAlbam)
        {
            _favoriteAlbam = favoriteAlbam;
        }

        protected override void Execute(IImageSource imageSource)
        {
            _favoriteAlbam.DeleteFavoriteItem(imageSource);
        }

        protected override void Execute(IEnumerable<IImageSource> imageSources)
        {
            base.Execute(imageSources.ToArray());
        }
    }
}
