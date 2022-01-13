using Microsoft.Toolkit.Mvvm.ComponentModel;
using Prism.Mvvm;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain.Albam;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Presentation.ViewModels.Albam;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using Windows.Storage;
using Windows.Storage.Streams;

namespace TsubameViewer.Presentation.ViewModels
{
    public sealed class AlbamImageListupPageViewModel : ViewModelBase
    {
        private readonly AlbamRepository _albamRepository;

        private AlbamViewModel _albumViewModel;
        public AlbamViewModel AlbamVM
        {
            get => _albumViewModel;
            private set => SetProperty(ref _albumViewModel, value);
        }

        public ObservableCollection<AlbamItemViewModel> Items { get; }

        public AlbamImageListupPageViewModel(AlbamRepository albamRepository)
        {
            _albamRepository = albamRepository;
            Items = new ();
        }

        public override void OnNavigatedTo(INavigationParameters parameters)
        {
            try
            {
                if (parameters.TryGetValueSafe(AlbamNavigationConstants.Key_AlbamId, out Guid albamId) is false)
                {
                    throw new ArgumentException($"AlbamListupPage is require {AlbamNavigationConstants.Key_AlbamId} navigation parameters.");
                }
                
                _albumViewModel = new AlbamViewModel(_albamRepository.GetAlbam(albamId));
                foreach (var item in _albamRepository.GetAlbamItems(_albumViewModel.AlbamId))
                {
                    Items.Add(new AlbamItemViewModel(new AlbamItemImageSource(item)));
                }

                RaisePropertyChanged(nameof(AlbamVM));
            }
            catch
            {
                _albumViewModel = null;
            }

            base.OnNavigatedTo(parameters);
        }
    }

    public sealed class AlbamItemViewModel : ObservableObject
    {
        private readonly AlbamItemImageSource _imageSource;

        public AlbamItemViewModel(AlbamItemImageSource imageSource)
        {
            _imageSource = imageSource;
        }
    }
    /*
    public sealed class AlbamImageCollection : IImageCollection
    {
        public string Name => throw new NotImplementedException();

        public IEnumerable<IImageSource> GetAllImages()
        {
            throw new NotImplementedException();
        }
    }
    */

}
