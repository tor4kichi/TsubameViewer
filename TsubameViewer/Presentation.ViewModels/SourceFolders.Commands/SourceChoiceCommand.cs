using I18NPortable;
using Microsoft.Toolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.SourceFolders;
using TsubameViewer.Presentation.Navigations;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using TsubameViewer.Presentation.Views;

namespace TsubameViewer.Presentation.ViewModels.SourceFolders.Commands
{
    public sealed class SourceChoiceCommand : CommandBase
    {
        private readonly IMessenger _messenger;
        private readonly SourceStorageItemsRepository _SourceStorageItemsRepository;

        public SourceChoiceCommand(
            IMessenger messenger,
            SourceStorageItemsRepository sourceStorageItemsRepository
            )
        {
            _messenger = messenger;
            _SourceStorageItemsRepository = sourceStorageItemsRepository;
        }

        public bool OpenAfterChoice { get; set; } = false;

        protected override bool CanExecute(object parameter)
        {
            return true;
        }

        protected override async void Execute(object parameter)
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads;
            picker.CommitButtonText = "SelectSourceFolder".Translate(); 
            picker.FileTypeFilter.Add("*");
            var seletedFolder = await picker.PickSingleFolderAsync();

            if (seletedFolder == null) { return; }

            var token = await _SourceStorageItemsRepository.AddItemPersistantAsync(seletedFolder, SourceOriginConstants.ChoiceDialog);

            if (OpenAfterChoice && token != null)
            {
                await _messenger.NavigateAsync(nameof(FolderListupPage), new NavigationParameters((PageNavigationConstants.GeneralPathKey, seletedFolder.Path)));
            }
        }
    }
}
