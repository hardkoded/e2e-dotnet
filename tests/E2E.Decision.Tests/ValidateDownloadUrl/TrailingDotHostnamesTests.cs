// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Decision.Tests.ValidateDownloadUrl.ValidateDownloadUrlAssertions;

namespace E2E.Decision.Tests.ValidateDownloadUrl;

/// <summary>The AI SDK's <c>validate-download-url.test.ts</c>, <c>describe('validateDownloadUrl > trailing-dot hostnames')</c>.</summary>
public sealed class TrailingDotHostnamesTests
{
    [Fact]
    public void Should_block_localhost_trailing_dot()
    {
        AssertBlocked("http://localhost./file");
    }

    [Fact]
    public void Should_block_local_trailing_dot()
    {
        AssertBlocked("http://myhost.local./file");
    }

    [Fact]
    public void Should_block_localhost_domain_trailing_dot()
    {
        AssertBlocked("http://app.localhost./file");
    }

    [Fact]
    public void Should_still_allow_public_hosts_with_a_trailing_dot()
    {
        AssertAllowed("https://example.com./image.png");
    }
}
