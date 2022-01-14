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
using TsubameViewer.Presentation.ViewModels.Albam.Commands;
using TsubameViewer.Models.Domain.SourceFolders;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ReadingFeature;

namespace TsubameViewer.Presentation.ViewModels
{
    internal sealed class AlbamListupPageViewModel : ViewModelBase
    {
        private readonly AlbamRepository _albamRepository;
        private readonly BookmarkManager _bookmarkManager;
        private readonly ImageCollectionManager _imageCollectionManager;
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly FolderListingSettings _folderListingSettings;
        private readonly ThumbnailManager _thumbnailManager;

        public ObservableCollection<object> Albams { get; } = new ();
        public AlbamOpenCommand AlbamOpenCommand { get; }

        private readonly CreateNewAlbamViewModel _createNewAlbamViewModel;

        public AlbamListupPageViewModel(
            AlbamRepository albamRepository,
            Albam.Commands.AlbamOpenCommand albamOpenCommand,

            BookmarkManager bookmarkManager,
            ImageCollectionManager imageCollectionManager,
            SourceStorageItemsRepository sourceStorageItemsRepository,
            FolderListingSettings folderListingSettings,
            ThumbnailManager thumbnailManager
            )
        {
            _albamRepository = albamRepository;
            AlbamOpenCommand = albamOpenCommand;
            _bookmarkManager = bookmarkManager;
            _imageCollectionManager = imageCollectionManager;
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
            _folderListingSettings = folderListingSettings;
            _thumbnailManager = thumbnailManager;
            _createNewAlbamViewModel = new();
        }        

        public override void OnNavigatedTo(INavigationParameters parameters)
        {
            Albams.Clear();
            Albams.Add(_createNewAlbamViewModel);
            foreach (var albam in _albamRepository.GetAlbams())
            {
                Albams.Add(new StorageItemViewModel(new AlbamImageSource(albam, new AlbamImageCollectionContext(albam, _albamRepository, _sourceStorageItemsRepository, _imageCollectionManager, _folderListingSettings, _thumbnailManager)), _sourceStorageItemsRepository, _bookmarkManager));
            }

            base.OnNavigatedTo(parameters);
        }
    }

    internal sealed class CreateNewAlbamViewModel
    {

    }

}
