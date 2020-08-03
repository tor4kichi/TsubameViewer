using LiteDB;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Infrastructure;

namespace TsubameViewer.Models.Domain.Bookmark
{
    public sealed class BookmarkManager
    {
        private readonly BookmarkRepository _bookmarkRepository;

        public BookmarkManager(BookmarkRepository bookmarkRepository)
        {
            _bookmarkRepository = bookmarkRepository;
        }

        // OneDriveを意識するならログインユーザーに対する一意のIDを持たせて置いたほうがいいかもしれない

        public bool IsBookmarked(string path)
        {
            return _bookmarkRepository.IsBookmarked(path);
        }

        public string GetBookmarkedPageName(string path)
        {
            return _bookmarkRepository.GetBookmarkPageName(path);
        }

        public (string pageName, int innerPageIndex) GetBookmarkedPageNameAndIndex(string path)
        {
            return _bookmarkRepository.GetBookmarkPageNameAndIndex(path);
        }


        public void AddBookmark(string path, string pageName)
        {
            _bookmarkRepository.AddorReplace(path, pageName);
        }

        public void AddBookmark(string path, string pageName, int innerPageIndex)
        {
            _bookmarkRepository.AddorReplace(path, pageName, innerPageIndex);
        }

        public void RemoveBookmark(string path)
        {
            _bookmarkRepository.Remove(path);
        }


        public sealed class BookmarkRepository : LiteDBServiceBase<BookmarkEntry>
        {
            public BookmarkRepository(ILiteDatabase liteDatabase) : base(liteDatabase)
            {
                _collection.EnsureIndex(x => x.Path);
            }

            public string GetBookmarkPageName(string path)
            {
                var bookmark = _collection.FindOne(x => x.Path == path);
                return bookmark?.PageName;
            }

            public (string pageName, int pageInnerIndex) GetBookmarkPageNameAndIndex(string path)
            {
                var bookmark = _collection.FindOne(x => x.Path == path);
                if (bookmark == null) { return default; }
                return (bookmark.PageName, bookmark.InnerPageIndex);
            }

            public bool IsBookmarked(string path)
            {
                var bookmark = _collection.FindOne(x => x.Path == path);
                return bookmark != null;
            }

            public bool IsBookmarked(string path, out string bookmarkPageName)
            {
                var bookmark = _collection.FindOne(x => x.Path == path);
                bookmarkPageName = bookmark?.PageName;
                return bookmark != null;
            }

            public void AddorReplace(string path, string bookmarkPageName, int innerPageIndex = 0)
            {
                var bookmark = _collection.FindOne(x => x.Path == path);
                if (bookmark == null)
                {
                    _collection.Insert(new BookmarkEntry() { Path = path, PageName = bookmarkPageName, InnerPageIndex = innerPageIndex });
                }
                else
                {
                    bookmark.PageName = bookmarkPageName;
                    bookmark.InnerPageIndex = innerPageIndex;
                    _collection.Update(bookmark);
                }
            }

            public void Remove(string path)
            {
                _collection.DeleteMany(x => x.Path == path);
            }
        }

    }

    public sealed class BookmarkEntry
    {
        [BsonId(autoId: true)]
        public int Id { get; set; }

        [BsonField]
        public string Path { get; set; }

        [BsonField]
        public string PageName { get; set; }

        [BsonField]
        public int InnerPageIndex { get; set; } 
    }
}
