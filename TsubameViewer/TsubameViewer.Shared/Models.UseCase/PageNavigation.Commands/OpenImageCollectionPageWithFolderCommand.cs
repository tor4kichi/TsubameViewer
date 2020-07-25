using Prism.Commands;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain.FolderItemListing;
using Windows.UI.Xaml.Media.Animation;

namespace TsubameViewer.Models.UseCase.PageNavigation.Commands
{    
    public sealed class OpenImageCollectionPageCommand : DelegateCommandBase
    {
        private readonly INavigationService _navigationService;

        public OpenImageCollectionPageCommand(INavigationService navigationService)
        {
            _navigationService = navigationService;
        }

        protected override bool CanExecute(object parameter)
        {
            return parameter is StorageItemViewModel;
        }

        protected override async void Execute(object parameter)
        {
            if (parameter is StorageItemViewModel item)
            {
                if (item.Type == StorageItemTypes.Image || item.Type == StorageItemTypes.Archive)
                {
                    var parameters = await StorageItemViewModel.CreatePageParameterAsync(item);
                    var result = await _navigationService.NavigateAsync(nameof(Presentation.Views.ImageCollectionViewerPage), parameters, new DrillInNavigationTransitionInfo());
                }
                else if (item.Type == StorageItemTypes.Folder)
                {
                    var parameters = await StorageItemViewModel.CreatePageParameterAsync(item);
                    var result = await _navigationService.NavigateAsync(nameof(Presentation.Views.ImageCollectionViewerPage), parameters, new DrillInNavigationTransitionInfo());
                }
            }
        }
    }
}
