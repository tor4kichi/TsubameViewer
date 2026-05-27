using CommunityToolkit.Mvvm.Messaging;
using TsubameViewer.Core.Models;
using TsubameViewer.ViewModels.PageNavigation;

namespace TsubameViewer.ViewModels.Albam.Commands
{
    public sealed class FavoriteListupOpenCommand : CommandBase
    {
        private readonly IMessenger _messenger;

        public FavoriteListupOpenCommand(IMessenger messenger)
        {
            _messenger = messenger;
        }

        public override bool CanExecute(object parameter)
        {
            return true;
        }

        public override void Execute(object parameter)
        {
            _messenger.NavigateAsync(nameof(Views.ImageListupPage), parameters: (Albam.AlbamNavigationConstants.Key_AlbamId, FavoriteAlbam.FavoriteAlbamId));
        }
    }
}
