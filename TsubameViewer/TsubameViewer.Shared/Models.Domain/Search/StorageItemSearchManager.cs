using LiteDB;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain.SourceFolders;
using TsubameViewer.Models.Infrastructure;
using Uno.Threading;
using Windows.Storage;
using Windows.Storage.Search;

namespace TsubameViewer.Models.Domain.Search
{
    public sealed class StorageItemSearchManager
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

        private readonly StorageItemSearchRepository _storageItemSearchRepository;
        private readonly IEventAggregator _eventAggregator;
        private readonly SearchIndexUpdateProcessSettings _settings;
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        SearchIndexUpdateProgressEvent _ProgressEvent;

        public StorageItemSearchManager(
            StorageItemSearchRepository storageItemSearchRepository,
            IEventAggregator eventAggregator,
            SearchIndexUpdateProcessSettings settings,
            SourceFolders.SourceStorageItemsRepository sourceStorageItemsRepository
            )
        {
            _storageItemSearchRepository = storageItemSearchRepository;
            _eventAggregator = eventAggregator;
            _settings = settings;
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
            _ProgressEvent = _eventAggregator.GetEvent<SearchIndexUpdateProgressEvent>();
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
                        await UpdateStorageItemAsync(token, storageItem as StorageFile, _ctsForToken.Token);
                        processCount = 1;
                        _ProgressEvent.Publish(new SearchIndexUpdateProgressEventArgs() { TotalCount = totalCount, ProcessedCount = prevProcessCount + processCount });
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
                            _storageItemSearchRepository.UpsertSearchIndex(token, item.Path, item.Name);
                            ct.ThrowIfCancellationRequested();
                        }
                    }

                    progress?.Invoke(currentIndex);
                }
            }, ct);
        }

        public async Task UpdateStorageItemAsync(string token, IStorageItem storageItem, CancellationToken ct = default)
        {
            using (await _lock.LockAsync(ct))
            {
                _storageItemSearchRepository.UpsertSearchIndex(token, storageItem.Path, storageItem.Name);
            }
        }

        public async Task UnregistrationAsync(string token, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(token))
            {
                throw new ArgumentNullException(nameof(token));
            }
            
            using (await _lock.LockAsync(ct))
            {
                if (_currentlyUpdateToken == token)
                {
                    var cts = _ctsForToken;
                    _ctsForToken = new CancellationTokenSource();
                    cts.Cancel();
                    cts.Dispose();
                }

                await Task.Run(() => _storageItemSearchRepository.RemoveWithReferenceToken(token));
            }
        }

        public async Task<StorageItemSearchResult> SearchAsync(string text, int offset = 0, int count = 50, CancellationToken ct = default)
        {
            using (await _lock.LockAsync(ct))
            {
                var searchResultCount = _storageItemSearchRepository.GetSearchResultCount(text);
                if (searchResultCount == 0)
                {
                    return new StorageItemSearchResult()
                    {
                        TotalCount = 0,
                        Text = text,
                        Entries = Enumerable.Empty<StorageItemSearchEntry>()
                    };
                }

                var result = _storageItemSearchRepository.GetSearchResult(text, offset, count);

                return new StorageItemSearchResult()
                {
                    Text = text,
                    TotalCount = searchResultCount,
                    Entries = result
                };
            }
        }


        public List<string> GetTagsForPath(string path)
        {
            return _storageItemSearchRepository.FindFromPathIncludeTag(path).Tags;
        }

        public void SetTagsForPath(string path, IEnumerable<string> tags)
        {
            _storageItemSearchRepository.UpdateTagsForPath(path, tags);
        }

        public class StorageItemSearchRepository : LiteDBServiceBase<StorageItemSearchEntry>
        {
            public StorageItemSearchRepository(ILiteDatabase liteDatabase) : base(liteDatabase)
            {
                _collection.EnsureIndex(x => x.Title);
                _collection.EnsureIndex(x => x.Tags);
                _collection.EnsureIndex(x => x.ReferenceTokens);
            }


            public void UpsertSearchIndex(string token, string path, string title)
            {
                var entry = _collection.FindById(path);
                if (entry != null)
                {
                    entry.Title = title;
                    if (!entry.ReferenceTokens.Contains(token))
                    {
                        entry.ReferenceTokens.Add(token);
                    }
                    _collection.Update(entry);
                }
                else
                {
                    _collection.Insert(new StorageItemSearchEntry() { Path = path, Title = title, ReferenceTokens = { token } });
                }
            }

            public int GetSearchResultCount(string q)
            {
                return MakeQuery(q).Count();
            }

            public ILiteQueryable<StorageItemSearchEntry>  MakeQuery(string q)
            {
                var querys = q.Split(' ', '　');
                var queryEnum = _collection.Query();
                foreach (var query in querys)
                {
                    queryEnum = queryEnum.Where(x => x.Title.Contains(query) || x.Tags.Any(y => y.Contains(query)));
                }

                return queryEnum;
            }

            public List<StorageItemSearchEntry> GetSearchResult(string q, int offset, int count)
            {
                return MakeQuery(q).Offset(offset).Limit(count).ToList();
            }

            public StorageItemSearchEntry FindFromPathIncludeTag(string path)
            {
                return _collection.Include(x => x.Tags).FindById(path);
            }

            public void UpdateTagsForPath(string path, IEnumerable<string> tags)
            {
                var entry = _collection.FindById(path);
                if (entry == null) { throw new ArgumentException("path is not exist entry. : " + path); }

                entry.Tags = tags.ToList();
                _collection.Update(entry);
            }


            public void RemoveWithReferenceToken(string token)
            {
                var items = _collection.Find(x => x.ReferenceTokens.Contains(token)).ToList();
                foreach (var item in items)
                {
                    item.ReferenceTokens.Remove(token);
                    if (item.ReferenceTokens.Count == 0)
                    {
                        _collection.Delete(item.Path);
                    }
                    else
                    {
                        _collection.Update(item);
                    }
                }
            }
        }
    }

    public class StorageItemSearchResult
    {
        public string Text { get; set; }
        public int TotalCount { get; set; }
        public IEnumerable<StorageItemSearchEntry> Entries { get; set; }
    }

    public class StorageItemSearchEntry
    {
        [BsonId]
        public string Path { get; set; }

        [BsonField]
        public List<string> ReferenceTokens { get; set; } = new List<string>();

        [BsonField]
        public string Title { get; set; }

        [BsonField]
        public List<string> Tags { get; set; } = new List<string>();
    }
}
