using System;

namespace TsubameViewer.Core.Contracts.Services;

public interface IBookmarkService
{
    void AddBookmark(string path, string pageName, int innerPageIndex, NormalizedPagePosition normalizedPosition);
    void AddBookmark(string path, string pageName, NormalizedPagePosition normalizedPosition);
    void FolderChanged(string oldPath, string newPath);
    string GetBookmarkedPageName(string path);
    (string pageName, int innerPageIndex) GetBookmarkedPageNameAndIndex(string path);
    float GetBookmarkLastReadPositionInNormalized(string path);
    bool IsBookmarked(string path);
    void RemoveAllBookmarkUnderPath(string path);
    void RemoveBookmark(string path);
}

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

    public NormalizedPagePosition(int pageCount, int currentPagePosition)
    {
        if (pageCount < currentPagePosition) { throw new ArgumentOutOfRangeException("pageCount < currentPagePosition"); }

        Value = Math.Clamp(currentPagePosition / (float)pageCount, 0.0f, 1.0f);
    }
}