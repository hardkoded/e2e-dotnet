// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Decision.Tests.ValidateDownloadUrl.ValidateDownloadUrlAssertions;

namespace E2E.Decision.Tests.ValidateDownloadUrl;

/// <summary>The AI SDK's <c>validate-download-url.test.ts</c>, <c>describe('validateDownloadUrl > IPv4-mapped IPv6 addresses')</c>.</summary>
public sealed class IPv4MappedIPv6AddressesTests
{
    [Fact]
    public void Should_block_ffff_127_0_0_1()
    {
        AssertBlocked("http://[::ffff:127.0.0.1]/file");
    }

    [Fact]
    public void Should_block_ffff_10_0_0_1()
    {
        AssertBlocked("http://[::ffff:10.0.0.1]/file");
    }

    [Fact]
    public void Should_block_ffff_169_254_169_254()
    {
        AssertBlocked("http://[::ffff:169.254.169.254]/file");
    }

    [Fact]
    public void Should_allow_ffff_with_public_IP()
    {
        AssertAllowed("http://[::ffff:8.8.8.8]/file");
    }
}
