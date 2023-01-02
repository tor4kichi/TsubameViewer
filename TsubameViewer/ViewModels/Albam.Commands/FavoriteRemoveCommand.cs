using System.Collections.Generic;
using System.Linq;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.ImageViewer;

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
