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

        public void SetSpecialProcessPage(string path, HashSet<int> pages)
        {
            _doubleImageViewSepecialProcessRepository.SetSpecialProcessPage(path, pages);
        }

        public void SetUserInputSpecialProcessPage(string path, HashSet<int> pages)
        {
            _doubleImageViewSepecialProcessRepository.SetUserInputSpecialProcessPage(path, pages);
        }

        public HashSet<int> GetSpecialProcessPages(string path)
        {
            return _doubleImageViewSepecialProcessRepository.Get(path);
        }

        public HashSet<int> GetUserINputSpecialProcessPages(string path)
        {
            return _doubleImageViewSepecialProcessRepository.GetFromUserInput(path);
        }

        public sealed class DoubleImageViewSepecialProcessRepository : LiteDBServiceBase<DoubleImageViewSepecialProcessEntry>
        {
            public DoubleImageViewSepecialProcessRepository(ILiteDatabase liteDatabase) : base(liteDatabase)
            {
            }

            public HashSet<int> Get(string path)
            {
                return _collection.FindById(path)?.DoubleImageViewPageIndexies ?? new HashSet<int>();
            }


            public HashSet<int> GetFromUserInput(string path)
            {
                return _collection.FindById(path)?.UserInputDoubleImageViewPageIndexies ?? new HashSet<int>();
            }

            public void SetSpecialProcessPage(string path, HashSet<int> pages)
            {
                var entry = _collection.FindById(path);
                if (entry == null)
                {
                    _collection.Insert(new DoubleImageViewSepecialProcessEntry() 
                    {
                        Path = path,
                        UserInputDoubleImageViewPageIndexies = new HashSet<int>() ,
                        DoubleImageViewPageIndexies = pages
                    });
                }
                else
                {
                    entry.DoubleImageViewPageIndexies = pages;
                    _collection.Update(entry);
                }
            }

            public void SetUserInputSpecialProcessPage(string path, HashSet<int> pages)
            {
                var entry = _collection.FindById(path);
                if (entry == null)
                {
                    _collection.Insert(new DoubleImageViewSepecialProcessEntry()
                    {
                        Path = path,
                        UserInputDoubleImageViewPageIndexies = pages,
                        DoubleImageViewPageIndexies = new HashSet<int>()
                    });
                }
                else
                {
                    entry.UserInputDoubleImageViewPageIndexies = pages;
                    _collection.Update(entry);
                }
            }
        }

        public class DoubleImageViewSepecialProcessEntry
        {
            [BsonId]
            public string Path { get; set; }

            [BsonField]
            public HashSet<int> DoubleImageViewPageIndexies { get; set; }

            [BsonField]
            public HashSet<int> UserInputDoubleImageViewPageIndexies { get; set; }

        }

    }
}
