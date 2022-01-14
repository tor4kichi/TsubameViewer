using Microsoft.Toolkit.Mvvm.Messaging;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.Albam;
using TsubameViewer.Presentation.ViewModels.PageNavigation;

namespace TsubameViewer.Presentation.ViewModels.Albam.Commands
{
    public sealed class AlbamOpenCommand : DelegateCommandBase
    {
        private readonly IMessenger _messenger;

        public AlbamOpenCommand(IMessenger messenger)
        {
            _messenger = messenger;
        }

        protected override bool CanExecute(object parameter)
        {
            return parameter is StorageItemViewModel;
        }

        protected override void Execute(object parameter)
        {
            if (parameter is StorageItemViewModel itemVM && itemVM.Item is AlbamImageSource albamImageSource)
            {
                _messenger.NavigateAsync(nameof(Views.ImageListupPage), parameters: (PageNavigationConstants.AlbamPathKey, albamImageSource.AlbamId));
            }            
        }
    }
}
