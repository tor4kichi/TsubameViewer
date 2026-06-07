using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;

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
    // アプリケーション全体で監視できるイベント
    public static event EventHandler<ExceptionEventArgs>? OnError;

    public static async void Handle(Func<Task> taskFactory)
    {
        try
        {
            await taskFactory();
        }
        catch (Exception ex)
        {
            // 自前の集約イベントを叩く
            OnError?.Invoke(null, new ExceptionEventArgs(ex));

            // 最終的にUnhandledExceptionへ強制伝播
            ExceptionDispatchInfo.Throw(ex);
        }
    }

    public static async void Handle<TState>(TState state, Func<TState, Task> taskFactory)
    {
        try
        {
            await taskFactory(state);
        }
        catch (Exception ex)
        {
            // 自前の集約イベントを叩く
            OnError?.Invoke(null, new ExceptionEventArgs(ex));

            // 最終的にUnhandledExceptionへ強制伝播
            ExceptionDispatchInfo.Throw(ex);
        }
    }
}

public class ExceptionEventArgs(Exception exception) : EventArgs
{
    public Exception Exception { get; } = exception;
}