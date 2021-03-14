using Prism.Events;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Text;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.ReadingFeature;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Domain.RestoreNavigation;
using TsubameViewer.Models.Domain.Search;
using TsubameViewer.Models.Domain.SourceFolders;
using TsubameViewer.Presentation.Services.UWP;

namespace TsubameViewer.Models.UseCase
{
    public sealed class ApplicationDataUpdateWhenPathReferenceCountChanged
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly RecentlyAccessManager _recentlyAccessManager;
        private readonly BookmarkManager _bookmarkManager;
        private readonly StorageItemSearchManager _storageItemSearchManager;
        private readonly FolderContainerTypeManager _folderContainerTypeManager;
        private readonly ThumbnailManager _thumbnailManager;
        private readonly FolderLastIntractItemManager _folderLastIntractItemManager;
        CompositeDisposable _disposables = new CompositeDisposable();

        public ApplicationDataUpdateWhenPathReferenceCountChanged(
            IEventAggregator eventAggregator,
            RecentlyAccessManager recentlyAccessManager,
            BookmarkManager bookmarkManager,
            StorageItemSearchManager storageItemSearchManager,
            FolderContainerTypeManager folderContainerTypeManager,
            ThumbnailManager thumbnailManager,
            SecondaryTileManager secondaryTileManager,
            FolderLastIntractItemManager folderLastIntractItemManager
            )
        {
            _eventAggregator = eventAggregator;
            _recentlyAccessManager = recentlyAccessManager;
            _bookmarkManager = bookmarkManager;
            _storageItemSearchManager = storageItemSearchManager;
            _folderContainerTypeManager = folderContainerTypeManager;
            _thumbnailManager = thumbnailManager;
            _folderLastIntractItemManager = folderLastIntractItemManager;
            _eventAggregator.GetEvent<PathReferenceCountManager.PathReferenceAddedEvent>()
                .Subscribe(args => 
                {
                }
                , keepSubscriberReferenceAlive: true
                )
                .AddTo(_disposables);

            _eventAggregator.GetEvent<PathReferenceCountManager.PathReferenceRemovedEvent>()
                .Subscribe(async args =>
                {
                    _recentlyAccessManager.Delete(args.Path);
                    _bookmarkManager.RemoveBookmark(args.Path);
                    _storageItemSearchManager.Remove(args.Path);
                    _folderContainerTypeManager.Delete(args.Path);
                    _folderLastIntractItemManager.Remove(args.Path);
                    await _thumbnailManager.DeleteFromPath(args.Path);
                    await secondaryTileManager.RemoveSecondaryTile(args.Path);
                }
                , keepSubscriberReferenceAlive: true
                )
                .AddTo(_disposables);
        }
    }
}
