using I18NPortable;
using Prism.Commands;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain.SourceFolders;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using Unity.Attributes;

namespace TsubameViewer.Presentation.Views.SourceFolders.Commands
{
    public sealed class SourceChoiceCommand : DelegateCommandBase
    {
        private INavigationService _navigationService => _lazyNavigationService.Value;
        private readonly Lazy<INavigationService> _lazyNavigationService;
        private readonly SourceStorageItemsRepository _SourceStorageItemsRepository;

        public SourceChoiceCommand(
            [Dependency("PrimaryWindowNavigationService")] Lazy<INavigationService> lazyNavigationService,
            SourceStorageItemsRepository sourceStorageItemsRepository
            )
        {
            _lazyNavigationService = lazyNavigationService;
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
                var parameters = new NavigationParameters((PageNavigationConstants.Path, seletedFolder.Path));
                await _navigationService.NavigateAsync(nameof(Views.FolderListupPage), parameters);
            }
        }
    }
}
