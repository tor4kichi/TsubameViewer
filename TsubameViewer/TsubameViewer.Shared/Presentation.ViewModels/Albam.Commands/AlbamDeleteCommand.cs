using I18NPortable;
using Microsoft.Toolkit.Mvvm.Messaging;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain.Albam;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using Windows.UI.Popups;

namespace TsubameViewer.Presentation.ViewModels.Albam.Commands
{
    public sealed class AlbamDeleteCommand : DelegateCommandBase
    {
        private readonly AlbamRepository _albamRepository;
        private readonly IMessenger _messenger;

        public AlbamDeleteCommand(
            AlbamRepository albamRepository,
            IMessenger messenger
            )
        {
            _albamRepository = albamRepository;
            _messenger = messenger;
        }
        protected override bool CanExecute(object parameter)
        {
            return parameter is StorageItemViewModel itemVM && itemVM.Type == Models.Domain.StorageItemTypes.Albam;
        }

        protected override async void Execute(object parameter)
        {
            if (parameter is StorageItemViewModel itemVM && 
                itemVM.Item is AlbamImageSource albam)
            {
                if (_albamRepository.GetAlbamItemsCount(albam.AlbamId) > 0)
                {
                    var dialog = new MessageDialog("AlbamDeleteConfirmDialogText".Translate(albam.Name))
                    {
                        Commands =
                        {
                            new UICommand("Delete".Translate()),
                            new UICommand("Cancel".Translate()),
                        },
                        CancelCommandIndex = 1,
                        DefaultCommandIndex = 1,
                    };

                    if (await dialog.ShowAsync() is IUICommand command 
                        && dialog.Commands.IndexOf(command) != 0
                        )
                    {
                        return;
                    }
                }

                if (_albamRepository.DeleteAlbam(albam.AlbamId))
                {
                    _messenger.Send(new AlbamDeletedMessage(albam.AlbamId));
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
        }
    }
}
