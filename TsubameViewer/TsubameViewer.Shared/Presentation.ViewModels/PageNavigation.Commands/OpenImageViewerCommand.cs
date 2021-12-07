using Prism.Commands;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain;
using TsubameViewer.Presentation.Views;
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
                    var parameters = StorageItemViewModel.CreatePageParameter(item);
                    var result = await _navigationService.NavigateAsync(nameof(ImageViewerPage), parameters, PageTransisionHelper.MakeNavigationTransitionInfoFromPageName(nameof(ImageViewerPage)));
                }
                else if (item.Type == StorageItemTypes.Folder)
                {
                    var parameters = StorageItemViewModel.CreatePageParameter(item);
                    var result = await _navigationService.NavigateAsync(nameof(ImageViewerPage), parameters, PageTransisionHelper.MakeNavigationTransitionInfoFromPageName(nameof(ImageViewerPage)));
                }
                else if (item.Type == StorageItemTypes.EBook)
                {
                    var parameters = StorageItemViewModel.CreatePageParameter(item);
                    var result = await _navigationService.NavigateAsync(nameof(EBookReaderPage), parameters, PageTransisionHelper.MakeNavigationTransitionInfoFromPageName(nameof(EBookReaderPage)));
                }
                else if (item.Type == StorageItemTypes.None)
                {
                }
            }
        }
    }
}
