using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain.SourceFolders;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation.Commands
{
    public sealed class DeleteStoredFolderCommand : DelegateCommandBase
    {
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;

        public DeleteStoredFolderCommand(SourceStorageItemsRepository sourceStorageItemsRepository)
        {
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
        }

        protected override bool CanExecute(object parameter)
        {
            return parameter is StorageItemViewModel;
        }

        protected override void Execute(object parameter)
        {
            if (parameter is StorageItemViewModel itemVM)
            {
                _sourceStorageItemsRepository.RemoveFolder(itemVM.Token.TokenString);
            }
        }
    }
}
