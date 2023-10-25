using CommunityToolkit.Mvvm.Messaging;
using I18NPortable;
using System;
using TsubameViewer.Contracts.Services;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.ViewModels.PageNavigation;

namespace TsubameViewer.ViewModels.Albam.Commands
{
    public sealed class AlbamDeleteCommand : CommandBase
    {
        private readonly AlbamRepository _albamRepository;
        private readonly IMessenger _messenger;
        private readonly IMessageDialogService _messageDialogService;

        public AlbamDeleteCommand(
            AlbamRepository albamRepository,
            IMessenger messenger,
            IMessageDialogService messageDialogService 
            )
        {
            _albamRepository = albamRepository;
            _messenger = messenger;
            _messageDialogService = messageDialogService;
        }
        protected override bool CanExecute(object parameter)
        {
            if (parameter is IStorageItemViewModel itemVM)
            {
                parameter = itemVM.Item;
            }

            return parameter is AlbamImageSource albam
                && albam.AlbamId != FavoriteAlbam.FavoriteAlbamId
                ;
        }

        protected override async void Execute(object parameter)
        {
            if (parameter is IStorageItemViewModel itemVM)
            {
                parameter = itemVM.Item;
            }

            if (parameter is AlbamImageSource albam)
            {
                if (_albamRepository.GetAlbamItemsCount(albam.AlbamId) > 0)
                {
                    var result = await _messageDialogService.ShowMessageDialogAsync(
                        message: "AlbamDeleteConfirmDialogText".Translate(albam.Name),
                        CommandLabels: new[] { "Delete".Translate(), "Cancel".Translate() },
                        cancelCommandIndex: 1,
                        defaultCommandIndex: 1
                        );

                    if (result.IsConfirm == false || result.ResultCommandIndex == 1)
                    {
                        return;
                    }
                }

                if (_albamRepository.DeleteAlbam(albam.AlbamId) is false)
                {
                    throw new InvalidOperationException();
                }
            }
        }
    }
}
