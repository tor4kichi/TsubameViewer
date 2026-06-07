using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.ImageViewer;

namespace TsubameViewer.ViewModels.Albam.Commands
{
    public sealed class FavoriteAddCommand : ImageSourceCommandBase
    {
        readonly FavoriteAlbam _favoriteAlbam;

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
