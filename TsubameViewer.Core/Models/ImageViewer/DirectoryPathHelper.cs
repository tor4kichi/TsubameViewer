using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SharpCompress.Archives;

namespace TsubameViewer.Core.Models.ImageViewer;


public static class DirectoryPathHelper
{
    public static bool IsSameDirectoryPath(string pathA, string pathB)
    {
        if (pathA == pathB) { return true; }

        bool pathAEmpty = string.IsNullOrEmpty(pathA);
        bool pathBEmpty = string.IsNullOrEmpty(pathB);
        if (pathAEmpty && GetDirectoryDepth(pathB) == 0) { return true; }
        else if (pathBEmpty && GetDirectoryDepth(pathA) == 0) { return true; }
        else if (pathAEmpty && pathBEmpty) { return true; }
        else if (pathAEmpty ^ pathBEmpty) { return false; }

        var pathASequence = pathA.Where(c => c != Path.DirectorySeparatorChar && c != Path.AltDirectorySeparatorChar);
        var pathBSequence = pathB.Where(c => c != Path.DirectorySeparatorChar && c != Path.AltDirectorySeparatorChar);
        return Enumerable.SequenceEqual(pathASequence, pathBSequence);

        /*
        bool isSkipALastChar = pathA.EndsWith(Path.DirectorySeparatorChar) || pathA.EndsWith(Path.AltDirectorySeparatorChar);
        bool isSkipBLastChar = pathB.EndsWith(Path.DirectorySeparatorChar) || pathB.EndsWith(Path.AltDirectorySeparatorChar);
        if (isSkipALastChar && isSkipBLastChar)
        {
            if (Enumerable.SequenceEqual(pathA.SkipLast(1), pathB.SkipLast(1))) { return true; }
        }
        else if (isSkipALastChar)
        {
            if (Enumerable.SequenceEqual(pathA.SkipLast(1), pathB)) { return true; }
        }
        else if (isSkipBLastChar)
        {
            if (Enumerable.SequenceEqual(pathA, pathB.SkipLast(1))) { return true; }
        }

        return false;
        */
    }


    public static bool IsChildDirectoryPath(string parent, string target)
    {
        return IsSameDirectoryPath(parent, Path.GetDirectoryName(target));
    }

    public static bool IsRootDirectoryPath(string path)
    {
        if (path == String.Empty)
        {
            return true;
        }
        else if (Path.IsPathRooted(path) &&
            ( path.EndsWith(Path.DirectorySeparatorChar)
                || path.EndsWith(Path.AltDirectorySeparatorChar))
                )
        {
            return true;
        }
        else if (path.Contains(Path.DirectorySeparatorChar) is false && path.Contains(Path.AltDirectorySeparatorChar) is false)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    public static int GetDirectoryDepth(string path)
    {
        return path.Count(c => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar);
    }
}


public static class ArchivePathExtensions
{
    public static string GetDirectoryPath(this IArchiveEntry entry)
    {
        return entry.IsDirectory ? entry.Key : Path.GetDirectoryName(entry.Key);
    }

    public static bool IsRootDirectoryEntry(this IArchiveEntry entry)
    {
        //return IsRootDirectoryPath(entry.IsDirectory ? entry.Key : Path.GetDirectoryName(entry.Key));
        return DirectoryPathHelper.IsRootDirectoryPath(Path.GetDirectoryName(entry.Key));
    }


    public static bool IsSameDirectoryPath(this IArchiveEntry x, IArchiveEntry y)
    {
        if (x == null && y == null) { throw new NotSupportedException(); }

        if (x == null)
        {
            return IsRootDirectoryEntry(y);
        }
        else if (y == null)
        {
            return IsRootDirectoryEntry(x);
        }
        else
        {
            var pathX = x.IsDirectory ? x.Key : Path.GetDirectoryName(x.Key);
            var pathY = y.IsDirectory ? y.Key : Path.GetDirectoryName(y.Key);

            //ReadOnlySpan<char> 
            return DirectoryPathHelper.IsSameDirectoryPath(pathX, pathY);
        }
    }



    public static bool IsSameDirectoryPath(this ArchiveDirectoryToken x, ArchiveDirectoryToken y)
    {
        if (x == null && y == null) { throw new NotSupportedException(); }

        if (x.Key == null)
        {
            return IsRootDirectoryEntry(y);
        }
        else if (x.Key == null)
        {
            return IsRootDirectoryEntry(x);
        }
        else
        {
            return DirectoryPathHelper.IsSameDirectoryPath(x.Key, y.Key);
        }
    }

    public static bool IsChildDirectoryPath(this ArchiveDirectoryToken parent, ArchiveDirectoryToken target)
    {
        if (parent.Key == null)
        {
            return IsRootDirectoryEntry(target);
        }

        if (target.Entry.IsDirectory)
        {
            return DirectoryPathHelper.IsSameDirectoryPath(parent.DirectoryPath, Path.GetDirectoryName(target.DirectoryPath));
        }
        else
        {
            return DirectoryPathHelper.IsSameDirectoryPath(parent.DirectoryPath, Path.GetDirectoryName(target.DirectoryPath));
        }
    }

    public static bool IsRootDirectoryEntry(this ArchiveDirectoryToken token)
    {
        if (token.Key == null) { return true; }
        else { return IsRootDirectoryEntry(token.Entry); }
    }


}
