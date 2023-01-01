using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TsubameViewer.Core;

public static class EnumerableTaskHelper
{
    // Note: 論理スレッド数は Environment.ProcessorCount で取得できる


    public static async Task ToAwaitableParallelTaskAsync<T>(this IEnumerable<T> items, Func<T, Task> taskFactory, int maxDegreeOfParallelism = -1)
    {
        if (maxDegreeOfParallelism == 0) { throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism), "0は指定できない"); }
        if (maxDegreeOfParallelism <= -2) { throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism), "-1より小さい値は設定できない"); }

        List<Task> tasks = new();
        foreach (var item in items)
        {
            if (tasks.Count == maxDegreeOfParallelism)
            {
                var exit = await Task.WhenAny(tasks);
                tasks.Remove(exit);
            }

            tasks.Add(taskFactory(item));
        }

        await Task.WhenAll(tasks);
    }

    public static async Task ToAwaitableParallelTaskAsync<T>(this IAsyncEnumerable<T> items, Func<T, Task> taskFactory, int maxDegreeOfParallelism = -1)
    {
        if (maxDegreeOfParallelism == 0) { throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism), "0は指定できない"); }
        if (maxDegreeOfParallelism <= -2) { throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism), "-1より小さい値は設定できない"); }

        List<Task> tasks = new();
        await foreach (var item in items)
        {
            if (tasks.Count == maxDegreeOfParallelism)
            {
                var completeTask = await Task.WhenAny(tasks);
                tasks.Remove(completeTask);
            }

            tasks.Add(taskFactory(item));
        }

        await Task.WhenAll(tasks);
    }
}
