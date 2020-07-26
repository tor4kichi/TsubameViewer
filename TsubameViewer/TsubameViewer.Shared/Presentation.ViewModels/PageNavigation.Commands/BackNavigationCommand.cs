using Prism.Commands;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Attributes;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation.Commands
{
    public sealed class BackNavigationCommand : DelegateCommandBase
    {
        private INavigationService _navigationService => _lazyNavigationService.Value;
        private readonly Lazy<INavigationService> _lazyNavigationService;

        public BackNavigationCommand([Dependency("PrimaryWindowNavigationService")] Lazy<INavigationService> lazyNavigationService)
        {
            _lazyNavigationService = lazyNavigationService;
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
