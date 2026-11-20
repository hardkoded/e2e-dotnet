// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Decision.Tests.ValidateDownloadUrl.ValidateDownloadUrlAssertions;

namespace E2E.Decision.Tests.ValidateDownloadUrl;

/// <summary>The AI SDK's <c>validate-download-url.test.ts</c>, <c>describe('validateDownloadUrl > malformed URLs')</c>.</summary>
public sealed class MalformedUrlsTests
{
    [Fact]
    public void Should_block_invalid_URLs()
    {
        AssertBlocked("not-a-url");
    }
}
