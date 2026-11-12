// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using IdHelpers = E2E.Internal.Ids;

namespace E2E.Tests.Ids;

/// <summary>Upstream's <c>describe('uuidv7')</c> (<c>tests/unit/ids.test.ts</c>).</summary>
public sealed class Uuidv7Tests
{
    [Fact]
    public void Orders_by_timestamp()
    {
        var earlier = IdHelpers.Uuidv7(DateTimeOffset.FromUnixTimeMilliseconds(1_000_000));
        var later = IdHelpers.Uuidv7(DateTimeOffset.FromUnixTimeMilliseconds(2_000_000));
        Assert.True(string.CompareOrdinal(earlier, later) < 0);
    }
}
