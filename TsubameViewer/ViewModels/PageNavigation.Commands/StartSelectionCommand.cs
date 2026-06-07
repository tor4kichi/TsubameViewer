using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace TsubameViewer.ViewModels.PageNavigation.Commands
{
    public sealed class StartSelectionCommand : CommandBase
    {
        readonly IMessenger _messenger;

        public StartSelectionCommand(IMessenger messenger)
        {
            _messenger = messenger;
        }

        public override bool CanExecute(object parameter)
        {
            return true;
        }

        public override void Execute(object parameter)
        {
            _messenger.Send<StartMultiSelectionMessage>();
        }
    }
}
