// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Report;

namespace E2E.Cli.Tests.FormatTokens;

/// <summary>Upstream: packages/e2e/tests/unit/format-tokens.test.ts, describe('formatTokens').</summary>
public sealed class FormatTokensTests
{
    [Fact]
    public void Rounds_before_choosing_the_unit_so_the_value_carries_over()
    {
        Assert.Equal("999", Format.FormatTokens(999));
        Assert.Equal("1.0k", Format.FormatTokens(1_000));
        Assert.Equal("12.4k", Format.FormatTokens(12_400));
        Assert.Equal("999.9k", Format.FormatTokens(999_949));
        Assert.Equal("1.0M", Format.FormatTokens(999_950));
        Assert.Equal("2.5M", Format.FormatTokens(2_500_000));
    }
}
