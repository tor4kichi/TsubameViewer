using Prism.Commands;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Attributes;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation.Commands
{
    public sealed class OpenPageCommand : DelegateCommandBase
    {
        private INavigationService _navigationService => _lazyNavigationService.Value;
        private readonly Lazy<INavigationService> _lazyNavigationService;

        public OpenPageCommand([Dependency("PrimaryWindowNavigationService")] Lazy<INavigationService> lazyNavigationService)
        {
            _lazyNavigationService = lazyNavigationService;
        }

        protected override bool CanExecute(object parameter)
        {
            return parameter is string;
        }

        protected override void Execute(object parameter)
        {
            if (parameter is string pageName)
            {
                _navigationService.NavigateAsync(pageName);
            }
        }
    }
}
