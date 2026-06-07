using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
#nullable enable
namespace TsubameViewer;

public static class TaskExtensions
{
    /// <summary>
    /// 例外をキャッチし、同期文脈またはスレッドプールを通じてUnhandledExceptionへ伝播させます。
    /// </summary>
    public static async void FireAndForgetSafe(this Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            // 現在の同期文脈（UIスレッドなど）があればそこへ、なければThreadPoolへ例外を逃がす
            if (SynchronizationContext.Current is { } context)
            {
                context.Post(_ => ExceptionDispatchInfo.Throw(ex), null);
            }
            else
            {
                ThreadPool.QueueUserWorkItem(_ => ExceptionDispatchInfo.Throw(ex));
            }
        }
    }

    /// <summary>
    /// 例外をキャッチし、同期文脈またはスレッドプールを通じてUnhandledExceptionへ伝播させます。
    /// </summary>
    public static async void FireAndForgetSafe(this ValueTask task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            // 現在の同期文脈（UIスレッドなど）があればそこへ、なければThreadPoolへ例外を逃がす
            if (SynchronizationContext.Current is { } context)
            {
                context.Post(_ => ExceptionDispatchInfo.Throw(ex), null);
            }
            else
            {
                ThreadPool.QueueUserWorkItem(_ => ExceptionDispatchInfo.Throw(ex));
            }
        }
    }

    public static async void FireAndForgetSafe<T>(this IAsyncOperation<T> task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            // 現在の同期文脈（UIスレッドなど）があればそこへ、なければThreadPoolへ例外を逃がす
            if (SynchronizationContext.Current is { } context)
            {
                context.Post(_ => ExceptionDispatchInfo.Throw(ex), null);
            }
            else
            {
                ThreadPool.QueueUserWorkItem(_ => ExceptionDispatchInfo.Throw(ex));
            }
        }
    }
}

public static class AsyncTaskErrorHandler
{
    public static async void Handle(Func<Task> taskFactory)
    {
        try
        {
            await taskFactory();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ExceptionDispatchInfo.Throw(ex);
        }
    }

    public static async void Handle<TState>(TState state, Func<TState, Task> taskFactory)
    {
        try
        {
            await taskFactory(state);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ExceptionDispatchInfo.Throw(ex);
        }
    }
}
