using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.Navigation;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using Windows.Storage;

namespace TsubameViewer.Presentation.ViewModels.SourceFolders.Commands
{
    internal class FolderOrArchiveResturctureCommand : CommandBase
    {
        private readonly IMessenger _messeger;

        public FolderOrArchiveResturctureCommand(IMessenger messeger)
        {
            _messeger = messeger;
        }
        protected override bool CanExecute(object parameter)
        {
            return parameter is StorageItemViewModel itemVM 
                && itemVM.Type is Models.Domain.StorageItemTypes.Folder or Models.Domain.StorageItemTypes.Archive;
        }

        protected override void Execute(object parameter)
        {
            if (parameter is StorageItemViewModel itemVM)
            {
                if (itemVM.Type is Models.Domain.StorageItemTypes.Folder or Models.Domain.StorageItemTypes.Archive)
                {
                    _messeger.NavigateAsync(nameof(Views.FolderOrArchiveRestructurePage), isForgetNavigation: false, parameters: (PageNavigationConstants.GeneralPathKey, Uri.EscapeDataString(itemVM.Path)));
                }
            }
        }
    }
}
