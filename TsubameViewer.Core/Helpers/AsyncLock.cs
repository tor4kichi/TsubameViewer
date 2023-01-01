using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TsubameViewer.Core;

public sealed class AsyncLock
{
    public AsyncLock()
    {
        _semaphore = new(1, 1);
    }

    public AsyncLock(int lineCount)
    {
        _semaphore = new(lineCount, lineCount);
    }

    private readonly SemaphoreSlim _semaphore;

    //
    // 概要:
    //     Acquires the lock, then provides a disposable to release it.
    //
    // パラメーター:
    //   ct:
    //     A cancellation token to cancel the lock
    //
    // 戻り値:
    //     An IDisposable instance that allows the release of the lock.
    public async Task<IDisposable> LockAsync(CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        return Disposable.Create(delegate
        {
            _semaphore.Release();
        });
    }
}
