using SharpCompress.Archives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TsubameViewer.Core.Models.ImageViewer;

public class ArchiveDirectoryEqualityComparer : IEqualityComparer<IArchiveEntry>
{
    public static readonly ArchiveDirectoryEqualityComparer Default = new ArchiveDirectoryEqualityComparer();
    private ArchiveDirectoryEqualityComparer() { }

    public bool Equals(IArchiveEntry x, IArchiveEntry y)
    {
        return x.IsSameDirectoryPath(y);
    }

    public int GetHashCode(IArchiveEntry obj)
    {
        var pathX = obj.IsDirectory ? obj.Key : Path.GetDirectoryName(obj.Key);
        if (pathX.EndsWith(Path.DirectorySeparatorChar))
        {
            return pathX.GetHashCode();
        }
        else if (pathX.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return (pathX.Remove(pathX.Length - 1) + Path.DirectorySeparatorChar).GetHashCode();
        }
        else
        {
            return (pathX + Path.DirectorySeparatorChar).GetHashCode();
        }
    }
}
