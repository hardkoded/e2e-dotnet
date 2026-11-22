// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E;

/// <summary>
/// The marker of an <c>expect.poll</c> read, upstream's <c>pollRead</c> and <c>insidePollRead</c> (<c>expect/poll-scope.ts</c>). The marker flows
/// with the async call of the read, so every step the read calls sees it. Upstream's <c>PollScope</c> class, which ends a poll its phase did not
/// await, has no counterpart yet, nor has <c>toPass</c>, the other block the marker covers.
/// </summary>
internal static class PollScopes
{
    private static readonly AsyncLocal<bool> Reading = new();

    /// <summary>Runs one <c>expect.poll</c> read, marked so <see cref="InsidePollRead"/> sees it in every step the read calls (upstream's <c>pollRead</c>).</summary>
    public static async Task<T> PollReadAsync<T>(Func<Task<T>> run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var before = Reading.Value;
        Reading.Value = true;
        try
        {
            // Started here, so the marker is the one the read's own flow inherits; the caller's flow gets its value back.
            return await run().ConfigureAwait(false);
        }
        finally
        {
            Reading.Value = before;
        }
    }

    /// <summary>Whether this async flow is inside an <c>expect.poll</c> read (upstream's <c>insidePollRead</c>).</summary>
    public static bool InsidePollRead() => Reading.Value;
}
