// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Engine;

/// <summary>An observed node: one position in the element list of the document it was read from (upstream's <c>ListedTarget</c>, <c>web/src/refs.ts</c>).</summary>
internal sealed record ListedTarget(ElementList List, int Index) : RefTarget;

/// <summary>A complete observation and the private handles that back its semantic refs (upstream's <c>CapturedObservation</c>).</summary>
internal sealed record CapturedObservation(Observation Snapshot, Dictionary<string, ListedTarget> Generation);

/// <summary>
/// Node reference registry for one attempt: the ids the engine mints for located and observed nodes and the Playwright targets behind
/// them (upstream's <c>RefRegistry</c>, <c>web/src/refs.ts</c>).
/// </summary>
/// <remarks>
/// Two populations with different lifetimes live here. Locator-backed refs from <c>locate</c> hold no live handles and are pruned
/// oldest-first past a bound. List-backed refs from <c>observe</c> form one generation per observation: the whole generation is swapped
/// atomically and its element lists disposed in a sweep, so locator-ref eviction can never destroy a list an in-flight observation still
/// references.
/// </remarks>
internal sealed class RefRegistry
{
    /// <summary>Located refs are pruned oldest-first past this bound so the map cannot grow unboundedly.</summary>
    private const int MaxStoredRefs = 2048;

    private readonly Lock _gate = new();
    private readonly Dictionary<string, ActionTarget> _located = new(StringComparer.Ordinal);

    // Located ids in the order they were stored: a Dictionary keeps no order once an entry is removed.
    private readonly Queue<string> _locatedOrder = new();
    private int _counter;
    private int _capture;
    private Dictionary<string, ListedTarget> _observation = new(StringComparer.Ordinal);

    public string MintId()
    {
        lock (_gate)
        {
            return MintIdLocked();
        }
    }

    /// <summary>Reserves ids before a document can stamp them, even if its response never arrives.</summary>
    public int ReserveIds(int count)
    {
        lock (_gate)
        {
            var first = _counter + 1;
            _counter += count;
            return first;
        }
    }

    /// <summary>Stores one located target under a fresh id, pruning the oldest past the bound.</summary>
    public string StoreLocated(ActionTarget target)
    {
        lock (_gate)
        {
            var id = MintIdLocked();
            _located[id] = target;
            _locatedOrder.Enqueue(id);
            while (_located.Count > MaxStoredRefs)
            {
                var oldest = _locatedOrder.Dequeue();
                if (_located.Remove(oldest, out var evicted) && evicted is ElementTarget element)
                {
                    WebSupport.DisposeQuietly(element.Element);
                }
            }

            return id;
        }
    }

    /// <summary>
    /// Ids are the engine's; revisions are the harness's. The adapter already rejected a ref from a superseded resolution, so lookup is by
    /// id alone (the port's refs are plain strings, with no revision to carry). An observed node's element handle is made here, the first
    /// time an action reaches for it.
    /// </summary>
    public async Task<ActionTarget> LookupAsync(string id)
    {
        ListedTarget observed;
        lock (_gate)
        {
            if (_located.TryGetValue(id, out var located))
            {
                return located;
            }

            if (!_observation.TryGetValue(id, out var listed))
            {
                throw new EngineException(EngineErrorCodes.NodeStale, $"node reference {id} is stale", retryable: true);
            }

            observed = listed;
        }

        return new ElementTarget(await observed.List.AtAsync(observed.Index).ConfigureAwait(false));
    }

    /// <summary>
    /// <see cref="LookupAsync"/>, and the list behind an observed node is kept from being released until the returned pin is disposed. An
    /// action holds its element handle for as long as it runs, and a concurrent observation publishing a new generation must not release
    /// that handle under it. .NET-only: the port's <c>Browser</c> and locators run operations concurrently (a click, a read and an assertion on
    /// one page at once), so an observation can publish while a click runs.
    /// </summary>
    public async Task<(ActionTarget Target, IDisposable Pin)> LookupPinnedAsync(string id)
    {
        ListedTarget observed;
        IDisposable pin;
        lock (_gate)
        {
            if (_located.TryGetValue(id, out var located))
            {
                return (located, NoPin.Instance);
            }

            if (!_observation.TryGetValue(id, out var listed))
            {
                throw new EngineException(EngineErrorCodes.NodeStale, $"node reference {id} is stale", retryable: true);
            }

            observed = listed;
            pin = observed.List.Pin();
        }

        try
        {
            return (new ElementTarget(await observed.List.AtAsync(observed.Index).ConfigureAwait(false)), pin);
        }
        catch
        {
            pin.Dispose();
            throw;
        }
    }

    /// <summary>Starts a capture, preventing older in-flight observations from publishing afterward.</summary>
    public int BeginCapture()
    {
        lock (_gate)
        {
            return ++_capture;
        }
    }

    /// <summary>Publishes only the current capture; pixel-only recovery also retires located refs.</summary>
    public void Publish(int capture, CapturedObservation captured)
    {
        ArgumentNullException.ThrowIfNull(captured);
        lock (_gate)
        {
            if (capture != _capture)
            {
                throw new EngineException(EngineErrorCodes.NodeStale, "observation capture was superseded", retryable: true);
            }

            if (captured.Snapshot.TreeUnavailable)
            {
                ClearLocked();
            }

            Dispose(_observation);
            _observation = captured.Generation;
        }
    }

    /// <summary>Releases every ref of the attempt: located and observed alike.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            ClearLocked();
        }
    }

    /// <summary>Disposes every element handle in one generation, each list once however many nodes it backs.</summary>
    public static void Dispose(IReadOnlyDictionary<string, ListedTarget> generation)
    {
        ArgumentNullException.ThrowIfNull(generation);
        DisposeTargets(generation.Values);
    }

    private static void DisposeTargets(IEnumerable<RefTarget> targets)
    {
        var lists = new HashSet<ElementList>();
        foreach (var target in targets)
        {
            switch (target)
            {
                case ListedTarget listed:
                    lists.Add(listed.List);
                    break;
                case ElementTarget element:
                    WebSupport.DisposeQuietly(element.Element);
                    break;
            }
        }

        foreach (var list in lists)
        {
            list.Dispose();
        }
    }

    private sealed class NoPin : IDisposable
    {
        public static readonly NoPin Instance = new();

        public void Dispose()
        {
        }
    }

    private string MintIdLocked()
    {
        _counter++;
        return "n" + _counter;
    }

    private void ClearLocked()
    {
        _capture++;
        Dispose(_observation);
        _observation = new Dictionary<string, ListedTarget>(StringComparer.Ordinal);
        DisposeTargets(_located.Values);
        _located.Clear();
        _locatedOrder.Clear();
    }
}
