// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Tests.Debug;

/// <summary>Upstream: packages/core/tests/unit/debug.test.ts, describe('formatMs').</summary>
public sealed class FormatMsTests
{
    [Fact]
    public void Rounds_before_choosing_ms_or_s_so_9999_6ms_reads_10_0s()
    {
        Assert.Equal("250ms", E2E.Internal.DebugTrace.FormatMs(250.4));
        Assert.Equal("9999ms", E2E.Internal.DebugTrace.FormatMs(9_999.4));
        Assert.Equal("10.0s", E2E.Internal.DebugTrace.FormatMs(9_999.6));
        Assert.Equal("12.3s", E2E.Internal.DebugTrace.FormatMs(12_340));
    }
}
