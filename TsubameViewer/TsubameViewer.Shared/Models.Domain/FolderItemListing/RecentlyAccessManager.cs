using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TsubameViewer.Models.Infrastructure;

namespace TsubameViewer.Models.Domain.FolderItemListing
{
    public sealed class RecentlyAccessManager 
    {
        public const int RecordCount = 100;
        private readonly RecentlyAccessRepository _recentlyAccessRepository;

        public RecentlyAccessManager(RecentlyAccessRepository recentlyAccessRepository)
        {
            _recentlyAccessRepository = recentlyAccessRepository;
        }

        public void AddWatched(string token, string subtractPath)
        {
            _recentlyAccessRepository.Upsert(token, subtractPath);
        }

        public List<RecentlyAccessEntry> GetItemsSortWithRecently(int take)
        {
            return _recentlyAccessRepository.GetItemsSortWithRecently(take);
        }

        public void Delete(RecentlyAccessEntry entry)
        {
            _recentlyAccessRepository.DeleteItem(entry.Id);
        }

        public sealed class RecentlyAccessRepository : LiteDBServiceBase<RecentlyAccessEntry>
        {
            public RecentlyAccessRepository(ILiteDatabase liteDatabase) : base(liteDatabase)
            {
                _collection.EnsureIndex(x => x.Token);
                _collection.EnsureIndex(x => x.SubtractPath);
            }

            public void Upsert(string token, string subtractPath)
            {
                var existItem = _collection.FindOne(x => x.Token == token && x.SubtractPath == subtractPath);
                if (existItem != null)
                {
                    _collection.Delete(existItem.Id);
                }
                else
                {
                    var count = _collection.Count();
                    if (count > RecordCount)
                    {
                        var first = _collection.Query().First();
                        _collection.Delete(first.Id);
                    }
                }

                _collection.Insert(new RecentlyAccessEntry() { Token = token, SubtractPath = subtractPath });
            }

            public List<RecentlyAccessEntry> GetItemsSortWithRecently(int take)
            {
                return _collection.Query().OrderByDescending(x => x.Id).Limit(take).ToList();
            }
        }

        public class RecentlyAccessEntry
        {
            [BsonId(autoId: true)]
            public int Id { get; set; }

            [BsonField]
            public string Token { get; set; }

            [BsonField]
            public string SubtractPath { get; set; }
        }
    }
}
