﻿using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain.SourceFolders;

namespace TsubameViewer.Presentation.Views.SourceFolders.Commands
{
    public sealed class SourceChoiceCommand : DelegateCommandBase
    {
        private readonly SourceStorageItemsRepository _SourceStorageItemsRepository;

        public SourceChoiceCommand(SourceStorageItemsRepository sourceStorageItemsRepository)
        {
            _SourceStorageItemsRepository = sourceStorageItemsRepository;
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
            picker.CommitButtonText = "選択"; // TODO: 選択 -> i18n
            picker.FileTypeFilter.Add("*");
            var seletedFolder = await picker.PickSingleFolderAsync();

            if (seletedFolder == null) { return; }

            await _SourceStorageItemsRepository.AddItemPersistantAsync(seletedFolder, SourceOriginConstants.ChoiceDialog);
        }
    }
}
