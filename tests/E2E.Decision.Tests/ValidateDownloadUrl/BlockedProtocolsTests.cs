// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Decision.Tests.ValidateDownloadUrl.ValidateDownloadUrlAssertions;

namespace E2E.Decision.Tests.ValidateDownloadUrl;

/// <summary>The AI SDK's <c>validate-download-url.test.ts</c>, <c>describe('validateDownloadUrl > blocked protocols')</c>.</summary>
public sealed class BlockedProtocolsTests
{
    [Fact]
    public void Should_block_file_URLs()
    {
        AssertBlocked("file:///etc/passwd");
    }

    [Fact]
    public void Should_block_ftp_URLs()
    {
        AssertBlocked("ftp://example.com/file");
    }

    [Fact]
    public void Should_block_javascript_URLs()
    {
        AssertBlocked("javascript:alert(1)");
    }
}
