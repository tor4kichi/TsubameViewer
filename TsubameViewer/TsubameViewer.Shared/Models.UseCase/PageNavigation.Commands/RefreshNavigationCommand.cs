using Prism.Commands;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Attributes;

namespace TsubameViewer.Models.UseCase.PageNavigation.Commands
{
    public sealed class RefreshNavigationCommand : DelegateCommandBase
    {
        private INavigationService _navigationService => _lazyNavigationService.Value;
        private readonly Lazy<INavigationService> _lazyNavigationService;

        public RefreshNavigationCommand([Dependency("PrimaryWindowNavigationService")] Lazy<INavigationService> lazyNavigationService)
        {
            _lazyNavigationService = lazyNavigationService;
        }

        protected override bool CanExecute(object parameter)
        {
            return true;
        }

        protected override void Execute(object parameter)
        {
            _ = _navigationService.RefreshAsync();
        }
    }
}
