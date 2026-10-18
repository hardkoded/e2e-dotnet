// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.Debug;

/// <summary>Upstream's <c>DebugTrace</c> describe.</summary>
public sealed class DebugTraceTests
{
    [Fact]
    public async Task Times_async_work_and_rethrows_failures()
    {
        var trace = new DebugTrace(true);
        var value = await trace.Time("ok", () => Task.FromResult(42));
        Assert.Equal(42, value);
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => trace.Time<int>("fails", () => Task.FromException<int>(new InvalidOperationException("boom"))));
        Assert.Equal("boom", failure.Message);
        var summary = trace.Summary();
        Assert.Contains("ok", summary, StringComparison.Ordinal);
        Assert.Contains("fails", summary, StringComparison.Ordinal);
    }
}
