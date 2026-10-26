// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.ExceptionServices;

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
}

/// <summary>
/// Holds one error raised on a path nobody awaits (a native dialog nobody handled, a route handler that broke its contract) until
/// the next step enters the engine, which then fails with the real cause (upstream's <c>ErrorLatch</c>, <c>web/src/support.ts</c>).
/// </summary>
internal sealed class ErrorLatch
{
    private Exception? _pending;

    /// <summary>Latches an error; the first one wins until it is thrown.</summary>
    public void Latch(Exception error) => Interlocked.CompareExchange(ref _pending, error, null);

    /// <summary>Rethrows the latched error once, if any.</summary>
    public void ThrowPending()
    {
        if (Interlocked.Exchange(ref _pending, null) is { } pending)
        {
            ExceptionDispatchInfo.Throw(pending);
        }
    }
}
