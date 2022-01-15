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
using TsubameViewer.Presentation.ViewModels.PageNavigation.Commands;
using Microsoft.Toolkit.Mvvm.Messaging;
using System.Linq;

namespace TsubameViewer.Presentation.ViewModels
{
    internal sealed class AlbamListupPageViewModel : ViewModelBase
    {
        private readonly IMessenger _messenger;
        private readonly AlbamRepository _albamRepository;
        private readonly BookmarkManager _bookmarkManager;
        private readonly ImageCollectionManager _imageCollectionManager;
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly FolderListingSettings _folderListingSettings;
        private readonly ThumbnailManager _thumbnailManager;

        public ObservableCollection<StorageItemViewModel> Albams { get; } = new ();
        public OpenFolderItemCommand OpenFolderItemCommand { get; }
        public OpenImageViewerCommand OpenImageViewerCommand { get; }
        public OpenImageListupCommand OpenImageListupCommand { get; }

        private readonly StorageItemViewModel _createNewAlbamViewModel;

        public AlbamListupPageViewModel(
            IMessenger messenger,
            AlbamRepository albamRepository,
            
            OpenFolderItemCommand openFolderItemCommand,
            OpenImageViewerCommand openImageViewerCommand,
            OpenImageListupCommand openImageListupCommand,

            BookmarkManager bookmarkManager,
            ImageCollectionManager imageCollectionManager,
            SourceStorageItemsRepository sourceStorageItemsRepository,
            FolderListingSettings folderListingSettings,
            ThumbnailManager thumbnailManager
            )
        {
            _messenger = messenger;
            _albamRepository = albamRepository;
            OpenFolderItemCommand = openFolderItemCommand;
            OpenImageViewerCommand = openImageViewerCommand;
            OpenImageListupCommand = openImageListupCommand;
            _bookmarkManager = bookmarkManager;
            _imageCollectionManager = imageCollectionManager;
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
            _folderListingSettings = folderListingSettings;
            _thumbnailManager = thumbnailManager;
            _createNewAlbamViewModel = new StorageItemViewModel("CreateAlbam".Translate(), Models.Domain.StorageItemTypes.AddAlbam);
        }

        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
            _messenger.Unregister<Albam.AlbamCreatedMessage>(this);

            base.OnNavigatedFrom(parameters);
        }

        public override void OnNavigatedTo(INavigationParameters parameters)
        {
            Albams.Clear();
            Albams.Add(_createNewAlbamViewModel);
            foreach (var albam in _albamRepository.GetAlbams())
            {
                Albams.Add(new StorageItemViewModel(new AlbamImageSource(albam, new AlbamImageCollectionContext(albam, _albamRepository, _sourceStorageItemsRepository, _imageCollectionManager, _folderListingSettings, _thumbnailManager)), _sourceStorageItemsRepository, _bookmarkManager));
            }

            _messenger.Register<Albam.AlbamCreatedMessage>(this, (r, m) => 
            {
                var albam = m.Value;
                Albams.Add(new StorageItemViewModel(new AlbamImageSource(albam, new AlbamImageCollectionContext(albam, _albamRepository, _sourceStorageItemsRepository, _imageCollectionManager, _folderListingSettings, _thumbnailManager)), _sourceStorageItemsRepository, _bookmarkManager));
            });

            _messenger.Register<Albam.AlbamDeletedMessage>(this, (r, m) =>
            {
                var albamId = m.Value;
                var albam = Albams.FirstOrDefault(x => (x.Item as AlbamImageSource)?.AlbamId == albamId);
                Albams.Remove(albam);
            });

            base.OnNavigatedTo(parameters);
        }
    }

    

}
