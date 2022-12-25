using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Contracts.Services;

using TsubameViewer.ViewModels.PageNavigation;

namespace TsubameViewer.ViewModels.Albam.Commands
{
    public sealed class AlbamEditCommand : CommandBase
    {
        private readonly AlbamRepository _albamRepository;
        private readonly IAlbamDialogService _albamDialogService;

        public AlbamEditCommand(
            AlbamRepository albamRepository,
            IAlbamDialogService albamDialogService
            )
        {
            _albamRepository = albamRepository;
            _albamDialogService = albamDialogService;
        }

        protected override bool CanExecute(object parameter)
        {
            if (parameter is StorageItemViewModel itemVM)
            {
                parameter = itemVM.Item;
            }

            return parameter is AlbamImageSource;
        }

        protected override async void Execute(object parameter)
        {
            if (parameter is StorageItemViewModel itemVM)
            {
                parameter = itemVM.Item;
            }

            if (parameter is AlbamImageSource albam)
            {
                var result = await _albamDialogService.EditAlbamAsync(albam.Name);
                if (result.isEdited)
                {
                    if (string.IsNullOrWhiteSpace(result.Rename) is false)
                    {
                        _albamRepository.UpdateAlbam(albam.AlbamEntry with { Name = result.Rename });
                    }
                }
            }
        }
    }
}
