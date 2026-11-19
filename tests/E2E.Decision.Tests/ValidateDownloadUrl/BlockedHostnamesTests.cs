// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Decision.Tests.ValidateDownloadUrl.ValidateDownloadUrlAssertions;

namespace E2E.Decision.Tests.ValidateDownloadUrl;

/// <summary>The AI SDK's <c>validate-download-url.test.ts</c>, <c>describe('validateDownloadUrl > blocked hostnames')</c>.</summary>
public sealed class BlockedHostnamesTests
{
    [Fact]
    public void Should_block_localhost()
    {
        AssertBlocked("http://localhost/file");
    }

    [Fact]
    public void Should_block_localhost_with_port()
    {
        AssertBlocked("http://localhost:3000/file");
    }

    [Fact]
    public void Should_block_local_domains()
    {
        AssertBlocked("http://myhost.local/file");
    }

    [Fact]
    public void Should_block_localhost_domains()
    {
        AssertBlocked("http://app.localhost/file");
    }

    [Theory]
    [InlineData("http://metadata.google.internal/file")]
    [InlineData("https://service.namespace.internal/file")]
    [InlineData("https://SERVICE.INTERNAL/file")]
    [InlineData("https://service.internal./file")]
    [InlineData("http://service.internal:8080/file")]
    public void Should_block_internal_hostnames(string url)
    {
        AssertBlocked(url);
    }
}
