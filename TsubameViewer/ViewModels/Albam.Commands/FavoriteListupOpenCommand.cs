using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Core.UseCases;
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

        protected override bool CanExecute(object parameter)
        {
            return true;
        }

        protected override void Execute(object parameter)
        {
            _messenger.NavigateAsync(nameof(Views.ImageListupPage), parameters: (Albam.AlbamNavigationConstants.Key_AlbamId, FavoriteAlbam.FavoriteAlbamId));
        }
    }
}
