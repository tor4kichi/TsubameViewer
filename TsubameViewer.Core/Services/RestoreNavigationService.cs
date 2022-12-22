using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TsubameViewer.Core.Contracts.Services;
using TsubameViewer.Core.Infrastructure;

namespace TsubameViewer.Core.Services;

public sealed class RestoreNavigationService : IRestoreNavigationService
{
    private readonly NavigationStackRepository _navigationStackRepository;

    public RestoreNavigationService()
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

