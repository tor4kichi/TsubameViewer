using CommunityToolkit.Mvvm.Messaging;
using I18NPortable;
using System;
using TsubameViewer.Contracts.Services;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.ViewModels.PageNavigation;

#nullable enable
namespace TsubameViewer.ViewModels.Albam.Commands;

public sealed class AlbamDeleteCommand : CommandBase
{
    readonly AlbamRepository _albamRepository;
    readonly IMessenger _messenger;
    readonly IMessageDialogService _messageDialogService;

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
    public override bool CanExecute(object parameter)
    {
        if (parameter is IStorageItemViewModel itemVM)
        {
            parameter = itemVM.Item;
        }

        return parameter is AlbamImageSource albam
            && albam.AlbamId != FavoriteAlbam.FavoriteAlbamId
            ;
    }

    public override async void Execute(object parameter)
    {
        if (parameter is IStorageItemViewModel itemVM)
        {
            parameter = itemVM.Item;
        }

        if (parameter is AlbamImageSource albam)
        {
            if (_albamRepository.GetAlbamItemsCount(albam.AlbamId) > 0)
            {
                if (!await _messageDialogService.ShowMessageDialogAsync(
                    "AlbamDeleteConfirmDialogText".Translate(albam.Name),
                    "Delete".Translate(),
                    "Cancel".Translate(),
                    true
                    ))
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
