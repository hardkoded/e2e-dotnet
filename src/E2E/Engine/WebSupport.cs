// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.ExceptionServices;
using Microsoft.Playwright;

namespace E2E.Engine;

/// <summary>What the web engine's files share (upstream's <c>web/src/support.ts</c>, beside <see cref="WebErrors"/>).</summary>
internal static class WebSupport
{
    /// <summary>The longest filename <see cref="SanitizeFilename"/> keeps.</summary>
    private const int MaxFilenameLength = 64;

    /// <summary>Constrains a caller-supplied artifact label to a safe filename (upstream's <c>sanitizeFilename</c>).</summary>
    public static string SanitizeFilename(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var safe = string.Create(name.Length, name, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                var c = source[i];
                span[i] = c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.' or '_' or '-' ? c : '_';
            }
        });
        if (safe.Length > MaxFilenameLength)
        {
            safe = safe[..MaxFilenameLength];
        }

        return safe.Length == 0 ? "artifact" : safe;
    }

    /// <summary>
    /// Releases a handle without waiting and without failing: a page that is gone has nothing left to release (upstream's inline
    /// <c>void handle.dispose().catch(() =&gt; undefined)</c>). .NET-only helper; C# has no such one-line idiom.
    /// </summary>
    public static void DisposeQuietly(IAsyncDisposable handle)
    {
        _ = DisposeQuietlyAsync(handle);
    }

    /// <summary>Releases the handle a read yields once it settles, for a read the deadline abandoned. .NET-only, as <see cref="DisposeQuietly"/>.</summary>
    public static void DisposeWhenSettled<T>(Task<T> read)
        where T : IAsyncDisposable
    {
        _ = DisposeWhenSettledAsync(read);
    }

    /// <summary>
    /// Releases the pin of a lookup that settles after the operation abandoned it, so the list is not held for good. .NET-only, as
    /// <see cref="DisposeWhenSettled"/>: the pin is the port's own (see <see cref="RefRegistry.LookupPinnedAsync"/>).
    /// </summary>
    public static void DisposePinWhenSettled(Task<(ActionTarget Target, IDisposable Pin)> lookup)
    {
        _ = lookup.ContinueWith(
            static done =>
            {
                if (done.IsCompletedSuccessfully)
                {
                    done.Result.Pin.Dispose();
                }
                else
                {
                    // The lookup failed: nothing is pinned, and its error was the caller's.
                    _ = done.Exception;
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static async Task DisposeQuietlyAsync(IAsyncDisposable handle)
    {
        try
        {
            await handle.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The page or connection is gone, and the handle with it.
        }
    }

    private static async Task DisposeWhenSettledAsync<T>(Task<T> read)
        where T : IAsyncDisposable
    {
        try
        {
            await DisposeQuietlyAsync(await read.ConfigureAwait(false)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The read failed: no handle arrived.
        }
    }
}

/// <summary>
/// Holds one error raised on a path nobody awaits (a native dialog nobody handled, a route handler that broke its contract) until
/// the next step enters the engine, which then fails with the real cause, or the attempt settles when no step follows (upstream's
/// <c>ErrorLatch</c>, <c>web/src/support.ts</c>). Rethrows once: the failure belongs to the step that observes it, not to every later one.
/// </summary>
internal sealed class ErrorLatch
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Task, string> _running = [];
    private Exception? _pending;
    private bool _abandoned;

    /// <summary>Latches an error; the first one wins until it is thrown. Dropped once <see cref="SettleAsync"/> gave up on a handler.</summary>
    public void Latch(Exception error)
    {
        lock (_gate)
        {
            if (!_abandoned)
            {
                _pending ??= error;
            }
        }
    }

    /// <summary>
    /// Tracks one unawaited <paramref name="kind"/> of path (a route or dialog handler) until it settles, so <see cref="SettleAsync"/> can wait
    /// for it. Its own failure is the caller's to latch.
    /// </summary>
    public void Track(string kind, Task work)
    {
        ArgumentNullException.ThrowIfNull(work);
        // The path leaves the set before the task a settle waits on completes, so a settle that saw it finish never reports it as outliving the budget.
        var tracked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            _running[tracked.Task] = kind;
        }

        _ = work.ContinueWith(
            _ =>
            {
                lock (_gate)
                {
                    _running.Remove(tracked.Task);
                }

                tracked.SetResult();
            },
            TaskScheduler.Default);
    }

    /// <summary>
    /// Rethrows the latched error once; with none latched yet, first waits, within <paramref name="timeout"/>, for the tracked paths
    /// running when it was called. One of those that outlives the budget fails closed: <c>CLEANUP_TIMEOUT</c> naming it, and whatever it
    /// throws afterwards is dropped here on purpose rather than landing on a verdict already reached. A path that starts during the wait
    /// is left to the next settle. Upstream's <c>settle(budget)</c>.
    /// </summary>
    public async Task SettleAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        ThrowPending();
        Task[] waited;
        lock (_gate)
        {
            waited = _abandoned ? [] : [.. _running.Keys];
        }

        if (waited.Length > 0)
        {
            try
            {
                await Task.WhenAll(waited).WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is TimeoutException or OperationCanceledException)
            {
                // The budget ended: whatever still runs is reported below.
            }

            string[] kinds;
            lock (_gate)
            {
                kinds = [.. waited.Where(_running.ContainsKey).Select(path => _running[path]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
                if (kinds.Length > 0)
                {
                    _running.Clear();
                    _abandoned = true;
                }
            }

            if (kinds.Length > 0)
            {
                ThrowPending();
                throw new EngineException(
                    "CLEANUP_TIMEOUT",
                    $"a {string.Join(" and ", kinds)} handler was still running when the cleanup budget ended; anything it throws now is dropped");
            }
        }

        ThrowPending();
    }

    /// <summary>Rethrows the latched error once, if any.</summary>
    public void ThrowPending()
    {
        Exception? pending;
        lock (_gate)
        {
            pending = _pending;
            _pending = null;
        }

        if (pending is not null)
        {
            ExceptionDispatchInfo.Throw(pending);
        }
    }
}

/// <summary>
/// What a node reference stands for: an <see cref="ActionTarget"/> or a <see cref="ListedTarget"/>. .NET-only base type: upstream types
/// <c>ActionTarget | ListedTarget</c> as a union, which C# has no syntax for.
/// </summary>
internal abstract record RefTarget;

/// <summary>What an action runs on (upstream's <c>ActionTarget</c>, <c>web/src/support.ts</c>). The port has no strict locator target: it pins no node to a Playwright locator.</summary>
internal abstract record ActionTarget : RefTarget;

/// <summary>An action target that is already an element handle (upstream's <c>{ kind: 'element' }</c>).</summary>
internal sealed record ElementTarget(IElementHandle Element) : ActionTarget;

