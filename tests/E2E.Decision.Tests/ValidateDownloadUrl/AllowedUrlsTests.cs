// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Decision.Tests.ValidateDownloadUrl.ValidateDownloadUrlAssertions;

namespace E2E.Decision.Tests.ValidateDownloadUrl;

/// <summary>The AI SDK's <c>validate-download-url.test.ts</c>, <c>describe('validateDownloadUrl > allowed URLs')</c>.</summary>
public sealed class AllowedUrlsTests
{
    [Fact]
    public void Should_allow_https_URLs()
    {
        AssertAllowed("https://example.com/image.png");
    }

    [Fact]
    public void Should_allow_http_URLs()
    {
        AssertAllowed("http://example.com/image.png");
    }

    [Theory]
    [InlineData("https://internal.example.com/file")]
    [InlineData("https://service.internal.example.com/file")]
    [InlineData("https://service.notinternal/file")]
    [InlineData("https://example.com/service.internal")]
    public void Should_allow_URLs_without_an_internal_hostname_suffix(string url)
    {
        AssertAllowed(url);
    }

    [Fact]
    public void Should_allow_public_IP_addresses()
    {
        // 8.8.8.8 is a genuinely public, globally-routable address. The TEST-NET documentation ranges (192.0.2/24, 198.51.100/24, 203.0.113/24) are blocked.
        AssertAllowed("https://8.8.8.8/file");
    }

    [Fact]
    public void Should_allow_URLs_with_ports()
    {
        AssertAllowed("https://example.com:8080/file");
    }

    [Fact]
    public void Should_allow_data_URLs()
    {
        AssertAllowed("data:text/plain;base64,aGVsbG8=");
    }
}
