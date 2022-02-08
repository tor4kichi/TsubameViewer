using I18NPortable;
using Microsoft.Toolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain.Albam;
using TsubameViewer.Presentation.Services;

namespace TsubameViewer.Presentation.ViewModels.Albam.Commands
{
    public sealed class AlbamCreateCommand : CommandBase
    {
        private readonly IMessenger _messenger;
        private readonly AlbamRepository _albamRepository;
        private readonly AlbamDialogService _albamDialogService;

        public AlbamCreateCommand(
            IMessenger messenger,
            AlbamRepository albamRepository,
            AlbamDialogService albamDialogService
            )
        {
            _messenger = messenger;
            _albamRepository = albamRepository;
            _albamDialogService = albamDialogService;
        }

        protected override bool CanExecute(object parameter)
        {
            return true;
        }

        protected override async void Execute(object parameter)
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
}
