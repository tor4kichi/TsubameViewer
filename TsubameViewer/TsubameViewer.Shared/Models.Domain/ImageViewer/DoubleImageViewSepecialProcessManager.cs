using LiteDB;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Infrastructure;

namespace TsubameViewer.Models.Domain.ImageViewer
{
    public sealed class DoubleImageViewSepecialProcessManager
    {
        private readonly DoubleImageViewSepecialProcessRepository _doubleImageViewSepecialProcessRepository;

        public DoubleImageViewSepecialProcessManager(DoubleImageViewSepecialProcessRepository doubleImageViewSepecialProcessRepository)
        {
            _doubleImageViewSepecialProcessRepository = doubleImageViewSepecialProcessRepository;
        }

        // 両開き表示で表示している時に縦横比が横長なアイテムを２ページとして扱う
        // 漫画など開始ページがズレている時に、ズレがあるとした前側のページ一つに対して両開き表示と記録することで
        // 常にズレを補正した表示ができるようにする

        public void AddSpecialProcessPage(string path, int page)
        {
            _doubleImageViewSepecialProcessRepository.AddSpecialProcessPage(path, page);
        }

        public void RemoveSpecialProcessPage(string path, int page)
        {
            _doubleImageViewSepecialProcessRepository.RemoveSpecialProcessPage(path, page);
        }

        public HashSet<int> GetSpecialProcessPages(string path)
        {
            return _doubleImageViewSepecialProcessRepository.Get(path);
        }

        public sealed class DoubleImageViewSepecialProcessRepository : LiteDBServiceBase<DoubleImageViewSepecialProcessEntry>
        {
            public DoubleImageViewSepecialProcessRepository(ILiteDatabase liteDatabase) : base(liteDatabase)
            {
            }

            public HashSet<int> Get(string path)
            {
                return _collection.FindById(path)?.ForceDoubleImageViewPageIndexies ?? new HashSet<int>();
            }

            public void AddSpecialProcessPage(string path, int page)
            {
                var entry = _collection.FindById(path);
                if (entry == null)
                {
                    _collection.Insert(new DoubleImageViewSepecialProcessEntry() 
                    {
                        Path = path,
                        ForceDoubleImageViewPageIndexies = new HashSet<int>() { page }
                    });
                }
                else
                {
                    if (!entry.ForceDoubleImageViewPageIndexies.Contains(page))
                    {
                        entry.ForceDoubleImageViewPageIndexies.Add(page);
                        _collection.Update(entry);
                    }
                }
            }

            public void RemoveSpecialProcessPage(string path, int page)
            {
                var entry = _collection.FindById(path);
                if (entry == null) { return; }

                if (entry.ForceDoubleImageViewPageIndexies.Remove(page))
                {
                    _collection.Update(entry);
                }
            }
        }

        public class DoubleImageViewSepecialProcessEntry
        {
            [BsonId]
            public string Path { get; set; }

            [BsonField]
            public HashSet<int> ForceDoubleImageViewPageIndexies { get; set; }
        }

    }
}
