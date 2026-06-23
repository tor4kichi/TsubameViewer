using LiteDB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using TsubameViewer.Core.Helpers;
using TsubameViewer.Core.Infrastructure;

namespace TsubameViewer.Core.Models.FolderItemListing;

public struct NormalizedPagePosition
{
    public float Value { get; set; }

    // Note: デフォルトコンストラクタを定義していないとx86 リリースモード時にエラーが
    public NormalizedPagePosition()
    {
        Value = 0f;
    }

    public NormalizedPagePosition(float normalized)
    {
        Value = Math.Clamp(normalized, 0.0f, 1.0f);
    }

    public NormalizedPagePosition(long pageCount, long currentPagePosition)
    {
        if (pageCount < currentPagePosition) { throw new ArgumentOutOfRangeException("pageCount < currentPagePosition"); }

        Value = Math.Clamp(currentPagePosition / (float)pageCount, 0.0f, 1.0f);
    }
}

public sealed class BookmarkEntry
{
    [BsonId(autoId: true)]
    public int Id { get; set; }

    [BsonField]
    public string Path { get; set; }    

    // Note: 動画のDurationにも使ってます
    [BsonField]
    public string PageName { get; set; }

    [BsonField]
    public int InnerPageIndex { get; set; }

    [BsonField]
    public NormalizedPagePosition Position { get; set; }

    [BsonField]
    public bool IsFinishedReading { get; set; }

    [BsonField]
    public ulong ParentPathHash { get; set; } = 0;
}

public sealed class LocalBookmarkRepository
{
    private readonly ILiteCollection<BookmarkEntry> _bookmarkRepository;

    public LocalBookmarkRepository(ILiteDatabase localDatabase)
    {
        _bookmarkRepository = localDatabase.GetCollection<BookmarkEntry>();
        _bookmarkRepository.EnsureIndex(x => x.Path);
        _bookmarkRepository.EnsureIndex(x => x.IsFinishedReading);
        if (_bookmarkRepository.EnsureIndex(x => x.ParentPathHash))
        {
            foreach (var item in _bookmarkRepository.Query().ForUpdate().ToEnumerable())
            {
                item.ParentPathHash = HashHelper.CalculateFNV1a64(Path.GetDirectoryName(item.Path));
                _bookmarkRepository.Update(item);
            }
        }
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


    public void AddBookmarkForImageViewer(string path, string pageName, NormalizedPagePosition normalizedPosition, bool isFinished)
    {
        _bookmarkRepository.AddorReplace(path, pageName, normalizedPosition, isFinished: isFinished);
    }

    public void AddBookmarkForEBookViewer(string path, string pageName, int innerPageIndex, NormalizedPagePosition normalizedPosition, bool isFinished)
    {
        _bookmarkRepository.AddorReplace(path, pageName, normalizedPosition, innerPageIndex, isFinished);
    }

    public void RemoveBookmark(string path)
    {
        _bookmarkRepository.Remove(path);
    }


    public void RemoveAllBookmarkUnderPath(string path)
    {
        _bookmarkRepository.RemoveAllUnderPath(path);
    }

    public void PathChanged(string oldPath, string newPath)
    {
        _bookmarkRepository.PathChanged(oldPath, newPath);
    }

    public BookmarkFacade GetBookmarkFacade(string path)
    {
        var entry = _bookmarkRepository.GetEnsureEntryByPath(path);
        return new BookmarkFacade(_bookmarkRepository, entry);
    }

    public (int finishedItemsCount, int totalItemsCount) GetItemsCountForFolder(string path)
    {
        ulong hash = HashHelper.CalculateFNV1a64(path);
        int sepCount = path.Count(c => c == System.IO.Path.DirectorySeparatorChar || c == System.IO.Path.AltDirectorySeparatorChar) + 1;
        var itemsCount = _bookmarkRepository.Count(x => x.ParentPathHash == hash);
        var finishedCount = _bookmarkRepository.Count(x => x.IsFinishedReading && x.ParentPathHash == hash);
        return (finishedCount, itemsCount);
    }
}


file static class BookmarkCollectionExtensions
{
    extension (ILiteCollection<BookmarkEntry> _collection)
    {
        
        public string GetBookmarkPageName(string path)
        {
            var bookmark = _collection.FindOne(x => x.Path.Equals(path, StringComparison.Ordinal));
            return bookmark?.PageName;
        }

        public (string pageName, int pageInnerIndex) GetBookmarkPageNameAndIndex(string path)
        {
            var bookmark = _collection.FindOne(x => x.Path.Equals(path, StringComparison.Ordinal));
            if (bookmark == null) { return default; }
            return (bookmark.PageName, bookmark.InnerPageIndex);
        }

        public float GetBookmarkLastReadPositionInNormalized(string path)
        {
            var bookmark = _collection.FindOne(x => x.Path.Equals(path, StringComparison.Ordinal));
            // Note: bookmark?.Position.Value ?? 0f; と書くと
            //       x86 のリリースモードで System.InvalidCastException が発生する
            if (bookmark == null)
            {
                return 0f;
            }
            else
            {
                return !bookmark.IsFinishedReading ? bookmark.Position.Value : 1f;
            }
        }

        public bool IsBookmarked(string path)
        {
            var bookmark = _collection.FindOne(x => x.Path.Equals(path, StringComparison.Ordinal));
            return bookmark != null;
        }

        public bool IsBookmarked(string path, out string bookmarkPageName)
        {
            var bookmark = _collection.FindOne(x => x.Path.Equals(path, StringComparison.Ordinal));
            bookmarkPageName = bookmark?.PageName;
            return bookmark != null;
        }

        public void AddorReplace(string path, string bookmarkPageName, NormalizedPagePosition normalizedPosition, int innerPageIndex = 0, bool isFinished = false)
        {
            var bookmark = _collection.FindOne(x => x.Path.Equals(path, StringComparison.Ordinal));
            if (bookmark == null)
            {
                _collection.Insert(new BookmarkEntry()
                {
                    Path = path,
                    PageName = bookmarkPageName,
                    InnerPageIndex = innerPageIndex,
                    Position = normalizedPosition,
                    IsFinishedReading = isFinished,
                    ParentPathHash = HashHelper.CalculateFNV1a64(System.IO.Path.GetDirectoryName(path))
                });
            }
            else
            {
                bookmark.PageName = bookmarkPageName;
                bookmark.InnerPageIndex = innerPageIndex;
                bookmark.Position = normalizedPosition;
                if (!bookmark.IsFinishedReading && isFinished)
                {
                    Debug.WriteLine($"Mark as Finished: {normalizedPosition.Value:F2}");
                    bookmark.IsFinishedReading = isFinished;
                }
                _collection.Update(bookmark);
            }
        }

        public void Remove(string path)
        {
            _collection.DeleteMany(x => x.Path.Equals(path, StringComparison.Ordinal));
        }

        public void RemoveAllUnderPath(string path)
        {
            _collection.DeleteMany(x => path.StartsWith(x.Path, StringComparison.Ordinal));
        }

        public void PathChanged(string oldPath, string newPath)
        {
            StringBuilder sb = new();
            var bookmarkEntries = _collection.Find(x => x.Path.StartsWith(oldPath, StringComparison.Ordinal)).ToList();
            foreach (var entry in bookmarkEntries)
            {
                var prevPath = entry.Path;
                sb.Clear();
                sb.Append(entry.Path);
                sb.Replace(oldPath, newPath);
                entry.Path = sb.ToString();
                entry.ParentPathHash = HashHelper.CalculateFNV1a64(System.IO.Path.GetDirectoryName(entry.Path));
                _collection.Update(entry);
                Debug.WriteLine($"Bookmark path {prevPath} ===> {entry.Path}");
            }
        }

        internal BookmarkEntry GetEnsureEntryByPath(string path)
        {
            var entry = _collection.FindOne(x => x.Path.Equals(path, StringComparison.Ordinal));
            if (entry == null)
            {
                entry = new BookmarkEntry() { Path = path };
                var id = _collection.Insert(entry);
                entry.Id = id;
            }

            return entry;
        }
    }
}


public sealed class BookmarkFacade : DeferSaveAwareObservableObject
{
    private readonly ILiteCollection<BookmarkEntry> _repo;
    private readonly BookmarkEntry _entry;

    public BookmarkFacade(ILiteCollection<BookmarkEntry> repo, BookmarkEntry entry)
    {
        _repo = repo;
        _entry = entry;
    }

    protected override void OnSave()
    {
        _repo.Update(_entry);
    }


    public NormalizedPagePosition ReadPosition
    {
        get => _entry.Position;
        set => SetProperty(_entry.Position, value, _entry, (m, v) => m.Position = v);
    }

    public void SetReadPosition(long currentValue, long totalValue)
    {
        ReadPosition = new NormalizedPagePosition(totalValue, currentValue);
    }

    // Note: 動画のDurationにも使ってます
    public string PageName
    {
        get => _entry.PageName;
        set => SetProperty(_entry.PageName, value, _entry, (m, v) => m.PageName = v);
    }

    public int InnerPageIndex
    {
        get => _entry.InnerPageIndex;
        set => SetProperty(_entry.InnerPageIndex, value, _entry, (m, v) => m.InnerPageIndex = v);
    }

    public bool IsFinishedReading 
    {
        get => _entry.IsFinishedReading;
        set => SetProperty(_entry.IsFinishedReading, value, _entry, (m, v) => m.IsFinishedReading = v);
    }
}
