using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Core.Services;
using TsubameViewer.ViewModels.PageNavigation;

namespace TsubameViewer.ViewModels.SourceFolders.Commands;

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
            && itemVM.Type is Core.Models.StorageItemTypes.Folder or Core.Models.StorageItemTypes.Archive;
    }

    protected override void Execute(object parameter)
    {
        if (parameter is StorageItemViewModel itemVM)
        {
            if (itemVM.Type is Core.Models.StorageItemTypes.Folder or Core.Models.StorageItemTypes.Archive)
            {
                _messeger.NavigateAsync(nameof(Views.FolderOrArchiveRestructurePage), isForgetNavigation: false, parameters: (PageNavigationConstants.GeneralPathKey, Uri.EscapeDataString(itemVM.Path)));
            }
        }
    }
}
