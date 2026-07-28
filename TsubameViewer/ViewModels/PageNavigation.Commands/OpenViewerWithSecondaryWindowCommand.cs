using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Core.Models;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Services;
using TsubameViewer.Services.Navigation;
using TsubameViewer.Views;

namespace TsubameViewer.ViewModels.PageNavigation.Commands;

internal class OpenViewerWithSecondaryWindowCommand : ImageSourceCommandBase
{
    private readonly SecondaryWindowService _secondaryWindowService;

    public OpenViewerWithSecondaryWindowCommand(SecondaryWindowService secondaryWindowService)
    {
        _secondaryWindowService = secondaryWindowService;
    }

    protected override void Execute(IImageSource imageSource)
    {
        _secondaryWindowService.OpenViewerAsync(imageSource).FireAndForgetSafe();
    }
}
