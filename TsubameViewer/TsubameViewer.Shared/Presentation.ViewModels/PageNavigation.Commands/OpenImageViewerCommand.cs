using Prism.Commands;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain;
using Windows.UI.Xaml.Media.Animation;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation.Commands
{
    public sealed class OpenImageViewerCommand : DelegateCommandBase
    {
        private INavigationService _navigationService;

        public OpenImageViewerCommand(
            INavigationService navigationService
            )
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
                    var result = await _navigationService.NavigateAsync(nameof(Presentation.Views.ImageViewerPage), parameters, new SuppressNavigationTransitionInfo());
                }
                else if (item.Type == StorageItemTypes.Folder)
                {
                    var parameters = await StorageItemViewModel.CreatePageParameterAsync(item);
                    var result = await _navigationService.NavigateAsync(nameof(Presentation.Views.ImageViewerPage), parameters, new SuppressNavigationTransitionInfo());
                }
                else if (item.Type == StorageItemTypes.EBook)
                {
                    var parameters = await StorageItemViewModel.CreatePageParameterAsync(item);
                    var result = await _navigationService.NavigateAsync(nameof(Presentation.Views.EBookReaderPage), parameters, new SuppressNavigationTransitionInfo());
                }
                else if (item.Type == StorageItemTypes.None)
                {
                }
            }
        }
    }
}
