using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Contracts.Services;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.FolderItemListing;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using TsubameViewer.Core.Models.SourceFolders;
using TsubameViewer.Navigations;
using TsubameViewer.ViewModels.PageNavigation;
using TsubameViewer.ViewModels.PageNavigation.Commands;
using Windows.Storage;
using Windows.UI.Xaml.Navigation;

namespace TsubameViewer.ViewModels
{
    public sealed class SearchResultPageViewModel : NavigationAwareViewModelBase
    {
        private readonly IMessenger _messenger;
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly FolderListingSettings _folderListingSettings;
        private readonly IBookmarkService _bookmarkManager;
        private readonly AlbamRepository _albamRepository;
        private readonly ThumbnailManager _thumbnailManager;

        public ObservableCollection<StorageItemViewModel> SearchResultItems { get; } = new ObservableCollection<StorageItemViewModel>();

        private string _SearchText;
        public string SearchText
        {
            get { return _SearchText; }
            set { SetProperty(ref _SearchText, value); }
        }

        public ISecondaryTileManager SecondaryTileManager { get; }
        public OpenFolderItemCommand OpenFolderItemCommand { get; }
        public OpenFolderItemSecondaryCommand OpenFolderItemSecondaryCommand { get; }
        public OpenImageViewerCommand OpenImageViewerCommand { get; }
        public OpenImageListupCommand OpenImageListupCommand { get; }
        public OpenFolderListupCommand OpenFolderListupCommand { get; }
        public OpenWithExplorerCommand OpenWithExplorerCommand { get; }
        public SecondaryTileAddCommand SecondaryTileAddCommand { get; }
        public SecondaryTileRemoveCommand SecondaryTileRemoveCommand { get; }

        public SearchResultPageViewModel(
            IMessenger messenger,
            SourceStorageItemsRepository sourceStorageItemsRepository,
            FolderListingSettings folderListingSettings,
            IBookmarkService bookmarkManager,
            AlbamRepository albamRepository,
            ThumbnailManager thumbnailManager,
            ISecondaryTileManager secondaryTileManager,

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
            _messenger = messenger;
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
            var mode = parameters.GetNavigationMode();
            if (mode == NavigationMode.Refresh)
            {
                return;
            }

            SearchResultItems.Clear();
            _navigationCts = new CancellationTokenSource();
            var ct = _navigationCts.Token;
            
            if (parameters.TryGetValue("q", out string q))
            {
                SearchText = q;

                try
                {
                    await foreach (var entry in _sourceStorageItemsRepository.SearchAsync(q, ct).WithCancellation(ct))
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
            return new StorageItemViewModel(storageItemImageSource, _messenger, _sourceStorageItemsRepository, _bookmarkManager, _albamRepository);
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
