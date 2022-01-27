﻿using Microsoft.Toolkit.Mvvm.Messaging;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Text;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation.Commands
{
    public sealed class StartSelectionCommand : DelegateCommandBase
    {
        private readonly IMessenger _messenger;

        public StartSelectionCommand(IMessenger messenger)
        {
            _messenger = messenger;
        }

        protected override bool CanExecute(object parameter)
        {
            return true;
        }

        protected override void Execute(object parameter)
        {
            _messenger.Send<StartMultiSelectionMessage>();
        }
    }
}
