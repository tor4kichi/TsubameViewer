using I18NPortable;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Contracts.Services;
#nullable enable
namespace TsubameViewer.ViewModels.Albam.Commands;

public sealed class AlbamCreateCommand : CommandBase
{
    readonly IMessenger _messenger;
    readonly AlbamRepository _albamRepository;
    readonly IAlbamDialogService _albamDialogService;

    public AlbamCreateCommand(
        IMessenger messenger,
        AlbamRepository albamRepository,
        IAlbamDialogService albamDialogService
        )
    {
        _messenger = messenger;
        _albamRepository = albamRepository;
        _albamDialogService = albamDialogService;
    }

    public override bool CanExecute(object parameter)
    {
        return true;
    }

    public override async void Execute(object parameter)
    {            
        var (isSuccess, albamName) = await _albamDialogService.GetNewAlbamNameAsync();
        if (isSuccess && string.IsNullOrEmpty(albamName) is false)
        {
            AlbamEntry createdAlbam = null;

            // Guidの衝突可能性を潰すべく数回リトライする
            int count = 0;
            while (createdAlbam == null)
            {
                if (++count >= 5)
                {
                    throw new InvalidOperationException();
                }

                try
                {
                    createdAlbam = _albamRepository.CreateAlbam(Guid.NewGuid(), albamName);
                }
                catch { }
            }

            
        }
    }
}
