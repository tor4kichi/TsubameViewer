using LiteDB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TsubameViewer.Models.Infrastructure;

namespace TsubameViewer.Models.Domain.RestoreNavigation
{
    public sealed class FolderLastIntractItemManager 
    {
        private readonly FolderLastIntractItemRepository _folderLastIntractItemRepository;

        public FolderLastIntractItemManager(FolderLastIntractItemRepository folderLastIntractItemRepository)
        {
            _folderLastIntractItemRepository = folderLastIntractItemRepository;
        }

        public string GetLastIntractItemName(string path)
        {
            return _folderLastIntractItemRepository.GetLastIntractItemName(path);
        }

        public void SetLastIntractItemName(string path, string itemName)
        {
            if (Path.IsPathRooted(itemName))
            {
                itemName = Path.GetFileName(itemName);
            }
            _folderLastIntractItemRepository.SetLastIntractItemName(path, itemName);
        }

        public void Remove(string path)
        {
            _folderLastIntractItemRepository.DeleteItem(path);
        }

        public void RemoveAllUnderPath(string path)
        {
            _folderLastIntractItemRepository.DeleteAllUnderPath(path);
        }

        public class FolderLastIntractItemRepository : LiteDBServiceBase<FolderLastIntractItem>
        {
            public FolderLastIntractItemRepository(ILiteDatabase liteDatabase) : base(liteDatabase)
            {
            }

            public string GetLastIntractItemName(string path)
            {
                return _collection.FindById(path)?.ItemName;
            }

            public void SetLastIntractItemName(string path, string itemName)
            {
                _collection.Upsert(new FolderLastIntractItem() { Path = path, ItemName = itemName });
            }

            internal void DeleteAllUnderPath(string path)
            {
                _collection.DeleteMany(x => path.StartsWith(x.Path));
            }
        }
    }

    public sealed class FolderLastIntractItem
    {
        [BsonId]
        public string Path { get; set; }

        [BsonField]
        public string ItemName { get; set; }
    }

}
