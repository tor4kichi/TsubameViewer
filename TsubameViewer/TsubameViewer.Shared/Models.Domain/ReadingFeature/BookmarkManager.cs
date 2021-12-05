using LiteDB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using TsubameViewer.Models.Infrastructure;

namespace TsubameViewer.Models.Domain.ReadingFeature
{
    public struct NormalizedPagePosition
    {
        public float Value { get; set; }

        public NormalizedPagePosition(float normalized)
        {
            Value = Math.Clamp(normalized, 0.0f, 1.0f);
        }

        public NormalizedPagePosition(int pageCount, int currentPagePosition)
        {
            if (pageCount < currentPagePosition) { throw new ArgumentOutOfRangeException("pageCount < currentPagePosition"); }

            Value = Math.Clamp(currentPagePosition / (float)pageCount, 0.0f, 1.0f);
        }
    }
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

        public float GetBookmarkLastReadPositionInNormalized(string path)
        {
            return _bookmarkRepository.GetBookmarkLastReadPositionInNormalized(path);
        }


        public void AddBookmark(string path, string pageName, NormalizedPagePosition normalizedPosition)
        {
            _bookmarkRepository.AddorReplace(path, pageName, normalizedPosition);
        }

        public void AddBookmark(string path, string pageName, int innerPageIndex, NormalizedPagePosition normalizedPosition)
        {
            _bookmarkRepository.AddorReplace(path, pageName, normalizedPosition, innerPageIndex);
        }

        public void RemoveBookmark(string path)
        {
            _bookmarkRepository.Remove(path);
        }

        public void FolderChanged(string oldPath, string newPath)
        {
            _bookmarkRepository.FolderChanged(oldPath, newPath);
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

            public float GetBookmarkLastReadPositionInNormalized(string path)
            {
                var bookmark = _collection.FindOne(x => x.Path == path);
                return bookmark?.Position.Value ?? 0.0f;
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

            public void AddorReplace(string path, string bookmarkPageName, NormalizedPagePosition normalizedPosition, int innerPageIndex = 0)
            {
                var bookmark = _collection.FindOne(x => x.Path == path);
                if (bookmark == null)
                {
                    _collection.Insert(new BookmarkEntry() { Path = path, PageName = bookmarkPageName, InnerPageIndex = innerPageIndex, Position = normalizedPosition });
                }
                else
                {
                    bookmark.PageName = bookmarkPageName;
                    bookmark.InnerPageIndex = innerPageIndex;
                    bookmark.Position = normalizedPosition;
                    _collection.Update(bookmark);
                }
            }

            public void Remove(string path)
            {
                _collection.DeleteMany(x => x.Path == path);
            }

            public void FolderChanged(string oldPath, string newPath)
            {
                var bookmarkEntries =_collection.Find(x => x.Path.StartsWith(oldPath)).ToList();
                foreach (var entry in bookmarkEntries)
                {
                    var prevPath = entry.Path;
                    entry.Path = entry.Path.Replace(oldPath, newPath);
                    _collection.Update(entry);
                    Debug.WriteLine($"Bookmark path {prevPath} ===> {entry.Path}");
                }
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

        [BsonField]
        public NormalizedPagePosition Position { get; set; }
    }
}
