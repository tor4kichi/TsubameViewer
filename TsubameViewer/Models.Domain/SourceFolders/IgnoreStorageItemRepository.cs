using LiteDB;
using Microsoft.Toolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TsubameViewer.Models.Domain.SourceFolders
{
    public class IgnoreStorageItemEntry
    {
        [BsonId]
        public string Path { get; set; }
    }

    public class IgnoreStorageItemRepository : Infrastructure.LiteDBServiceBase<IgnoreStorageItemEntry>
    {
        public IgnoreStorageItemRepository(ILiteDatabase liteDatabase) : base(liteDatabase)
        {
            _collection.EnsureIndex(x => x.Path);
        }

        public bool IsIgnoredPath(string path)
        {
            return _collection.Exists(x => path.StartsWith(x.Path));
        }

        public bool IsIgnoredPathExact(string path)
        {
            return _collection.Exists(x => path == x.Path);
        }

        public bool TryPeek(out IgnoreStorageItemEntry outEntry)
        {
            if (_collection.Count() == 0)
            {
                outEntry = null;
                return false;
            }
            else
            {
                outEntry = _collection.FindAll().First();
                return true;
            }
        }        

        public bool Any()
        {
            return _collection.Count() != 0;
        }

        public void Delete(IgnoreStorageItemEntry entry)
        {
            _collection.Delete(entry.Path);
        }
    }



    
}
