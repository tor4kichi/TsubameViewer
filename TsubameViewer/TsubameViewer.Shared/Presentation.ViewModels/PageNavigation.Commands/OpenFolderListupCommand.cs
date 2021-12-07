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
    public sealed class OpenFolderListupCommand : DelegateCommandBase
    {
        private INavigationService _navigationService;

        public OpenFolderListupCommand(
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
                if (item.Type is StorageItemTypes.Archive or StorageItemTypes.Folder or StorageItemTypes.ArchiveFolder)
                {
                    var parameters = StorageItemViewModel.CreatePageParameter(item);
                    var result = await _navigationService.NavigateAsync(nameof(FolderListupPage), parameters, PageTransisionHelper.MakeNavigationTransitionInfoFromPageName(nameof(FolderListupPage)));
                }
            }
        }
    }
}
