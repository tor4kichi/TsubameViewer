using Microsoft.Toolkit.Mvvm.Messaging;
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
        private readonly IMessenger _messenger;

        public OpenImageViewerCommand(
            IMessenger messenger
            )
        {
            _messenger = messenger;
        }

        protected override bool CanExecute(object parameter)
        {
            return parameter is StorageItemViewModel;
        }

        protected override async void Execute(object parameter)
        {
            if (parameter is StorageItemViewModel item)
            {
                if (item.Type is StorageItemTypes.Image or StorageItemTypes.Archive or StorageItemTypes.Folder or StorageItemTypes.Albam or StorageItemTypes.AlbamImage)
                {
                    var parameters = StorageItemViewModel.CreatePageParameter(item);
                    var result = await _messenger.NavigateAsync(nameof(ImageViewerPage), parameters);
                }
                else if (item.Type == StorageItemTypes.EBook)
                {
                    var parameters = StorageItemViewModel.CreatePageParameter(item);
                    var result = await _messenger.NavigateAsync(nameof(EBookReaderPage), parameters);
                }
                else if (item.Type == StorageItemTypes.None)
                {
                }
            }
        }
    }
}
