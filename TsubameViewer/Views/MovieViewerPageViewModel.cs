using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Services.Navigation;
#nullable enable
namespace TsubameViewer.Views;

public sealed partial class MovieViewerPageViewModel : NavigationAwareViewModelBase
{
    public override async Task OnNavigatedToAsync(INavigationParameters parameters, CancellationToken ct)
    {

    }

    public override void OnNavigatedFrom(INavigationParameters parameters)
    {
        base.OnNavigatedFrom(parameters);
    }
}
