using Microsoft.Toolkit.Mvvm.Messaging;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain.SourceFolders;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation.Commands
{
    public sealed class DeleteStoredFolderCommand : DelegateCommandBase
    {
        private readonly IMessenger _messenger;

        public DeleteStoredFolderCommand(IMessenger messenger)
        {
            _messenger = messenger;
        }

        protected override bool CanExecute(object parameter)
        {
            return parameter is StorageItemViewModel;
        }

        protected override void Execute(object parameter)
        {
            if (parameter is StorageItemViewModel itemVM)
            {
                _messenger.Send<SourceStorageItemIgnoringRequestMessage>(new (itemVM.Path));
            }
        }
    }
}
