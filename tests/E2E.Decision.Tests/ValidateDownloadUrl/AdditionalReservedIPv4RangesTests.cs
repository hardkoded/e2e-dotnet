// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Decision.Tests.ValidateDownloadUrl.ValidateDownloadUrlAssertions;

namespace E2E.Decision.Tests.ValidateDownloadUrl;

/// <summary>The AI SDK's <c>validate-download-url.test.ts</c>, <c>describe('validateDownloadUrl > additional reserved IPv4 ranges')</c>.</summary>
public sealed class AdditionalReservedIPv4RangesTests
{
    [Fact]
    public void Should_block_100_64_0_0_10_CGNAT()
    {
        AssertBlocked("http://100.64.0.1/file");
        AssertBlocked("http://100.127.255.255/file");
    }

    [Fact]
    public void Should_allow_100_63_x_x_and_100_128_x_x_public()
    {
        AssertAllowed("http://100.63.0.1/file");
        AssertAllowed("http://100.128.0.1/file");
    }

    [Fact]
    public void Should_block_198_18_0_0_15_benchmarking()
    {
        AssertBlocked("http://198.18.0.1/file");
        AssertBlocked("http://198.19.255.255/file");
    }

    [Fact]
    public void Should_block_192_0_0_0_24_IETF_protocol_assignments()
    {
        AssertBlocked("http://192.0.0.1/file");
    }

    [Fact]
    public void Should_block_240_0_0_0_4_reserved_and_the_broadcast_address()
    {
        AssertBlocked("http://240.0.0.1/file");
        AssertBlocked("http://255.255.255.255/file");
    }
}
