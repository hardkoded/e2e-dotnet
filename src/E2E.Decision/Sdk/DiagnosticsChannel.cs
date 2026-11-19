// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;

namespace E2E.Decision.Sdk;

/// <summary>
/// .NET-only stand-in for Node's <c>node:diagnostics_channel</c>, which the AI SDK's tracing channel publishes to
/// (<c>tracing-channel-publisher.ts</c>). A tracing channel is found by name, and every caller that asks for a name gets the same one.
/// </summary>
public static class DiagnosticsChannel
{
    private static readonly ConcurrentDictionary<string, TracingChannel> TracingChannels = new(StringComparer.Ordinal);

    /// <summary><c>diagnosticsChannel.tracingChannel(name)</c>.</summary>
    public static TracingChannel GetTracingChannel(string name) =>
        TracingChannels.GetOrAdd(name, static key => new TracingChannel(key));
}

/// <summary>A named channel that delivers each published message to its subscribers (<c>diagnosticsChannel.channel</c>).</summary>
public sealed class Channel
{
    private readonly object _gate = new();

    private Action<object>[] _subscribers = [];

    private (object Store, Func<object, Action> Enter)[] _stores = [];

    internal Channel(string name) => Name = name;

    /// <summary>The channel's name.</summary>
    public string Name { get; }

    /// <summary>Whether a subscriber or a bound store would see a message.</summary>
    public bool HasSubscribers => _subscribers.Length > 0 || _stores.Length > 0;

    /// <summary><c>channel.subscribe</c>.</summary>
    public void Subscribe(Action<object> subscriber)
    {
        ArgumentNullException.ThrowIfNull(subscriber);
        lock (_gate)
        {
            _subscribers = [.. _subscribers, subscriber];
        }
    }

    /// <summary><c>channel.unsubscribe</c>. Returns whether the subscriber was subscribed.</summary>
    public bool Unsubscribe(Action<object> subscriber)
    {
        lock (_gate)
        {
            var index = Array.IndexOf(_subscribers, subscriber);
            if (index < 0)
            {
                return false;
            }

            _subscribers = [.. _subscribers.Where((_, i) => i != index)];
            return true;
        }
    }

    /// <summary>
    /// <c>channel.bindStore</c>: while <see cref="RunStores"/> runs, <paramref name="store"/> holds what <paramref name="transform"/> returns
    /// for the message.
    /// </summary>
    public void BindStore<T>(AsyncLocal<T?> store, Func<object, T?> transform)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(transform);
        lock (_gate)
        {
            _stores = [.. _stores, (store, message =>
                {
                    var previous = store.Value;
                    store.Value = transform(message);
                    return () => store.Value = previous;
                })];
        }
    }

    /// <summary><c>channel.unbindStore</c>. Returns whether the store was bound.</summary>
    public bool UnbindStore<T>(AsyncLocal<T?> store)
    {
        lock (_gate)
        {
            if (!_stores.Any(bound => ReferenceEquals(bound.Store, store)))
            {
                return false;
            }

            _stores = [.. _stores.Where(bound => !ReferenceEquals(bound.Store, store))];
            return true;
        }
    }

    /// <summary>
    /// <c>channel.publish</c>. A subscriber that throws does not affect the publisher or the other subscribers, as in Node, where the error
    /// is reported out of band.
    /// </summary>
    public void Publish(object message)
    {
        foreach (var subscriber in _subscribers)
        {
            try
            {
                subscriber(message);
            }
#pragma warning disable CA1031 // A diagnostics subscriber must never affect the operation it observes.
            catch (Exception)
#pragma warning restore CA1031
            {
                // Diagnostics subscribers should never affect SDK behavior.
                continue;
            }
        }
    }

    /// <summary><c>channel.runStores</c>: binds the stores to the message, publishes it, and runs <paramref name="execute"/> inside them.</summary>
    public T RunStores<T>(object message, Func<T> execute)
    {
        var stores = _stores;
        if (stores.Length == 0)
        {
            Publish(message);
            return execute();
        }

        return RunStore(stores, 0, message, execute);
    }

    private T RunStore<T>(
        (object Store, Func<object, Action> Enter)[] stores,
        int index,
        object message,
        Func<T> execute
    )
    {
        if (index == stores.Length)
        {
            Publish(message);
            return execute();
        }

        var restore = stores[index].Enter(message);
        try
        {
            return RunStore(stores, index + 1, message, execute);
        }
        finally
        {
            restore();
        }
    }
}

/// <summary>
/// The five channels of a tracing channel (<c>diagnosticsChannel.TracingChannel</c>): <c>start</c>, <c>end</c>, <c>asyncStart</c>,
/// <c>asyncEnd</c> and <c>error</c>. Each is a <see cref="Channel"/> named <c>tracing:{name}:{event}</c>, as in Node.
/// </summary>
public sealed class TracingChannel
{
    internal TracingChannel(string name)
    {
        Start = new Channel($"tracing:{name}:start");
        End = new Channel($"tracing:{name}:end");
        AsyncStart = new Channel($"tracing:{name}:asyncStart");
        AsyncEnd = new Channel($"tracing:{name}:asyncEnd");
        Error = new Channel($"tracing:{name}:error");
    }

    /// <summary>Published when the operation starts.</summary>
    public Channel Start { get; }

    /// <summary>Published when the operation's synchronous part returns.</summary>
    public Channel End { get; }

    /// <summary>Published when the operation's task completes, before <see cref="AsyncEnd"/>.</summary>
    public Channel AsyncStart { get; }

    /// <summary>Published when the operation's task completes.</summary>
    public Channel AsyncEnd { get; }

    /// <summary>Published when the operation fails.</summary>
    public Channel Error { get; }

    /// <summary><c>tracingChannel.hasSubscribers</c>.</summary>
    public bool HasSubscribers =>
        Start.HasSubscribers
        || End.HasSubscribers
        || AsyncStart.HasSubscribers
        || AsyncEnd.HasSubscribers
        || Error.HasSubscribers;

    /// <summary><c>tracingChannel.subscribe</c>.</summary>
    public void Subscribe(TracingChannelSubscribers subscribers)
    {
        ArgumentNullException.ThrowIfNull(subscribers);
        if (subscribers.Start is not null)
        {
            Start.Subscribe(subscribers.Start);
        }

        if (subscribers.End is not null)
        {
            End.Subscribe(subscribers.End);
        }

        if (subscribers.AsyncStart is not null)
        {
            AsyncStart.Subscribe(subscribers.AsyncStart);
        }

        if (subscribers.AsyncEnd is not null)
        {
            AsyncEnd.Subscribe(subscribers.AsyncEnd);
        }

        if (subscribers.Error is not null)
        {
            Error.Subscribe(subscribers.Error);
        }
    }

    /// <summary><c>tracingChannel.unsubscribe</c>.</summary>
    public void Unsubscribe(TracingChannelSubscribers subscribers)
    {
        ArgumentNullException.ThrowIfNull(subscribers);
        if (subscribers.Start is not null)
        {
            Start.Unsubscribe(subscribers.Start);
        }

        if (subscribers.End is not null)
        {
            End.Unsubscribe(subscribers.End);
        }

        if (subscribers.AsyncStart is not null)
        {
            AsyncStart.Unsubscribe(subscribers.AsyncStart);
        }

        if (subscribers.AsyncEnd is not null)
        {
            AsyncEnd.Unsubscribe(subscribers.AsyncEnd);
        }

        if (subscribers.Error is not null)
        {
            Error.Unsubscribe(subscribers.Error);
        }
    }

    /// <summary>
    /// <c>tracingChannel.tracePromise</c>: runs <paramref name="execute"/> and publishes <c>start</c> and <c>end</c> around the call, then
    /// <c>error</c> (when it fails), <c>asyncStart</c> and <c>asyncEnd</c> when its task completes, with <see cref="TelemetryTracingChannelMessage.Result"/>
    /// or <see cref="TelemetryTracingChannelMessage.Error"/> set on <paramref name="message"/>. The task's own result or failure is returned.
    /// </summary>
    public Task<T> TracePromiseAsync<T>(Func<Task<T>> execute, TelemetryTracingChannelMessage message)
    {
        ArgumentNullException.ThrowIfNull(execute);
        ArgumentNullException.ThrowIfNull(message);
        if (!HasSubscribers)
        {
            return execute();
        }

        return Start.RunStores(message, () =>
        {
            try
            {
                return TraceCompletionAsync(execute(), message);
            }
            catch (Exception error)
            {
                message.Error = error;
                Error.Publish(message);
                throw;
            }
            finally
            {
                End.Publish(message);
            }
        });
    }

    private async Task<T> TraceCompletionAsync<T>(Task<T> task, TelemetryTracingChannelMessage message)
    {
        try
        {
            var result = await task.ConfigureAwait(false);
            message.Result = result;
            AsyncStart.Publish(message);
            AsyncEnd.Publish(message);
            return result;
        }
        catch (Exception error)
        {
            message.Error = error;
            Error.Publish(message);
            AsyncStart.Publish(message);
            AsyncEnd.Publish(message);
            throw;
        }
    }
}

/// <summary>The subscribers of a <see cref="TracingChannel"/> (<c>TracingChannelSubscribers</c>). Each one is optional.</summary>
public sealed class TracingChannelSubscribers
{
    /// <summary>Called when the operation starts.</summary>
    public Action<object>? Start { get; init; }

    /// <summary>Called when the operation's synchronous part returns.</summary>
    public Action<object>? End { get; init; }

    /// <summary>Called when the operation's task completes, before <see cref="AsyncEnd"/>.</summary>
    public Action<object>? AsyncStart { get; init; }

    /// <summary>Called when the operation's task completes.</summary>
    public Action<object>? AsyncEnd { get; init; }

    /// <summary>Called when the operation fails.</summary>
    public Action<object>? Error { get; init; }
}
