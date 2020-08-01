using LiteDB;
using System;
using System.Collections.Generic;
using System.Text;

namespace TsubameViewer.Models.Domain.Bookmark
{
    public sealed class BookmarkManager
    {
        // OneDriveを意識するならログインユーザーに対する一意のIDを持たせて置いたほうがいいかもしれない

        public bool IsBookmarked(string path)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class FolderItemBookmarkEntry
    {
        [BsonId]
        public string FolderPath { get; set; }

        [BsonField]
        public string FileName { get; set; }
    }

    public sealed class ArchiveItemBookmarkEntry
    {
        [BsonId]
        public string Path { get; set; }

        [BsonField]
        public string ItemKey { get; set; } 
    }
}
