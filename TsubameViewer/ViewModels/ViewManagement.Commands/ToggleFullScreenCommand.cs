using System;
using System.Collections.Generic;
using System.Text;
using Windows.UI.ViewManagement;

namespace TsubameViewer.ViewModels.ViewManagement.Commands
{
    public sealed class ToggleFullScreenCommand : CommandBase
    {
        private ApplicationView _currentView;

        public ToggleFullScreenCommand()
        {
            _currentView = ApplicationView.GetForCurrentView();
        }
        public override bool CanExecute(object parameter)
        {
            return true;
        }

        public override void Execute(object parameter)
        {
            System.Diagnostics.Debug.WriteLine("ToggleFullScreenCommand");
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
