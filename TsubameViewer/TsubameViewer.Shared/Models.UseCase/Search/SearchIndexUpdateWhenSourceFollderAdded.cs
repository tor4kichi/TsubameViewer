using Prism.Events;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Text;
using TsubameViewer.Models.Domain.Search;
using TsubameViewer.Models.Domain.SourceFolders;
using Windows.Storage;

namespace TsubameViewer.Models.UseCase.Search
{
    public sealed class SearchIndexUpdateWhenSourceFollderAdded : IDisposable
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly StorageItemSearchManager _storageItemSearchManager;

        CompositeDisposable _disposables = new CompositeDisposable();

        public SearchIndexUpdateWhenSourceFollderAdded(
            IEventAggregator eventAggregator,
            StorageItemSearchManager storageItemSearchManager
            )
        {
            _eventAggregator = eventAggregator;
            _storageItemSearchManager = storageItemSearchManager;
            _eventAggregator.GetEvent<SourceStorageItemsRepository.AddedEvent>()
                .Subscribe(args =>
                {
                    _storageItemSearchManager.RegistrationUpdateIndex(args.Token);
                }
                , keepSubscriberReferenceAlive: true
                )
                .AddTo(_disposables);

            _eventAggregator.GetEvent<SourceStorageItemsRepository.RemovedEvent>()
                .Subscribe(async args =>
                {
                    Debug.WriteLine("[SearchIndexUpdat] Start delete search index : " + args.Token);
                    await _storageItemSearchManager.UnregistrationAsync(args.Token);
                    Debug.WriteLine("[SearchIndexUpdat] Complete deletion : " + args.Token);
                }
                , keepSubscriberReferenceAlive: true
                )
                .AddTo(_disposables);
        }

        public void Initialize()
        {
            _storageItemSearchManager.RestoreUpdateIndexProcess();
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }
    }
}
