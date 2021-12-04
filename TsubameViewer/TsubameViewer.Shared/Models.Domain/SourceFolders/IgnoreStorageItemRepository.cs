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
        [BsonId(true)]
        public int _id { get; set; }

        public string Path { get; set; }
    }

    public class IgnoreStorageItemRepository : Infrastructure.LiteDBServiceBase<IgnoreStorageItemEntry>
    {
        public IgnoreStorageItemRepository(ILiteDatabase liteDatabase) : base(liteDatabase)
        {
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
            _collection.Delete(entry._id);
        }
    }



    
}
