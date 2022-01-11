using I18NPortable;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Prism.Mvvm;
using Prism.Navigation;
using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Models.Domain.Albam;
using TsubameViewer.Models.UseCase;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using TsubameViewer.Presentation.ViewModels.Albam;

namespace TsubameViewer.Presentation.ViewModels
{
    internal sealed class AlbamPageViewModel : ViewModelBase
    {
        private readonly AlbamRepository _albamRepository;

        public ObservableCollection<object> Albams { get; } = new ();

        private readonly CreateNewAlbamViewModel _createNewAlbamViewModel;

        public AlbamPageViewModel(AlbamRepository albamRepository)
        {
            _albamRepository = albamRepository;

            _createNewAlbamViewModel = new();
        }        

        public override void OnNavigatedTo(INavigationParameters parameters)
        {
            Albams.Clear();
            Albams.Add(_createNewAlbamViewModel);
            foreach (var albam in _albamRepository.GetAlbams())
            {
                Albams.Add(new AlbamViewModel(albam));
            }

            base.OnNavigatedTo(parameters);
        }
    }

    internal sealed class CreateNewAlbamViewModel
    {

    }

}
