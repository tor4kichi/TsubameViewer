using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Presentation.ViewModels.PageNavigation;

namespace TsubameViewer.Presentation.ViewModels.Albam.Commands
{
    internal class AlbamEditCommand : DelegateCommandBase
    {
        protected override bool CanExecute(object parameter)
        {
            return parameter is StorageItemViewModel;
        }

        protected override void Execute(object parameter)
        {
            if (parameter is StorageItemViewModel itemVM)
            {
                throw new NotImplementedException();
            }
        }
    }
}
