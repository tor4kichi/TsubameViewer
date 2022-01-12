using Microsoft.Toolkit.Mvvm.Messaging;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Text;
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
            return parameter is AlbamViewModel;
        }

        protected override void Execute(object parameter)
        {
            if (parameter is AlbamViewModel albamVM)
            {
                _messenger.NavigateAsync(nameof(Views.AlbamImageListupPage), parameters: (Albam.AlbamNavigationConstants.Key_AlbamId, albamVM.AlbamId));
            }            
        }
    }
}
