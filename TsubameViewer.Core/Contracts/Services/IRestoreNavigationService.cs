using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TsubameViewer.Core.Contracts.Services;

public interface IRestoreNavigationService
{
    Task<PageEntry[]> GetBackNavigationEntriesAsync();
    PageEntry GetCurrentNavigationEntry();
    Task<PageEntry[]> GetForwardNavigationEntriesAsync();
    Task SetBackNavigationEntriesAsync(IEnumerable<PageEntry> entries);
    void SetCurrentNavigationEntry(PageEntry pageEntry);
    Task SetForwardNavigationEntriesAsync(IEnumerable<PageEntry> entries);
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