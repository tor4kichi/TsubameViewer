using LiteDB;
using Microsoft.Toolkit.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TsubameViewer.Models.Infrastructure;

namespace TsubameViewer.Models.Domain.Albam
{
    public sealed record AlbamEntry
    {
        [BsonId(autoId: true)]
        public Guid _id { get; init; }

        public string Name { get; init; }

        public DateTimeOffset CreatedAt { get; init; }        
    }

    public sealed record AlbamItemEntry
    {
        [BsonId(autoId: true)]
        public Guid _id { get; init; }


        public Guid AlbamId { get; init; }

        public string Path { get; init; }

        public DateTimeOffset AddedAt { get; init; }
    }

    public sealed class AlbamRepository
    {
        

        sealed class AlbamDatabase : LiteDBServiceBase<AlbamEntry>
        {
            public AlbamDatabase(ILiteDatabase liteDatabase) : base(liteDatabase)
            {
                
            }

            internal AlbamEntry FindById(Guid albamId)
            {
                return _collection.FindById(albamId);
            }
        }


        sealed class AlbamItemDatabase : LiteDBServiceBase<AlbamItemEntry>
        {
            public AlbamItemDatabase(ILiteDatabase liteDatabase) : base(liteDatabase)
            {
                _collection.EnsureIndex(x => x.AlbamId);
                _collection.EnsureIndex(x => x.Path);
                _collection.EnsureIndex(x => x.AddedAt);
            }

            public int DeleteAlbam(Guid albamId)
            {
                return _collection.DeleteMany(x => x.AlbamId == albamId);
            }

            public bool Delete(Guid albamId, string path)
            {
                return _collection.DeleteMany(x => x.AlbamId == albamId && x.Path == path) > 0;
            }

            public IEnumerable<AlbamItemEntry> GetAlbamItem(Guid albamId, int skip = 0, int limit = int.MaxValue)
            {
                return _collection.Find(x => x.AlbamId == albamId, skip, limit).OrderByDescending(x => x.AddedAt);
            }

            public int DeleteUnderPath(string path)
            {
                return _collection.DeleteMany(x => x.Path.StartsWith(path));
            }
        }


        private readonly AlbamDatabase _albamDatabase;
        private readonly AlbamItemDatabase _albamItemDatabase;



        public AlbamRepository(ILiteDatabase liteDatabase)
        {
            _albamDatabase = new AlbamDatabase(liteDatabase);
            _albamItemDatabase = new AlbamItemDatabase(liteDatabase);
        }

        public bool IsExistAlbam(Guid albamId)
        {
            return _albamDatabase.Exists(x => x._id == albamId);
        }

        public AlbamEntry CreateAlbam(Guid id, string name)
        {
            var entry = new AlbamEntry { _id = id, Name = name, CreatedAt = DateTimeOffset.Now };
            return _albamDatabase.CreateItem(entry);
        }

        public bool DeleteAlbam(Guid albamId)
        {
            _albamItemDatabase.DeleteAlbam(albamId);
            return _albamDatabase.DeleteItem(albamId);
        }

        public bool DeleteAlbam(AlbamEntry entry)
        {
            return _albamDatabase.DeleteItem(entry._id);
        }

        public void UpdateAlbam(AlbamEntry entry)
        {
            _albamDatabase.UpdateItem(entry);
        }

        public AlbamEntry GetAlbam(Guid albamId)
        {
            return _albamDatabase.FindById(albamId);
        }

        public List<AlbamEntry> GetAlbams()
        {
            return _albamDatabase.ReadAllItems();
        }

        public bool IsExistAlbamItem(Guid albamId)
        {
            return _albamItemDatabase.Exists(x => x.AlbamId == albamId);
        }

        public bool IsExistAlbamItem(Guid albamId, string path)
        {
            return _albamItemDatabase.Exists(x => x.AlbamId == albamId && x.Path == path);
        }

        public AlbamItemEntry AddAlbamItem(Guid albamId, string path)
        {
#if DEBUG
            Guard.IsTrue(_albamDatabase.Exists(x => x._id == albamId), $"Not Exist Id: {albamId}");
#endif
            return _albamItemDatabase.CreateItem(new AlbamItemEntry() { _id = Guid.NewGuid(), AlbamId = albamId, Path = path, AddedAt = DateTimeOffset.Now });
        }

        public bool DeleteAlbamItem(Guid albamId, string path)
        {
            return _albamItemDatabase.Delete(albamId, path);
        }

        public int GetAlbamItemsCount(Guid albamId)
        {
            return _albamItemDatabase.Count(x => x.AlbamId == albamId);
        }

        public IEnumerable<AlbamItemEntry> GetAlbamItems(Guid albamId, int skip = 0, int limit = int.MaxValue)
        {
            return _albamItemDatabase.GetAlbamItem(albamId, skip, limit);
        }

        public int DeleteAlbamItemsUnderPath(string path)
        {
            return _albamItemDatabase.DeleteUnderPath(path);
        }

    }
}
