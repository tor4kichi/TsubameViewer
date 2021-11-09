using Prism.Commands;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using Windows.Storage;
using Windows.UI.Xaml.Media.Animation;
using StorageItemTypes = TsubameViewer.Models.Domain.StorageItemTypes;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation.Commands
{
    public sealed class OpenListupCommand : DelegateCommandBase
    {
        private INavigationService _navigationService;
        private readonly FolderContainerTypeManager _folderContainerTypeManager;

        public OpenListupCommand(
            INavigationService navigationService,
            FolderContainerTypeManager folderContainerTypeManager
            )
        {
            _navigationService = navigationService;
            _folderContainerTypeManager = folderContainerTypeManager;
        }

        protected override bool CanExecute(object parameter)
        {
            return parameter is StorageItemViewModel;
        }

        protected override async void Execute(object parameter)
        {
            if (parameter is StorageItemViewModel item)
            {
                if (item.Type == StorageItemTypes.Archive)
                {
                    var parameters = StorageItemViewModel.CreatePageParameter(item);
                    var result = await _navigationService.NavigateAsync(nameof(Presentation.Views.ImageListupPage), parameters, new DrillInNavigationTransitionInfo());
                }
                else if (item.Type == StorageItemTypes.Folder)
                {
                    var containerType = await _folderContainerTypeManager.GetFolderContainerTypeWithCacheAsync((item.Item as StorageItemImageSource).StorageItem as StorageFolder);
                    if (containerType == FolderContainerType.Other)
                    {
                        var parameters = StorageItemViewModel.CreatePageParameter(item);
                        var result = await _navigationService.NavigateAsync(nameof(Presentation.Views.FolderListupPage), parameters, new DrillInNavigationTransitionInfo());
                    }
                    else
                    {
                        var parameters = StorageItemViewModel.CreatePageParameter(item);
                        var result = await _navigationService.NavigateAsync(nameof(Presentation.Views.ImageListupPage), parameters, new SuppressNavigationTransitionInfo());
                    }
                }
                else if (item.Type == StorageItemTypes.EBook)
                {

                }
                else if (item.Type == StorageItemTypes.None)
                {
                }
            }
        }
    }
}
