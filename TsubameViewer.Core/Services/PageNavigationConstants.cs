using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Web;

namespace TsubameViewer.Core.Services;

public static class PageNavigationConstants
{
    public const string GeneralPathKey = "q"; // general content url
    public const string AlbamPathKey = "al"; // albam url
    private const string PageName = "p"; // pageName
    public const string Restored = "re"; // restored

    public static string MakeStorageItemIdWithPage(string path, string pageName)
    {
        return $"{path}?{PageName}={pageName}";
    }

    public static (string Path, string PageName) ParseStorageItemId(string id)
    {            
        var storageItemIdValues = id.Split('?', 2);
        if (storageItemIdValues.Length == 1)
        {
            return (storageItemIdValues[0], String.Empty);
        }
        else if (storageItemIdValues.Length == 2)
        {
            var queries = HttpUtility.ParseQueryString(storageItemIdValues[1]);
            if (queries.Get(PageName) is not null and var pageName)
            {
                return (storageItemIdValues[0], pageName);
            }
            else
            {
                throw new NotSupportedException(storageItemIdValues[1]);
            }
        }
        else
        {
            throw new NotSupportedException(id);
        }                
    }

    public readonly static TimeSpan BusyWallDisplayDelayTime = TimeSpan.FromMilliseconds(750);
}
