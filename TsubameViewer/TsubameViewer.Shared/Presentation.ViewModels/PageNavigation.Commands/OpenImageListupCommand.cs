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
    public sealed class OpenImageListupCommand : DelegateCommandBase
    {
        private readonly IMessenger _messenger;

        public OpenImageListupCommand(
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
                try
                {
                    if (item.Type == StorageItemTypes.Archive)
                    {
                        var result = await _messenger.NavigateAsync(nameof(ImageListupPage), StorageItemViewModel.CreatePageParameter(item));
                    }
                    else if (item.Type == StorageItemTypes.Folder)
                    {
                        var result = await _messenger.NavigateAsync(nameof(ImageListupPage), StorageItemViewModel.CreatePageParameter(item));
                    }
                    else if (item.Type == StorageItemTypes.EBook)
                    {

                    }
                    else if (item.Type == StorageItemTypes.None)
                    {
                    }
                }
                catch
                {
                    await _messenger.NavigateAsync(nameof(SourceStorageItemsPage));
                }
            }
        }
    }
}
