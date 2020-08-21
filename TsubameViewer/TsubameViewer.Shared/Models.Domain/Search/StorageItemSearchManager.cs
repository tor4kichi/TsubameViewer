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
        

        

        private readonly StorageItemSearchRepository _storageItemSearchRepository;
        private readonly IEventAggregator _eventAggregator;
        
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;
        private readonly PathReferenceCountManager _PathReferenceCountManager;
        

        public StorageItemSearchManager(
            StorageItemSearchRepository storageItemSearchRepository,
            IEventAggregator eventAggregator,
            
            SourceFolders.SourceStorageItemsRepository sourceStorageItemsRepository,
            PathReferenceCountManager PathReferenceCountManager
            )
        {
            _storageItemSearchRepository = storageItemSearchRepository;
            _eventAggregator = eventAggregator;
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
            _PathReferenceCountManager = PathReferenceCountManager;
            
        }

        FastAsyncLock _lock = new FastAsyncLock();

        public void Update(IStorageItem storageItem)
        {
            _storageItemSearchRepository.UpsertSearchIndex(storageItem.Path, storageItem.Name);
        }

        public void Remove(string path)
        {
            _storageItemSearchRepository.Delete(path);
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
            }


            public void UpsertSearchIndex(string path, string title)
            {
                var entry = _collection.FindById(path);
                if (entry != null)
                {
                    entry.Title = title;
                    _collection.Update(entry);
                }
                else
                {
                    _collection.Insert(new StorageItemSearchEntry() { Path = path, Title = title });
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
                    queryEnum = queryEnum.Where(x => x.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) || x.Tags.Any(y => y.Contains(query, StringComparison.CurrentCultureIgnoreCase)));
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


            public void Delete(string path)
            {
                _collection.Delete(path);
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
        public string Title { get; set; }

        [BsonField]
        public List<string> Tags { get; set; } = new List<string>();
    }
}
