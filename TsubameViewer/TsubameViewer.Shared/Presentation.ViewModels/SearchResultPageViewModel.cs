﻿using Prism.Mvvm;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.Bookmark;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.ImageViewer.ImageSource;
using TsubameViewer.Models.Domain.Search;
using TsubameViewer.Models.Domain.SourceFolders;
using TsubameViewer.Presentation.Services.UWP;
using TsubameViewer.Presentation.ViewModels.PageNavigation;
using TsubameViewer.Presentation.ViewModels.PageNavigation.Commands;
using TsubameViewer.Presentation.Views;
using Uno.Extensions;
using Windows.Storage;

namespace TsubameViewer.Presentation.ViewModels
{
    public sealed class SearchResultPageViewModel : ViewModelBase
    {
        private readonly StorageItemSearchManager _storageItemSearchManager;
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly PathReferenceCountManager _PathReferenceCountManager;
        private readonly FolderListingSettings _folderListingSettings;
        private readonly BookmarkManager _bookmarkManager;
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
        public OpenImageViewerCommand OpenImageViewerCommand { get; }
        public OpenFolderListupCommand OpenFolderListupCommand { get; }
        public OpenWithExplorerCommand OpenWithExplorerCommand { get; }
        public SecondaryTileAddCommand SecondaryTileAddCommand { get; }
        public SecondaryTileRemoveCommand SecondaryTileRemoveCommand { get; }

        public SearchResultPageViewModel(
            StorageItemSearchManager storageItemSearchManager,
            SourceStorageItemsRepository sourceStorageItemsRepository,
            PathReferenceCountManager PathReferenceCountManager,
            FolderListingSettings folderListingSettings,
            BookmarkManager bookmarkManager,
            ThumbnailManager thumbnailManager,
            SecondaryTileManager secondaryTileManager,

            OpenFolderItemCommand openFolderItemCommand,
            OpenImageViewerCommand openImageViewerCommand,
            OpenFolderListupCommand openFolderListupCommand,
            OpenWithExplorerCommand openWithExplorerCommand,
            SecondaryTileAddCommand secondaryTileAddCommand,
            SecondaryTileRemoveCommand secondaryTileRemoveCommand
            )
        {
            _storageItemSearchManager = storageItemSearchManager;
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
            _PathReferenceCountManager = PathReferenceCountManager;
            _folderListingSettings = folderListingSettings;
            _bookmarkManager = bookmarkManager;
            _thumbnailManager = thumbnailManager;
            SecondaryTileManager = secondaryTileManager;
            OpenFolderItemCommand = openFolderItemCommand;
            OpenImageViewerCommand = openImageViewerCommand;
            OpenFolderListupCommand = openFolderListupCommand;
            OpenWithExplorerCommand = openWithExplorerCommand;
            SecondaryTileAddCommand = secondaryTileAddCommand;
            SecondaryTileRemoveCommand = secondaryTileRemoveCommand;
        }

        CancellationTokenSource _navigationCts;

        public override void OnNavigatingTo(INavigationParameters parameters)
        {
            PrimaryWindowCoreLayout.SetCurrentNavigationParameters(parameters);

            SearchResultItems.Clear();

            base.OnNavigatingTo(parameters);
        }

        public override async Task OnNavigatedToAsync(INavigationParameters parameters)
        {
            _navigationCts = new CancellationTokenSource();
            var ct = _navigationCts.Token;
            
            if (parameters.TryGetValue("q", out string q))
            {
                SearchText = q;

                var result = await Task.Run(() => _storageItemSearchManager.SearchAsync(q.Trim(), 0, 100), ct);
                foreach (var entry in result.Entries)
                {
                    SearchResultItems.Add(await ConvertStorageItemViewModel(entry));
                }

                int totalCount = result.TotalCount;
                while (totalCount > SearchResultItems.Count)
                {
                    result = await Task.Run(() => _storageItemSearchManager.SearchAsync(q.Trim(), SearchResultItems.Count, 100), ct);
                    foreach (var entry in result.Entries)
                    {
                        SearchResultItems.Add(await ConvertStorageItemViewModel(entry));
                    }

                    ct.ThrowIfCancellationRequested();
                }
            }
            else
            {
                throw new Exception();
            }

            await base.OnNavigatedToAsync(parameters);
        }

        private async Task<StorageItemViewModel> ConvertStorageItemViewModel(StorageItemSearchEntry entry)
        {
            var token = _PathReferenceCountManager.GetToken(entry.Path);
            var storageItem = await _sourceStorageItemsRepository.GetStorageItemFromPath(token, entry.Path);
            var storageItemImageSource = new StorageItemImageSource(storageItem, _thumbnailManager);
            return new StorageItemViewModel(storageItemImageSource, null, _sourceStorageItemsRepository, _folderListingSettings, _bookmarkManager);
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
