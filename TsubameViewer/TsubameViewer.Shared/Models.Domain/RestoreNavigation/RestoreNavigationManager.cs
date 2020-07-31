using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using TsubameViewer.Models.Infrastructure;

namespace TsubameViewer.Models.Domain.RestoreNavigation
{
    public sealed class RestoreNavigationManager
    {
        private readonly NavigationStackRepository _navigationStackRepository;

        public RestoreNavigationManager()
        {
            _navigationStackRepository = new NavigationStackRepository();
        }

        public void SetCurrentNavigationEntry(PageEntry pageEntry)
        {
            _navigationStackRepository.SetCurrentNavigationEntry(pageEntry);
        }

        public PageEntry GetCurrentNavigationEntry()
        {
            return _navigationStackRepository.GetCurrentNavigationEntry();
        }

        public void SetBackNavigationEntries(IEnumerable<PageEntry> entries)
        {
            _navigationStackRepository.SetBackNavigationEntries(entries.ToArray());
        }

        public void SetForwardNavigationEntries(IEnumerable<PageEntry> entries)
        {
            _navigationStackRepository.SetForwardNavigationEntries(entries.ToArray());
        }

        public PageEntry[] GetBackNavigationEntries()
        {
            return _navigationStackRepository.GetBackNavigationEntries();
        }

        public PageEntry[] GetForwardNavigationEntries()
        {
            return _navigationStackRepository.GetForwardNavigationEntries();
        }



        internal class NavigationStackRepository : FlagsRepositoryBase
        {
            public NavigationStackRepository()
            {

            }

            public const string CurrentNavigationEntryName = "CurrentNavigationEntry";
            public const string BackNavigationEntriesName = "BackNavigationEntries";
            public const string ForwardNavigationEntriesName = "ForwardNavigationEntries";


            public PageEntry GetCurrentNavigationEntry()
            {
                var json = Read<byte[]>(null, CurrentNavigationEntryName);
                if (json == null) { return null; }
                return JsonSerializer.Deserialize<PageEntry>(json);
            }

            public void SetCurrentNavigationEntry(PageEntry entry)
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(entry);
                Save(bytes, CurrentNavigationEntryName);
            }

            public PageEntry[] GetBackNavigationEntries()
            {
                var json = Read<byte[]>(null, BackNavigationEntriesName);
                if (json == null) { return new PageEntry[0]; }
                return JsonSerializer.Deserialize<PageEntry[]>(json);
            }

            public void SetBackNavigationEntries(PageEntry[] entries)
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(entries);
                Save(bytes, BackNavigationEntriesName);
            }

            public PageEntry[] GetForwardNavigationEntries()
            {
                var json = Read<byte[]>(null, ForwardNavigationEntriesName);
                if (json == null) { return new PageEntry[0]; }
                return JsonSerializer.Deserialize<PageEntry[]>(json);
            }

            public void SetForwardNavigationEntries(PageEntry[] entries)
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(entries);
                Save(bytes, ForwardNavigationEntriesName);
            }
        }
    }

    public class PageEntry
    {
        public PageEntry() { }

        public PageEntry(string pageName, IEnumerable<KeyValuePair<string, object>> parameters)
        {
            PageName = pageName;
            Parameters = parameters?.ToDictionary(x => x.Key, (x) => x.Value.ToString()).ToList();
        }

        public string PageName { get; set; }
        public List<KeyValuePair<string, string>> Parameters { get; set; }
    }
}
