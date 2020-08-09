using Microsoft.Xaml.Interactivity;
using Prism.Commands;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using TsubameViewer.Presentation.Views.SourceFolders.Commands;
using Windows.Storage;
using Windows.UI.Xaml.Media.Animation;
using StorageItemTypes = TsubameViewer.Models.Domain.StorageItemTypes;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation.Commands
{
    public sealed class OpenFolderItemCommand : DelegateCommandBase
    {
        private readonly INavigationService _navigationService;
        private readonly FolderContainerTypeManager _folderContainerTypeManager;
        private readonly SourceChoiceCommand _sourceChoiceCommand;

        public OpenFolderItemCommand(
            INavigationService navigationService,
            FolderContainerTypeManager folderContainerTypeManager,
            SourceChoiceCommand sourceChoiceCommand
            )
        {
            _navigationService = navigationService;
            _folderContainerTypeManager = folderContainerTypeManager;
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
                if (item.Type == StorageItemTypes.Image || item.Type == StorageItemTypes.Archive)
                {
                    var parameters = await StorageItemViewModel.CreatePageParameterAsync(item);
                    var result = await _navigationService.NavigateAsync(nameof(Presentation.Views.ImageViewerPage), parameters, new SuppressNavigationTransitionInfo());
                }
                else if (item.Type == StorageItemTypes.Folder)
                {
                    var containerType = await _folderContainerTypeManager.GetFolderContainerType((item.Item as StorageItemImageSource).StorageItem as StorageFolder);
                    if (containerType == FolderContainerType.Other)
                    {
                        var parameters = await StorageItemViewModel.CreatePageParameterAsync(item);
                        var result = await _navigationService.NavigateAsync(nameof(Presentation.Views.FolderListupPage), parameters, new DrillInNavigationTransitionInfo());
                    }
                    else
                    {
                        var parameters = await StorageItemViewModel.CreatePageParameterAsync(item);
                        var result = await _navigationService.NavigateAsync(nameof(Presentation.Views.ImageViewerPage), parameters, new SuppressNavigationTransitionInfo());
                    }
                }
                else if (item.Type == StorageItemTypes.EBook)
                {
                    var parameters = await StorageItemViewModel.CreatePageParameterAsync(item);
                    var result = await _navigationService.NavigateAsync(nameof(Presentation.Views.EBookReaderPage), parameters, new SuppressNavigationTransitionInfo());
                }
                else if (item.Type == StorageItemTypes.None)
                {
                    ((ICommand)_sourceChoiceCommand).Execute(null);
                }
            }
        }
    }
}
