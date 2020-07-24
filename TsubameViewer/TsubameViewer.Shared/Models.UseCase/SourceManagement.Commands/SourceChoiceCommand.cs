using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain.SourceManagement;

namespace TsubameViewer.Models.UseCase.SourceManagement.Commands
{
    public sealed class SourceChoiceCommand : DelegateCommandBase
    {
        private readonly StoredFoldersRepository _storedFoldersRepository;

        public SourceChoiceCommand(StoredFoldersRepository storedFoldersRepository)
        {
            _storedFoldersRepository = storedFoldersRepository;
        }
        protected override bool CanExecute(object parameter)
        {
            return true;
        }

        protected override async void Execute(object parameter)
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads;
            picker.CommitButtonText = "選択";
            picker.FileTypeFilter.Add("*");
            var seletedFolder = await picker.PickSingleFolderAsync();

            if (seletedFolder == null) { return; }

            _storedFoldersRepository.AddFolder(seletedFolder);
        }
    }
}
