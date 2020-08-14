using Prism.Events;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.Search;
using TsubameViewer.Models.Domain.SourceFolders;
using TsubameViewer.Models.Infrastructure;
using Uno.Threading;
using Windows.Storage;
using Windows.Storage.Search;

namespace TsubameViewer.Models.UseCase
{
    public sealed class PathReferenceCountUpdateWhenSourceManagementChanged : IDisposable
    {
        public class SearchIndexUpdateProgressEventArgs
        {
            public uint TotalCount { get; set; }
            public uint ProcessedCount { get; set; }
        }

        public class SearchIndexUpdateProgressEvent : PubSubEvent<SearchIndexUpdateProgressEventArgs>
        { }


        public class SearchIndexUpdateProcessSettings : FlagsRepositoryBase
        {
            public SearchIndexUpdateProcessSettings()
            {
                _RequireUpdateIndexiesTokens = Read(new string[0], nameof(RequireUpdateIndexiesTokens));
            }



            private string[] _RequireUpdateIndexiesTokens;
            public string[] RequireUpdateIndexiesTokens
            {
                get { return _RequireUpdateIndexiesTokens; }
                set { SetProperty(ref _RequireUpdateIndexiesTokens, value); }
            }

        }

        private readonly IEventAggregator _eventAggregator;
        private readonly StorageItemSearchManager _storageItemSearchManager;
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly PathReferenceCountManager _PathReferenceCountManager;
        private readonly SearchIndexUpdateProcessSettings _settings;
        CompositeDisposable _disposables = new CompositeDisposable();
        SearchIndexUpdateProgressEvent _ProgressEvent;
        public PathReferenceCountUpdateWhenSourceManagementChanged(
            IEventAggregator eventAggregator,
            SearchIndexUpdateProcessSettings settings,
            StorageItemSearchManager storageItemSearchManager,
            SourceStorageItemsRepository sourceStorageItemsRepository,
            PathReferenceCountManager PathReferenceCountManager
            )
        {
            _eventAggregator = eventAggregator;
            _settings = settings;
            _storageItemSearchManager = storageItemSearchManager;
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
            _PathReferenceCountManager = PathReferenceCountManager;
            _ProgressEvent = _eventAggregator.GetEvent<SearchIndexUpdateProgressEvent>();

            _eventAggregator.GetEvent<SourceStorageItemsRepository.AddedEvent>()
                .Subscribe(async args =>
                {
                    if (args.StorageItem is StorageFolder)
                    {
                        RegistrationUpdateIndex(args.Token);
                    }
                    else 
                    {
                        using (await _lock.LockAsync(default))
                        {
                            _storageItemSearchManager.Update(args.StorageItem);
                            _PathReferenceCountManager.Upsert(args.StorageItem.Path, args.Token);
                        }
                    }
                }
                , keepSubscriberReferenceAlive: true
                )
                .AddTo(_disposables);

            _eventAggregator.GetEvent<SourceStorageItemsRepository.RemovedEvent>()
                .Subscribe(async args =>
                {
                    using (await _lock.LockAsync(default))
                    {
                        Debug.WriteLine("[SearchIndexUpdat] Start delete search index : " + args.Token);
                        PathReferenceCountManager.Remove(args.Token);
                        Debug.WriteLine("[SearchIndexUpdat] Complete deletion : " + args.Token);
                    }
                }
                , keepSubscriberReferenceAlive: true
                )
                .AddTo(_disposables);
        }

        public void Initialize()
        {
            RestoreUpdateIndexProcess();
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }

        FastAsyncLock _lock = new FastAsyncLock();


        public void RestoreUpdateIndexProcess()
        {
            _ = TryRunUpdateIndexNextFolder();
        }

        public void RegistrationUpdateIndex(string token)
        {
            _settings.RequireUpdateIndexiesTokens = _settings.RequireUpdateIndexiesTokens.Append(token).ToArray();

            _ = TryRunUpdateIndexNextFolder();
        }

        string _currentlyUpdateToken;
        CancellationTokenSource _ctsForToken = new CancellationTokenSource();

        private async Task TryRunUpdateIndexNextFolder(uint prevProcessCount = 0)
        {
            var totalCount = await GetRequireUpdateIndexiesTokenFoldersItemsCountAsync();
            _ProgressEvent.Publish(new SearchIndexUpdateProgressEventArgs() { TotalCount = totalCount, ProcessedCount = prevProcessCount });

            if (_currentlyUpdateToken != null) { return; }

            var token = _settings.RequireUpdateIndexiesTokens.FirstOrDefault();
            if (token == null) { return; }

            uint processCount = 0;
            try
            {
                _currentlyUpdateToken = token;
                // 更新待ちの間にフォルダが管理から取り除かれる可能性がある
                var storageItem = await _sourceStorageItemsRepository.GetItemAsync(token);
                if (storageItem != null)
                {
                    if (storageItem is StorageFolder folder)
                    {
                        Action<uint> progressUpdate = (cnt) =>
                        {
                            processCount = cnt;
                            _ProgressEvent.Publish(new SearchIndexUpdateProgressEventArgs() { TotalCount = totalCount, ProcessedCount = prevProcessCount + processCount });
                        };

                        await RegistrationFolderAsync(token, folder, progressUpdate, _ctsForToken.Token);
                    }
                    else
                    {
                        using (await _lock.LockAsync(default))
                        { 
                            _storageItemSearchManager.Update(storageItem);
                            processCount = 1;
                            _ProgressEvent.Publish(new SearchIndexUpdateProgressEventArgs() { TotalCount = totalCount, ProcessedCount = prevProcessCount + processCount });
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Unregistrationによるキャンセルの場合は次の更新に
            }
            catch
            {
                throw;
            }
            finally
            {
                _currentlyUpdateToken = null;
            }

            var list = _settings.RequireUpdateIndexiesTokens.ToList();
            list.Remove(token);
            _settings.RequireUpdateIndexiesTokens = list.ToArray();

            if (_settings.RequireUpdateIndexiesTokens.Any())
            {
                _ = TryRunUpdateIndexNextFolder(prevProcessCount + processCount);
            }
        }

        private async Task<uint> GetRequireUpdateIndexiesTokenFoldersItemsCountAsync()
        {
            var tokens = _settings.RequireUpdateIndexiesTokens.ToList();
            uint sum = 0;
            foreach (var token in tokens)
            {
                var storageItem = await _sourceStorageItemsRepository.GetItemAsync(token);
                if (storageItem is StorageFolder folder)
                {
                    var options = MakeQueryOptions();
                    var query = folder.CreateItemQueryWithOptions(options);
                    var count = await query.GetItemCountAsync();
                    sum += count;
                }
                else
                {
                    sum += 1;
                }
            }

            return sum;
        }

        private QueryOptions MakeQueryOptions()
        {
            return new Windows.Storage.Search.QueryOptions(
                    Windows.Storage.Search.CommonFileQuery.DefaultQuery, SupportedFileTypesHelper.SupportedArchiveFileExtensions.Concat(SupportedFileTypesHelper.SupportedEBookFileExtensions))
            {
                FolderDepth = Windows.Storage.Search.FolderDepth.Deep,
                IndexerOption = Windows.Storage.Search.IndexerOption.UseIndexerWhenAvailable,
            };
        }

        private async Task RegistrationFolderAsync(string token, StorageFolder folder, Action<uint> progress = null, CancellationToken ct = default)
        {
            await Task.Run(async () =>
            {
                var options = MakeQueryOptions();

                var query = folder.CreateItemQueryWithOptions(options);
                var count = await query.GetItemCountAsync();

                _storageItemSearchManager.Update(folder);
                _PathReferenceCountManager.Upsert(folder.Path, token);

                uint currentIndex = 0;
                while (currentIndex < count)
                {
                    ct.ThrowIfCancellationRequested();

                    var items = await query.GetItemsAsync(currentIndex, 10);

                    currentIndex += (uint)items.Count;

                    using (await _lock.LockAsync(ct))
                    {
                        foreach (var item in items)
                        {
                            _storageItemSearchManager.Update(item);
                            _PathReferenceCountManager.Upsert(item.Path, token);
                            ct.ThrowIfCancellationRequested();
                        }
                    }

                    progress?.Invoke(currentIndex);
                }
            }, ct);
        }

    }
}
