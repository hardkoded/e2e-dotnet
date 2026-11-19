// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Decision.Tests.ValidateDownloadUrl.ValidateDownloadUrlAssertions;

namespace E2E.Decision.Tests.ValidateDownloadUrl;

/// <summary>The AI SDK's <c>validate-download-url.test.ts</c>, <c>describe('validateDownloadUrl > blocked IPv4 addresses')</c>.</summary>
public sealed class BlockedIPv4AddressesTests
{
    [Fact]
    public void Should_block_127_0_0_1_loopback()
    {
        AssertBlocked("http://127.0.0.1/file");
    }

    [Fact]
    public void Should_block_127_x_x_x_range()
    {
        AssertBlocked("http://127.255.0.1/file");
    }

    [Fact]
    public void Should_block_10_x_x_x_private()
    {
        AssertBlocked("http://10.0.0.1/file");
    }

    [Fact]
    public void Should_block_172_16_x_x_through_172_31_x_x_private()
    {
        AssertBlocked("http://172.16.0.1/file");
        AssertBlocked("http://172.31.255.255/file");
    }

    [Fact]
    public void Should_allow_172_15_x_x_and_172_32_x_x_public()
    {
        AssertAllowed("http://172.15.0.1/file");
        AssertAllowed("http://172.32.0.1/file");
    }

    [Fact]
    public void Should_block_192_168_x_x_private()
    {
        AssertBlocked("http://192.168.1.1/file");
    }

    [Fact]
    public void Should_block_169_254_x_x_link_local_and_cloud_metadata()
    {
        AssertBlocked("http://169.254.169.254/latest/meta-data/");
    }

    [Fact]
    public void Should_block_0_0_0_0()
    {
        AssertBlocked("http://0.0.0.0/file");
    }

    [Fact]
    public void Should_block_224_0_0_0_4_multicast()
    {
        AssertBlocked("http://224.0.0.1/file");
        AssertBlocked("http://239.255.255.250/file");
    }

    [Fact]
    public void Should_block_the_TEST_NET_documentation_ranges()
    {
        // TEST-NET-1 (192.0.2.0/24)
        AssertBlocked("http://192.0.2.1/file");
        // TEST-NET-2 (198.51.100.0/24)
        AssertBlocked("http://198.51.100.1/file");
        // TEST-NET-3 (203.0.113.0/24)
        AssertBlocked("http://203.0.113.1/file");
        // Neighboring public /24s stay allowed.
        AssertAllowed("http://192.0.3.1/file");
        AssertAllowed("http://198.51.101.1/file");
        AssertAllowed("http://203.0.114.1/file");
    }
}
