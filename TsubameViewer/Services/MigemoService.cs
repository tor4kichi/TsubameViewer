using CsMigemo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Storage;
#nullable enable
namespace TsubameViewer.Services;

public sealed class MigemoService
{
    public readonly static MigemoService Default = new();
    private readonly Migemo _migemo;

    private MigemoService()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        Stream stream = assembly.GetManifestResourceStream("TsubameViewer.Assets.migemo-compact-dict");
        _migemo = new Migemo(stream, RegexOperator.DEFAULT);        
    }

    private string? _lastQuery;
    private Regex? _cachedRegex;
    public static Regex Query(string q)
    {
        var migemo = Default;
        if (migemo._lastQuery != null
            && migemo._lastQuery.Equals(q, StringComparison.Ordinal)
            && migemo._cachedRegex != null)
        {
            return migemo._cachedRegex;
        }

        migemo._lastQuery = q;
        return migemo._cachedRegex = new Regex(migemo._migemo.Query(q));
    }
}
