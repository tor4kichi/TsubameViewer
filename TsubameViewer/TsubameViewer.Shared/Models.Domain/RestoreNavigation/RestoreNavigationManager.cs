using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
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

            public async Task<PageEntry[]> GetBackNavigationEntriesAsync()
            {
                return await ReadFileAsync<PageEntry[]>(null, BackNavigationEntriesName);
            }

            public async Task SetBackNavigationEntriesAsync(PageEntry[] entries)
            {
                await SaveFileAsync(entries, BackNavigationEntriesName);
            }

            public async Task<PageEntry[]> GetForwardNavigationEntriesAsync()
            {
                return await ReadFileAsync<PageEntry[]>(null, ForwardNavigationEntriesName);
            }

            public async Task SetForwardNavigationEntriesAsync(PageEntry[] entries)
            {
                await SaveFileAsync(entries, ForwardNavigationEntriesName);
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
