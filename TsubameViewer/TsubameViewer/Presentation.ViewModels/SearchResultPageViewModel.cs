using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.ReadingFeature;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using TsubameViewer.Models.Domain.SourceFolders;
using TsubameViewer.Presentation.Services.UWP;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using TsubameViewer.Presentation.ViewModels.PageNavigation.Commands;
using TsubameViewer.Presentation.Views;
using Windows.Storage;
using TsubameViewer.Models.Domain.Albam;

namespace TsubameViewer.Presentation.ViewModels
{
    public sealed class SearchResultPageViewModel : NavigationAwareViewModelBase
    {
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly FolderListingSettings _folderListingSettings;
        private readonly BookmarkManager _bookmarkManager;
        private readonly AlbamRepository _albamRepository;
        private readonly ThumbnailManager _thumbnailManager;

        public ObservableCollection<StorageItemViewModel> SearchResultItems { get; } = new ObservableCollection<StorageItemViewModel>();

        private string _SearchText;
        public string SearchText
        {
            get { return _SearchText; }
            set { SetProperty(ref _SearchText, value); }
        }

        public SecondaryTileManager SecondaryTileManager { get; }
        public OpenFolderItemCommand OpenFolderItemCommand { get; }
        public OpenFolderItemSecondaryCommand OpenFolderItemSecondaryCommand { get; }
        public OpenImageViewerCommand OpenImageViewerCommand { get; }
        public OpenImageListupCommand OpenImageListupCommand { get; }
        public OpenFolderListupCommand OpenFolderListupCommand { get; }
        public OpenWithExplorerCommand OpenWithExplorerCommand { get; }
        public SecondaryTileAddCommand SecondaryTileAddCommand { get; }
        public SecondaryTileRemoveCommand SecondaryTileRemoveCommand { get; }

        public SearchResultPageViewModel(
            SourceStorageItemsRepository sourceStorageItemsRepository,
            FolderListingSettings folderListingSettings,
            BookmarkManager bookmarkManager,
            AlbamRepository albamRepository,
            ThumbnailManager thumbnailManager,
            SecondaryTileManager secondaryTileManager,

            OpenFolderItemCommand openFolderItemCommand,
            OpenFolderItemSecondaryCommand openFolderItemSecondaryCommand,
            OpenImageViewerCommand openImageViewerCommand,
            OpenImageListupCommand openImageListupCommand,
            OpenFolderListupCommand openFolderListupCommand,
            OpenWithExplorerCommand openWithExplorerCommand,
            SecondaryTileAddCommand secondaryTileAddCommand,
            SecondaryTileRemoveCommand secondaryTileRemoveCommand
            )
        {
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
            _folderListingSettings = folderListingSettings;
            _bookmarkManager = bookmarkManager;
            _albamRepository = albamRepository;
            _thumbnailManager = thumbnailManager;
            SecondaryTileManager = secondaryTileManager;
            OpenFolderItemCommand = openFolderItemCommand;
            OpenFolderItemSecondaryCommand = openFolderItemSecondaryCommand;
            OpenImageViewerCommand = openImageViewerCommand;
            OpenImageListupCommand = openImageListupCommand;
            OpenFolderListupCommand = openFolderListupCommand;
            OpenWithExplorerCommand = openWithExplorerCommand;
            SecondaryTileAddCommand = secondaryTileAddCommand;
            SecondaryTileRemoveCommand = secondaryTileRemoveCommand;
        }

        CancellationTokenSource _navigationCts;


        public override async Task OnNavigatedToAsync(INavigationParameters parameters)
        {
            SearchResultItems.Clear();
            _navigationCts = new CancellationTokenSource();
            var ct = _navigationCts.Token;
            
            if (parameters.TryGetValue("q", out string q))
            {
                SearchText = q;

                try
                {
                    await foreach (var entry in _sourceStorageItemsRepository.SearchAsync(q, ct))
                    {
                        SearchResultItems.Add(ConvertStorageItemViewModel(entry));
                    }
                }
                catch (OperationCanceledException) 
                {
                    SearchResultItems.Clear();
                }
            }
            else
            {
                throw new Exception();
            }

            await base.OnNavigatedToAsync(parameters);
        }

        private StorageItemViewModel ConvertStorageItemViewModel(IStorageItem storageItem)
        {
            var storageItemImageSource = new StorageItemImageSource(storageItem, _folderListingSettings, _thumbnailManager);
            return new StorageItemViewModel(storageItemImageSource, _sourceStorageItemsRepository, _bookmarkManager, _albamRepository);
        }

        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
            _navigationCts?.Cancel();
            _navigationCts?.Dispose();
            _navigationCts = null;

            base.OnNavigatedFrom(parameters);
        }
    }
}
