// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Playwright;

namespace E2E.Engine;

/// <summary>
/// One handle to the in-page array the reader filled, and the element handles made from it on demand (upstream's <c>ElementList</c>,
/// <c>web/src/element-list.ts</c>).
/// </summary>
/// <remarks>
/// An observation can list thousands of nodes and an action touches a few, so a handle per node would cost a round trip to create and
/// another to release for every node, every observation. The array keeps each element alive, which is what a handle did: a ref names
/// the element the walk read, never whatever sits at that position now. Disposing the list releases the array and every handle made
/// from it.
/// </remarks>
internal sealed class ElementList(IJSHandle handle) : IDisposable
{
    private readonly Lock _gate = new();
    private readonly Dictionary<int, Task<IElementHandle>> _resolved = [];
    private bool _disposed;

    /// <summary>The in-page array of the elements the walk read.</summary>
    public IJSHandle Handle { get; } = handle;

    /// <summary>
    /// A new handle to one element of the walk. The caller owns it and disposes it; a missing element, or a document replaced since
    /// the walk, is a stale node.
    /// </summary>
    public async Task<IElementHandle> FetchAsync(int index)
    {
        IJSHandle made;
        try
        {
            made = await Handle.EvaluateHandleAsync("(elements, at) => elements[at]", index).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw WebErrors.NavigationStaleOr(ex, "resolve observed node");
        }

        // A handle to a missing element is a plain JSHandle: the element's type is what tells the two apart.
        if (made is IElementHandle element)
        {
            return element;
        }

        WebSupport.DisposeQuietly(made);
        throw new EngineException(EngineErrorCodes.NodeStale, $"observation node {index} lost its element", retryable: true);
    }

    /// <summary>The element of one node, made once and kept until the list is disposed.</summary>
    public Task<IElementHandle> AtAsync(int index)
    {
        TaskCompletionSource<IElementHandle> source;
        lock (_gate)
        {
            if (_disposed)
            {
                return Task.FromException<IElementHandle>(StaleList());
            }

            if (_resolved.TryGetValue(index, out var element))
            {
                return element;
            }

            source = new TaskCompletionSource<IElementHandle>(TaskCreationOptions.RunContinuationsAsynchronously);
            _resolved[index] = source.Task;
        }

        _ = ResolveAsync(index, source);
        return source.Task;
    }

    /// <summary>Releases the array and every element made from it; safe to call again.</summary>
    public void Dispose()
    {
        Task<IElementHandle>[] made;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            made = [.. _resolved.Values];
            _resolved.Clear();
        }

        foreach (var element in made)
        {
            _ = ReleaseAsync(element);
        }

        WebSupport.DisposeQuietly(Handle);
    }

    private async Task ResolveAsync(int index, TaskCompletionSource<IElementHandle> source)
    {
        try
        {
            var made = await FetchAsync(index).ConfigureAwait(false);
            lock (_gate)
            {
                // Completed under the lock, so a disposal cannot slip between the check and the result.
                if (!_disposed)
                {
                    source.SetResult(made);
                    return;
                }
            }

            WebSupport.DisposeQuietly(made);
            source.SetException(StaleList());
        }
        catch (Exception cause)
        {
            lock (_gate)
            {
                // A later read tries again, unless the list was disposed meanwhile: then the entry is already gone.
                if (_resolved.TryGetValue(index, out var current) && ReferenceEquals(current, source.Task))
                {
                    _resolved.Remove(index);
                }

                source.SetException(_disposed ? StaleList() : cause);
            }
        }
    }

    private static async Task ReleaseAsync(Task<IElementHandle> element)
    {
        try
        {
            var made = await element.ConfigureAwait(false);
            await made.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The element never arrived or its page is gone: nothing is left to release.
        }
    }

    /// <summary>The observation the list belonged to was replaced or cleared, so its refs no longer bind.</summary>
    private static EngineException StaleList() =>
        new(EngineErrorCodes.NodeStale, "observation node references were released", retryable: true);
}
