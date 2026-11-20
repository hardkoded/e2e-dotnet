// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;

namespace E2E.Decision.Tests.Download;

/// <summary>The AI SDK's <c>download.test.ts</c>, <c>describe('download SSRF protection')</c>.</summary>
public sealed class DownloadSsrfProtectionTests
{
    [Fact]
    public async Task Should_reject_private_IPv4_addresses()
    {
        await Assert.ThrowsAsync<DownloadException>(() =>
            Downloads.DownloadAsync(new Uri("http://127.0.0.1/file"), null, null, default)
        );
        await Assert.ThrowsAsync<DownloadException>(() =>
            Downloads.DownloadAsync(new Uri("http://10.0.0.1/file"), null, null, default)
        );
        await Assert.ThrowsAsync<DownloadException>(() =>
            Downloads.DownloadAsync(new Uri("http://169.254.169.254/latest/meta-data/"), null, null, default)
        );
    }

    [Fact]
    public async Task Should_reject_localhost() =>
        await Assert.ThrowsAsync<DownloadException>(() =>
            Downloads.DownloadAsync(new Uri("http://localhost/file"), null, null, default)
        );
}
