using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        public Task SetBackNavigationEntriesAsync(IEnumerable<PageEntry> entries)
        {
            return _navigationStackRepository.SetBackNavigationEntriesAsync(entries.ToArray());
        }

        public Task SetForwardNavigationEntriesAsync(IEnumerable<PageEntry> entries)
        {
            return _navigationStackRepository.SetForwardNavigationEntriesAsync(entries.ToArray());
        }

        public Task<PageEntry[]> GetBackNavigationEntriesAsync()
        {
            return _navigationStackRepository.GetBackNavigationEntriesAsync();
        }

        public Task<PageEntry[]> GetForwardNavigationEntriesAsync()
        {
            return _navigationStackRepository.GetForwardNavigationEntriesAsync();
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
                return Read<PageEntry>(null, CurrentNavigationEntryName);
            }

            public void SetCurrentNavigationEntry(PageEntry entry)
            {
                Save(entry, CurrentNavigationEntryName);
            }

            public Task<PageEntry[]> GetBackNavigationEntriesAsync()
            {
                return ReadFileAsync<PageEntry[]>(null, BackNavigationEntriesName);
            }

            public Task SetBackNavigationEntriesAsync(PageEntry[] entries)
            {
                return SaveFileAsync(entries, BackNavigationEntriesName);
            }

            public Task<PageEntry[]> GetForwardNavigationEntriesAsync()
            {
                return ReadFileAsync<PageEntry[]>(null, ForwardNavigationEntriesName);
            }

            public Task SetForwardNavigationEntriesAsync(PageEntry[] entries)
            {
                return SaveFileAsync(entries, ForwardNavigationEntriesName);
            }
        }
    }

    public class PageEntry
    {
        public PageEntry() { }

        public PageEntry(string pageName)
        {
            PageName = pageName;
            Parameters = new List<KeyValuePair<string, string>>();
        }

        public PageEntry(string pageName, IEnumerable<KeyValuePair<string, object>> parameters)
        {
            PageName = pageName;
            Parameters = parameters?.Select(x => new KeyValuePair<string, string>(x.Key, x.Value.ToString())).ToList();
        }

        public string PageName { get; set; }
        public List<KeyValuePair<string, string>> Parameters { get; set; }
    }
}
