using Microsoft.Toolkit.Mvvm.Messaging;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.UseCase;
using TsubameViewer.Presentation.ViewModels.PageNavigation;

namespace TsubameViewer.Presentation.ViewModels.Albam.Commands
{
    public sealed class FavoriteListupOpenCommand : DelegateCommandBase
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
            _messenger.NavigateAsync(nameof(Views.AlbamImageListupPage), parameters: (Albam.AlbamNavigationConstants.Key_AlbamId, FavoriteAlbam.FavoriteAlbamId));
        }
    }
}
