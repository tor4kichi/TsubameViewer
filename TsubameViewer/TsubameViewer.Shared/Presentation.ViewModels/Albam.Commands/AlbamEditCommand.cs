using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain.Albam;
using TsubameViewer.Presentation.Services;
using TsubameViewer.Presentation.ViewModels.PageNavigation;

namespace TsubameViewer.Presentation.ViewModels.Albam.Commands
{
    public sealed class AlbamEditCommand : DelegateCommandBase
    {
        private readonly AlbamRepository _albamRepository;
        private readonly AlbamDialogService _albamDialogService;

        public AlbamEditCommand(
            AlbamRepository albamRepository,
            AlbamDialogService albamDialogService
            )
        {
            _albamRepository = albamRepository;
            _albamDialogService = albamDialogService;
        }

        protected override bool CanExecute(object parameter)
        {
            return parameter is StorageItemViewModel itemVM
                && itemVM.Type == Models.Domain.StorageItemTypes.Albam;
        }

        protected override async void Execute(object parameter)
        {
            if (parameter is StorageItemViewModel itemVM
                && itemVM.Item is AlbamImageSource albam
                )
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
