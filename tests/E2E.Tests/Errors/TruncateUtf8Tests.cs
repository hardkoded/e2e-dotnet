// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Internal;

namespace E2E.Tests.Errors;

/// <summary>Upstream's <c>describe('truncateUtf8')</c> (<c>tests/unit/errors.test.ts</c>).</summary>
public sealed class TruncateUtf8Tests
{
    [Fact]
    public void Never_splits_a_four_byte_code_point_and_honors_a_zero_budget()
    {
        Assert.Equal("a", TextRules.TruncateUtf8("a😀b", 4)); // 😀 is 4 bytes; 1 + 4 > 4
        Assert.Equal("a😀", TextRules.TruncateUtf8("a😀b", 5));
        Assert.Equal("", TextRules.TruncateUtf8("a😀b", 0));
    }
}
