using Microsoft.Toolkit.Mvvm.Messaging;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain.Albam;
using TsubameViewer.Presentation.ViewModels.PageNavigation;

namespace TsubameViewer.Presentation.ViewModels.Albam.Commands
{
    public sealed class AlbamItemRemoveCommand : DelegateCommandBase
    {
        private readonly IMessenger _messenger;
        private readonly AlbamRepository _albamRepository;

        public AlbamItemRemoveCommand(
            IMessenger messenger,
            AlbamRepository albamRepository            
            )
        {
            _messenger = messenger;
            _albamRepository = albamRepository;
        }

        protected override bool CanExecute(object parameter)
        {
            if (parameter is StorageItemViewModel itemVM)
            {
                parameter = itemVM.Item;
            }

            return parameter is AlbamItemImageSource;
        }

        protected override void Execute(object parameter)
        {
            if (parameter is StorageItemViewModel itemVM)
            {
                parameter = itemVM.Item;
            }

            if (parameter is AlbamItemImageSource albamItem)
            {
                _albamRepository.DeleteAlbamItem(albamItem.AlbamId, albamItem.Path);
            }
        }
    }
}
