using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace TsubameViewer.Core.Helpers;
public class PerfomanceStopWatch : IStopwatch
{
    public static PerfomanceStopWatch StartNew(string groupName)
    {
        var sw = new PerfomanceStopWatch(groupName);
        sw.Restart();
        return sw;
    }

    private readonly Stopwatch sw = new Stopwatch();
    public string GroupName { get; }

    public PerfomanceStopWatch(string groupName)
    {
        GroupName = groupName;
    }

    public TimeSpan Elapsed => sw.Elapsed;

    public void Restart()
    {
        _lastElapsed = 0;
        sw.Restart();
    }

    public void Start()
    {
        sw.Start();
    }
    long _lastElapsed = 0;
    public void ElapsedWrite(string name)
    {
        Debug.WriteLine($"[{GroupName} elapsed] {sw.ElapsedMilliseconds - _lastElapsed}ms / Total {sw.ElapsedMilliseconds}ms [{name}]");
        _lastElapsed = sw.ElapsedMilliseconds;
    }

    public void Stop()
    {
        sw.Stop();
    }
}
