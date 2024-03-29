﻿using CommunityToolkit.Mvvm.Messaging;
using I18NPortable;
using System.Collections.ObjectModel;
using System.Linq;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.Services.Navigation;
using TsubameViewer.ViewModels.PageNavigation.Commands;

namespace TsubameViewer.ViewModels;

internal sealed class AlbamListupPageViewModel : NavigationAwareViewModelBase
{
    private readonly IMessenger _messenger;
    private readonly AlbamRepository _albamRepository;
    private readonly LocalBookmarkRepository _bookmarkManager;
    private readonly ImageCollectionManager _imageCollectionManager;
    private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
    private readonly ThumbnailImageManager _thumbnailManager;

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
        LocalBookmarkRepository bookmarkManager,
        ImageCollectionManager imageCollectionManager,
        SourceStorageItemsRepository sourceStorageItemsRepository,            
        ThumbnailImageManager thumbnailManager
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
        _thumbnailManager = thumbnailManager;
        _createNewAlbamViewModel = new StorageItemViewModel("CreateAlbam".Translate(), Core.Models.StorageItemTypes.AddAlbam);
    }

    public override void OnNavigatedFrom(INavigationParameters parameters)
    {
        _messenger.Unregister<AlbamCreatedMessage>(this);
        _messenger.Unregister<AlbamDeletedMessage>(this);

        base.OnNavigatedFrom(parameters);
    }

    public override void OnNavigatedTo(INavigationParameters parameters)
    {
        Albams.Clear();
        Albams.Add(_createNewAlbamViewModel);
        foreach (var albam in _albamRepository.GetAlbams())
        {
            Albams.Add(new StorageItemViewModel(new AlbamImageSource(albam, new AlbamImageCollectionContext(albam, _albamRepository, _sourceStorageItemsRepository, _imageCollectionManager, _messenger)), _messenger, _sourceStorageItemsRepository, _bookmarkManager, _thumbnailManager, _albamRepository));
        }

        _messenger.Register<AlbamCreatedMessage>(this, (r, m) => 
        {
            var albam = m.Value;
            Albams.Add(new StorageItemViewModel(new AlbamImageSource(albam, new AlbamImageCollectionContext(albam, _albamRepository, _sourceStorageItemsRepository, _imageCollectionManager, _messenger)), _messenger, _sourceStorageItemsRepository, _bookmarkManager, _thumbnailManager, _albamRepository));
        });

        _messenger.Register<AlbamDeletedMessage>(this, (r, m) =>
        {
            var albamId = m.Value;
            var albam = Albams.Skip(1).FirstOrDefault(x => (x.Item as AlbamImageSource).AlbamId == albamId);
            if (albam is not null)
            {
                albam.Dispose();
                Albams.Remove(albam);
            }
        });

        _messenger.Register<AlbamEditedMessage>(this, (r, m) =>
        {
            var albam = m.Value;
            var albamVM = Albams.Skip(1).FirstOrDefault(x => (x.Item as AlbamImageSource).AlbamId == albam._id);
            if (albamVM is not null)
            {
                var index = Albams.IndexOf(albamVM);
                Albams.Remove(albamVM);
                albamVM.Dispose();
                Albams.Insert(index, new StorageItemViewModel(new AlbamImageSource(albam, new AlbamImageCollectionContext(albam, _albamRepository, _sourceStorageItemsRepository, _imageCollectionManager, _messenger)), _messenger, _sourceStorageItemsRepository, _bookmarkManager, _thumbnailManager, _albamRepository));
            }
        });

        base.OnNavigatedTo(parameters);
    }
}


