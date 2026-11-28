// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Collections;

namespace E2E;

/// <summary>Context passed to a service's <see cref="Service.Start"/> function (upstream's <c>ServiceStartContext</c>, <c>types/services.ts</c>).</summary>
/// <param name="Signal">
/// Cancelled in these cases: startup is cancelled, or the service does not yield within <see cref="Service.StartupTimeout"/> (60 seconds by
/// default); when the services are no longer needed, the code after <c>yield</c> does not finish within <see cref="Service.ShutdownTimeout"/>
/// (10 seconds by default); and Ctrl+C is pressed a second time in <c>e2e run</c> or <c>e2e explore</c>. Upstream's <c>signal</c>, an
/// <c>AbortSignal</c>.
/// </param>
public sealed record ServiceStartContext(CancellationToken Signal);

/// <summary>
/// A dependency of the app under test (like an email API, an OAuth provider, or a database) started before the app commands and stopped at the
/// end of the run (upstream's <c>Service</c>, <c>types/services.ts</c>). One instance is shared across the run. Build one with
/// <see cref="Define.DefineService{TValue}"/>.
/// </summary>
public sealed class Service
{
    internal Service(
        Func<ServiceStartContext, IAsyncEnumerable<object>> start,
        TimeSpan? startupTimeout,
        TimeSpan? shutdownTimeout
    )
    {
        Start = start;
        StartupTimeout = startupTimeout;
        ShutdownTimeout = shutdownTimeout;
    }

    /// <summary>
    /// How long the service gets to yield (default 60 seconds). When the timeout passes, the service's signal is cancelled and startup fails
    /// with <c>SERVICE_START_FAILED</c>.
    /// </summary>
    public TimeSpan? StartupTimeout { get; }

    /// <summary>
    /// How long the code after <c>yield</c> gets to finish (default 10 seconds). When the timeout passes, the signal is cancelled and the
    /// runner stops waiting.
    /// </summary>
    public TimeSpan? ShutdownTimeout { get; }

    /// <summary>
    /// Starts the dependency and stops it when it is no longer needed: an async iterator that yields the service value once. The config and the
    /// tests receive it. Upstream's <c>start</c>, an async generator.
    /// </summary>
    public Func<ServiceStartContext, IAsyncEnumerable<object>> Start { get; }
}

/// <summary>
/// The values the configured services yielded, keyed by name (upstream's <c>ServiceValues</c>, <c>types/services.ts</c>): what the
/// <c>services</c> fixture holds and what an <c>app.command.env</c> function receives. A service the config sets to nothing has no entry.
/// </summary>
public sealed class ServiceValues : IReadOnlyDictionary<string, object>
{
    /// <summary>No services.</summary>
    public static ServiceValues Empty { get; } = new(new Dictionary<string, object>());

    private readonly Dictionary<string, object> _values;

    internal ServiceValues(Dictionary<string, object> values) => _values = values;

    /// <inheritdoc />
    public int Count => _values.Count;

    /// <inheritdoc />
    public IEnumerable<string> Keys => _values.Keys;

    /// <inheritdoc />
    public IEnumerable<object> Values => _values.Values;

    /// <inheritdoc />
    public object this[string key] => _values[key];

    /// <summary>The value service <paramref name="name"/> yielded, as the type its <c>DefineService</c> yields.</summary>
    /// <exception cref="KeyNotFoundException">No service has that name.</exception>
    public TValue Get<TValue>(string name)
        where TValue : class =>
        _values.TryGetValue(name, out var value)
            ? (TValue)value
            : throw new KeyNotFoundException(
                "no service named \""
                    + name
                    + "\"; services: "
                    + (_values.Count == 0 ? "none" : string.Join(", ", _values.Keys))
            );

    /// <inheritdoc />
    public bool ContainsKey(string key) => _values.ContainsKey(key);

    /// <inheritdoc />
    public bool TryGetValue(string key, out object value) => _values.TryGetValue(key, out value!);

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// The function that builds <c>app.command.env</c> from the services (upstream's <c>env</c> function, <c>types.ts</c> <c>CommandConfig</c>).
/// The run calls it once, after the services start and before any app command starts, and checks what it returns like an <c>env</c> object.
/// A null value is dropped, so the app keeps the variable it inherits. <paramref name="cancellationToken"/> is cancelled when the run is.
/// </summary>
public delegate Task<IReadOnlyDictionary<string, string?>> CommandEnv(
    ServiceValues services,
    CancellationToken cancellationToken
);

/// <summary>Config file helpers (upstream's <c>config/define.ts</c>).</summary>
public static class Define
{
    /// <summary>
    /// Defines a service (upstream's <c>defineService</c>). <paramref name="start"/> is an async iterator: the code before its
    /// <c>yield return</c> starts the dependency, the yielded object is the service value the config and the tests receive, and the code after
    /// it (a <c>finally</c> block) stops it. Yield exactly once.
    /// </summary>
    /// <param name="start">The async iterator.</param>
    /// <param name="startupTimeout">How long it gets to yield (default 60 seconds).</param>
    /// <param name="shutdownTimeout">How long the code after the yield gets to finish (default 10 seconds).</param>
    public static Service DefineService<TValue>(
        Func<ServiceStartContext, IAsyncEnumerable<TValue>> start,
        TimeSpan? startupTimeout = null,
        TimeSpan? shutdownTimeout = null
    )
        where TValue : class
    {
        ArgumentNullException.ThrowIfNull(start);
        return new Service(context => Widen(start(context)), startupTimeout, shutdownTimeout);
    }

    private static async IAsyncEnumerable<object> Widen<TValue>(IAsyncEnumerable<TValue> source)
        where TValue : class
    {
        await foreach (var value in source.ConfigureAwait(false))
        {
            yield return value;
        }
    }
}
