using System;
using System.Collections.Generic;
using System.Text;
using Xamarin.Essentials;
using TsubameViewer.Presentation.ViewModels;
using Windows.UI.ViewManagement;
using Microsoft.UI.Windowing;

namespace TsubameViewer.Presentation.Views.ViewManagement.Commands
{
    public sealed class ToggleFullScreenCommand : RelayCommandBase
    {
        private AppWindow _currentView;

        public ToggleFullScreenCommand()
        {
            _currentView = App.Current.AppWindow;
        }
        protected override bool CanExecute(object parameter)
        {
            return true;
        }

        protected override void Execute(object parameter)
        {
            System.Diagnostics.Debug.WriteLine("ToggleFullScreenCommand");
            if (_currentView.Presenter.Kind == AppWindowPresenterKind.FullScreen)
            {
                _currentView.SetPresenter(AppWindowPresenterKind.Default);
            }
            else
            {
                _currentView.SetPresenter(AppWindowPresenterKind.FullScreen);
            }
        }
    }
}
