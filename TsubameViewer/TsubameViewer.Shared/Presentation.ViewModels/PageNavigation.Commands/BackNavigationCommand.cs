using Microsoft.Toolkit.Mvvm.Messaging;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Text;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation.Commands
{
    public sealed class BackNavigationCommand : DelegateCommandBase
    {
        private readonly IMessenger _messenger;

        public BackNavigationCommand(IMessenger messenger)
        {
            _messenger = messenger;
        }

        protected override bool CanExecute(object parameter)
        {
            return true;
        }

        protected override void Execute(object parameter)
        {
            _messenger.Send<BackNavigationRequestMessage>();
        }
    }
}
