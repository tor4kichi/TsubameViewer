using System;
using System.Collections.Generic;

namespace TsubameViewer.Core.Models.FolderItemListing;

public interface IRecentlyAccessService
{
    void AddWatched(string path);
    void AddWatched(string path, DateTimeOffset lastAccess);
    void Delete(string path);
    void DeleteAllUnderPath(string path);
    List<(string Path, DateTimeOffset LastAccessTime)> GetItemsSortWithRecently(int take);
}