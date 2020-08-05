using Prism.Commands;
using Prism.Events;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Attributes;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation.Commands
{
    public sealed class BackNavigationCommand : DelegateCommandBase
    {
        private readonly IEventAggregator _eventAggregator;

        public BackNavigationCommand(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
        }

        protected override bool CanExecute(object parameter)
        {
            return true;
        }

        protected override void Execute(object parameter)
        {
            _eventAggregator.GetEvent<BackNavigationRequestEvent>().Publish();
        }
    }
}
