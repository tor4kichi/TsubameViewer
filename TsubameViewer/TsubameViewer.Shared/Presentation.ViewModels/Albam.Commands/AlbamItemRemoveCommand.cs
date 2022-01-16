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
            return parameter is StorageItemViewModel;
        }

        protected override void Execute(object parameter)
        {
            if (parameter is StorageItemViewModel itemVM)
            {
                if (itemVM.Item is AlbamItemImageSource albamItemImageSource)
                {
                    _albamRepository.DeleteAlbamItem(albamItemImageSource.AlbamId, albamItemImageSource.Path);
                }
            }
        }
    }
}
