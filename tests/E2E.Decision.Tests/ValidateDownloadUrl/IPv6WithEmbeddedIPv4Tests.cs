// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Decision.Tests.ValidateDownloadUrl.ValidateDownloadUrlAssertions;

namespace E2E.Decision.Tests.ValidateDownloadUrl;

/// <summary>The AI SDK's <c>validate-download-url.test.ts</c>, <c>describe('validateDownloadUrl > IPv6 with embedded IPv4')</c>.</summary>
public sealed class IPv6WithEmbeddedIPv4Tests
{
    [Fact]
    public void Should_block_IPv4_compatible_127_0_0_1()
    {
        AssertBlocked("http://[::127.0.0.1]/file");
    }

    [Fact]
    public void Should_block_IPv4_translated_ffff_0_127_0_0_1()
    {
        AssertBlocked("http://[::ffff:0:127.0.0.1]/file");
    }

    [Fact]
    public void Should_block_NAT64_64_ff9b_127_0_0_1()
    {
        AssertBlocked("http://[64:ff9b::127.0.0.1]/file");
    }

    [Fact]
    public void Should_block_NAT64_64_ff9b_169_254_169_254_metadata()
    {
        AssertBlocked("http://[64:ff9b::169.254.169.254]/file");
    }

    [Fact]
    public void Should_block_NAT64_local_use_64_ff9b_1_169_254_169_254()
    {
        AssertBlocked("http://[64:ff9b:1::169.254.169.254]/file");
    }

    [Fact]
    public void Should_allow_NAT64_with_a_public_embedded_IPv4()
    {
        AssertAllowed("http://[64:ff9b::8.8.8.8]/file");
    }

    [Fact]
    public void Should_allow_a_regular_public_IPv6_address()
    {
        // 2606:4700::/32 (Cloudflare) is a genuinely public, globally-routable address. `2001:db8::/32` is the documentation range and is blocked.
        AssertAllowed("http://[2606:4700::1]/file");
    }
}
