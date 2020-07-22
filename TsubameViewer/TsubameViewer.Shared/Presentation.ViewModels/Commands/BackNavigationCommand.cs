using Prism.Commands;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Text;

namespace TsubameViewer.Presentation.ViewModels.Commands
{
    public sealed class BackNavigationCommand : DelegateCommandBase
    {
        private readonly INavigationService _navigationService;

        public BackNavigationCommand(INavigationService navigationService)
        {
            _navigationService = navigationService;
        }

        protected override bool CanExecute(object parameter)
        {
            return _navigationService.CanGoBack();
        }

        protected override void Execute(object parameter)
        {
            _ = _navigationService.GoBackAsync();
        }
    }
}
