// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk.Gateway.VercelOidc;

namespace E2E.Decision.Sdk.Gateway;

/// <summary>
/// The ambient Vercel request context. Maps to the <c>@vercel/request-context</c> object that Vercel stores on
/// <c>globalThis[Symbol.for('@vercel/request-context')]</c> and that <c>getContext</c> in <c>@vercel/oidc</c> reads.
/// A .NET host supplies it with <see cref="Use(IReadOnlyDictionary{string, string})"/>, for example from ASP.NET Core middleware.
/// </summary>
public static class VercelRequestContext
{
    private static readonly AsyncLocal<Scope?> s_current = new();

    internal static IReadOnlyDictionary<string, string?>? Headers => s_current.Value?.Headers;

    /// <summary>
    /// Makes <paramref name="headers"/> the request headers of the current async flow (for example <c>x-vercel-id</c> and
    /// <c>x-vercel-oidc-token</c>) until the returned scope is disposed. Header names are matched case-insensitively;
    /// names that differ only by case must carry the same value, otherwise <see cref="ArgumentException"/> is thrown.
    /// Disposing the scope also revokes the headers for work that captured them, and disposing twice does nothing.
    /// </summary>
    public static IDisposable Use(IReadOnlyDictionary<string, string?>? headers)
    {
        Dictionary<string, string?>? copy = null;
        if (headers is not null)
        {
            copy = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in headers)
            {
                if (copy.TryGetValue(pair.Key, out var existing) && existing != pair.Value)
                {
                    throw new ArgumentException(
                        $"The header '{pair.Key}' appears more than once with different values.",
                        nameof(headers)
                    );
                }

                copy[pair.Key] = pair.Value;
            }
        }

        var scope = new Scope(copy, s_current.Value);
        s_current.Value = scope;
        return scope;
    }

    /// <summary>
    /// Makes <paramref name="get"/> the provider of the request headers of the current async flow, until the returned
    /// scope is disposed. Maps to the <c>get</c> method of the <c>@vercel/request-context</c> object. The function runs
    /// on every lookup. A null <paramref name="get"/> is a context without a <c>get</c> method, and a function that
    /// returns null is a context without headers: both read as no headers. Header names match case-insensitively.
    /// </summary>
    public static IDisposable Use(Func<IReadOnlyDictionary<string, string?>?>? get)
    {
        var scope = new Scope(get, s_current.Value);
        s_current.Value = scope;
        return scope;
    }

    private sealed class Scope : IDisposable
    {
        private readonly Scope? _previous;
        private Func<IReadOnlyDictionary<string, string?>?>? _get;
        private int _disposed;

        public Scope(IReadOnlyDictionary<string, string?>? headers, Scope? previous)
            : this(headers is null ? null : () => headers, previous) { }

        public Scope(Func<IReadOnlyDictionary<string, string?>?>? get, Scope? previous)
        {
            _get = get;
            _previous = previous;
        }

        public IReadOnlyDictionary<string, string?>? Headers => _get?.Invoke();

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            _get = null;
            s_current.Value = _previous;
        }
    }
}

/// <summary>
/// Vercel runtime lookups. Maps to <c>vercel-environment.ts</c>, which re-exports <c>getVercelOidcToken</c> from
/// <c>@vercel/oidc</c>, ported in <c>VercelOidc/</c>.
/// </summary>
internal static class VercelEnvironment
{
    private static readonly AsyncLocal<Func<CancellationToken, Task<string>>?> s_oidcTokenOverride = new();

    /// <summary>Environment variable that carries the Vercel OIDC token.</summary>
    public const string OidcTokenEnvironmentVariable = "VERCEL_OIDC_TOKEN";

    /// <summary>Test seam that replaces <see cref="GetVercelOidcTokenAsync"/>. Null uses the environment variable.</summary>
    internal static Func<CancellationToken, Task<string>>? OidcTokenOverride
    {
        get => s_oidcTokenOverride.Value;
        set => s_oidcTokenOverride.Value = value;
    }

    /// <summary>
    /// Returns the Vercel OIDC token, refreshing it with the Vercel CLI credentials when it is missing or expired.
    /// Maps to <c>getVercelOidcToken</c>.
    /// </summary>
    public static Task<string> GetVercelOidcTokenAsync(CancellationToken cancellationToken)
    {
        var overrideProvider = OidcTokenOverride;
        if (overrideProvider is not null)
        {
            return overrideProvider(cancellationToken);
        }

        return VercelOidcToken.GetVercelOidcTokenAsync(null, cancellationToken);
    }

    /// <summary>
    /// Returns the <c>x-vercel-id</c> header of the Vercel request context, or null. Maps to <c>getVercelRequestId</c>.
    /// </summary>
    public static string? GetVercelRequestId()
    {
        return GetContextHeader("x-vercel-id");
    }

    /// <summary>Returns a header of the Vercel request context, or null. Maps to <c>getContext().headers?.[name]</c>.</summary>
    internal static string? GetContextHeader(string name)
    {
        var headers = VercelRequestContext.Headers;
        if (headers is null)
        {
            return null;
        }

        // A callback may return a case-sensitive dictionary; header names match case-insensitively, as in the dictionary overload.
        foreach (var pair in headers)
        {
            if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }
}
