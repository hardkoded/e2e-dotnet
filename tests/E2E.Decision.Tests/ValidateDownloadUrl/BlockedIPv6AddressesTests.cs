// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Decision.Tests.ValidateDownloadUrl.ValidateDownloadUrlAssertions;

namespace E2E.Decision.Tests.ValidateDownloadUrl;

/// <summary>The AI SDK's <c>validate-download-url.test.ts</c>, <c>describe('validateDownloadUrl > blocked IPv6 addresses')</c>.</summary>
public sealed class BlockedIPv6AddressesTests
{
    [Fact]
    public void Should_block_1_loopback()
    {
        AssertBlocked("http://[::1]/file");
    }

    [Fact]
    public void Should_block_unspecified()
    {
        AssertBlocked("http://[::]/file");
    }

    [Fact]
    public void Should_block_fc00_7_unique_local()
    {
        AssertBlocked("http://[fc00::1]/file");
        AssertBlocked("http://[fd12::1]/file");
    }

    [Fact]
    public void Should_block_fe80_10_link_local()
    {
        AssertBlocked("http://[fe80::1]/file");
    }

    [Fact]
    public void Should_block_2001_db8_32_documentation()
    {
        AssertBlocked("http://[2001:db8::1]/file");
    }

    [Fact]
    public void Should_block_3fff_20_documentation_RFC_9637()
    {
        AssertBlocked("http://[3fff::1]/file");
        AssertBlocked("http://[3fff:fff::1]/file");
        // 3fff:1000::/20 is outside the documentation range.
        AssertAllowed("http://[3fff:1000::1]/file");
    }
}
