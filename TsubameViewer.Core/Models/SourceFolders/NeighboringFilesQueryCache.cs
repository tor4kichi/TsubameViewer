using System;
using System.Collections.Generic;
using System.Text;
using Windows.Storage.Search;

namespace TsubameViewer.Core.Models.SourceFolders;

public class NeighboringFilesQueryCache
{
    static Dictionary<string, StorageFileQueryResult> _NeighboringFilesQueryCache = new Dictionary<string, StorageFileQueryResult>();


    public static void AddNeighboringFilesQuery(string path, StorageFileQueryResult query)
    {
        if (_NeighboringFilesQueryCache.ContainsKey(path))
        {
            _NeighboringFilesQueryCache[path] = query;
        }
        else
        {
            _NeighboringFilesQueryCache.Add(path, query);
        }
    }

    public static StorageFileQueryResult GetNeighboringFilesQuery(string path)
    {
        return _NeighboringFilesQueryCache.TryGetValue(path, out var cache) ? cache : null;
    }

    public static void RemoveNeighboringFilesQuery(string path)
    {
        _NeighboringFilesQueryCache.Remove(path);
    }
}
