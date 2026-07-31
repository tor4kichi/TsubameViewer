using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Services;
using Windows.UI.ViewManagement;

namespace TsubameViewer.ViewModels.ViewManagement.Commands;

public sealed class ToggleFullScreenCommand : CommandBase
{
    private readonly SecondaryWindowService _secondaryWindowService;
    private readonly IWindowManagementAware _windowContext;

    public ToggleFullScreenCommand(SecondaryWindowService secondaryWindowService)
    {
        _secondaryWindowService = secondaryWindowService;
        _windowContext = _secondaryWindowService.GetCurentFocusWindow();
    }
    public override bool CanExecute(object parameter)
    {
        return true;
    }

    public override void Execute(object parameter)
    {
        if (_windowContext.IsFullScreenMode)
        {
            _windowContext.ExitFullScreenMode();
        }
        else
        {
            _windowContext.TryEnterFullScreenMode();
        }
    }
}
