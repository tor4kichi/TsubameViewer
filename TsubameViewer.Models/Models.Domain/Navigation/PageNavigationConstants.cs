using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Web;

namespace TsubameViewer.Models.Domain.Navigation
{
    public static class PageNavigationConstants
    {
        public const string GeneralPathKey = "q"; // general content url
        public const string AlbamPathKey = "al"; // albam url
        private const string PageName = "p"; // pageName
        private const string ArchiveFolderName = "ac"; // archiveFolder
        public const string Restored = "re"; // restored

        public static string MakeStorageItemIdWithPage(string path, string pageName)
        {
            return $"{path}?{PageName}={pageName}";
        }

        public static string MakeStorageItemIdWithArchiveFolder(string path, string archiveFolderName)
        {
            return $"{path}?{ArchiveFolderName}={archiveFolderName}";
        }

        public static (string Path, string PageName, string ArchiveFolderName) ParseStorageItemId(string id)
        {            
            var storageItemIdValues = id.Split('?', 2);
            if (storageItemIdValues.Length == 1)
            {
                return (storageItemIdValues[0], String.Empty, String.Empty);
            }
            else if (storageItemIdValues.Length == 2)
            {
                var queries = HttpUtility.ParseQueryString(storageItemIdValues[1]);
                if (queries.Get(PageName) is not null and var pageName)
                {
                    return (storageItemIdValues[0], pageName, String.Empty);
                }
                else if (queries.Get(ArchiveFolderName) is not null and var archiveFolderName)
                {
                    return (storageItemIdValues[0], String.Empty, archiveFolderName);
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
}
