using Microsoft.Toolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation.Commands
{
    public sealed class RefreshNavigationCommand : RelayCommandBase
    {
        private readonly IMessenger _messenger;

        public RefreshNavigationCommand(IMessenger messenger)
        {
            _messenger = messenger;
        }

        protected override bool CanExecute(object parameter)
        {
            return true;
        }

        protected override void Execute(object parameter)
        {
            _messenger.Send<RefreshNavigationRequestMessage>();
        }
    }
}
