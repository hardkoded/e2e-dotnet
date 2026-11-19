// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using static E2E.Decision.Tests.ValidateDownloadUrl.ValidateDownloadUrlAssertions;

namespace E2E.Decision.Tests.ValidateDownloadUrl;

/// <summary>The AI SDK's <c>validate-download-url.test.ts</c>, <c>describe('validateDownloadUrl > additional reserved IPv6 ranges')</c>.</summary>
public sealed class AdditionalReservedIPv6RangesTests
{
    [Fact]
    public void Should_block_fec0_10_site_local()
    {
        AssertBlocked("http://[fec0::1]/file");
    }

    [Fact]
    public void Should_block_ff00_8_multicast()
    {
        AssertBlocked("http://[ff02::1]/file");
    }
}
