// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Decision.Tests.ValidateDownloadUrl.ValidateDownloadUrlAssertions;

namespace E2E.Decision.Tests.ValidateDownloadUrl;

/// <summary>The AI SDK's <c>validate-download-url.test.ts</c>, <c>describe('validateDownloadUrl > non-dotted IPv4 notations')</c>.</summary>
public sealed class NonDottedIPv4NotationsTests
{
    [Fact]
    public void Should_block_decimal_2130706433_127_0_0_1()
    {
        AssertBlocked("http://2130706433/file");
    }

    [Fact]
    public void Should_block_hex_0x7f000001_127_0_0_1()
    {
        AssertBlocked("http://0x7f000001/file");
    }

    [Fact]
    public void Should_block_octal_0177_0_0_1_127_0_0_1()
    {
        AssertBlocked("http://0177.0.0.1/file");
    }
}
