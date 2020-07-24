using Microsoft.Xaml.Interactivity;
using Prism.Commands;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;
using TsubameViewer.Models.UseCase.SourceManagement.Commands;
using Windows.UI.Xaml.Media.Animation;

namespace TsubameViewer.Models.UseCase.PageNavigation.Commands
{
    public sealed class OpenFolderItemCommand : DelegateCommandBase
    {
        private readonly INavigationService _navigationService;
        private readonly SourceChoiceCommand _sourceChoiceCommand;

        public OpenFolderItemCommand(
            INavigationService navigationService,
            SourceChoiceCommand sourceChoiceCommand
            )
        {
            _navigationService = navigationService;
            _sourceChoiceCommand = sourceChoiceCommand;
        }

        protected override bool CanExecute(object parameter)
        {
            return parameter is StorageItemViewModel;
        }

        protected override async void Execute(object parameter)
        {
            if (parameter is StorageItemViewModel item)
            {
                if (item.Type == Windows.Storage.StorageItemTypes.File)
                {
                    var parameters = await StorageItemViewModel.CreatePageParameterAsync(item);
                    var result = await _navigationService.NavigateAsync(nameof(Presentation.Views.ImageCollectionViewerPage), parameters, new DrillInNavigationTransitionInfo());
                }
                else if (item.Type == Windows.Storage.StorageItemTypes.Folder)
                {
                    var parameters = await StorageItemViewModel.CreatePageParameterAsync(item);
                    var result = await _navigationService.NavigateAsync(nameof(Presentation.Views.FolderListupPage), parameters, new DrillInNavigationTransitionInfo());
                }
                else if (item.Type == Windows.Storage.StorageItemTypes.None)
                {
                    ((ICommand)_sourceChoiceCommand).Execute(null);
                }
            }
        }
    }
}
