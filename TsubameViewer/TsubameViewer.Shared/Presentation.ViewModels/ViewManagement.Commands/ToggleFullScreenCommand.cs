using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using Windows.UI.ViewManagement;
using Xamarin.Essentials;

namespace TsubameViewer.Presentation.Views.ViewManagement.Commands
{
    public sealed class ToggleFullScreenCommand : DelegateCommandBase
    {
        private ApplicationView _currentView;

        public ToggleFullScreenCommand()
        {
            _currentView = ApplicationView.GetForCurrentView();
        }
        protected override bool CanExecute(object parameter)
        {
            return true;
        }

        protected override void Execute(object parameter)
        {
            if (_currentView.IsFullScreenMode)
            {
                _currentView.ExitFullScreenMode();
            }
            else
            {
                _currentView.TryEnterFullScreenMode();
            }
        }
    }
}
