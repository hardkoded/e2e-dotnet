// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using E2E.Decision.Sdk;
using static E2E.Decision.Tests.Download.DownloadTestHelpers;

namespace E2E.Decision.Tests.Download;

/// <summary>
/// The AI SDK's <c>download.test.ts</c>, <c>describe('download SSRF redirect protection')</c>. Not ported: "should let the browser follow
/// redirects natively on an opaque redirect" and "should fail closed on an opaque redirect outside the browser". .NET has no opaque
/// redirect response and no browser runtime, so the port has no such branch.
/// </summary>
public sealed class DownloadSsrfRedirectProtectionTests
{
    // Partial: upstream also asserts the fetch options carry no `dispatcher`; the port has no dispatcher.
    [Fact]
    public async Task Partial_uses_the_current_download_mock_after_mocks_are_reset()
    {
        var url = new Uri("https://download.invalid/file");
        var first = new RecordingFetch(() => Ok("first"u8.ToArray()));
        var firstResult = await Downloads.DownloadAsync(url, null, first.Fetch, default);
        Assert.Equal("first"u8.ToArray(), firstResult.Data);

        var second = new RecordingFetch(() => Ok("second"u8.ToArray()));
        var secondResult = await Downloads.DownloadAsync(url, null, second.Fetch, default);
        Assert.Equal("second"u8.ToArray(), secondResult.Data);
        Assert.Single(second.Calls);
        Assert.Single(first.Calls);
    }

    [Fact]
    public async Task Should_reject_a_redirect_to_a_private_IP_without_requesting_it()
    {
        var body = new TrackedContent("redirecting"u8.ToArray());
        var fetch = new RecordingFetch(() =>
            Redirect(HttpStatusCode.Found, "https://169.254.169.254/latest/meta-data/", body)
        );

        await Assert.ThrowsAsync<DownloadException>(() =>
            Downloads.DownloadAsync(new Uri("https://evil.com/redirect"), null, fetch.Fetch, default)
        );
        // The redirect target must never be requested.
        Assert.Single(fetch.Calls);
        // Body must be released so the open-redirect rejection does not leak the underlying socket.
        Assert.True(body.IsDisposed);
    }

    [Fact]
    public async Task Should_reject_a_redirect_to_localhost_without_requesting_it()
    {
        var body = new TrackedContent("redirecting"u8.ToArray());
        var fetch = new RecordingFetch(() =>
            Redirect(HttpStatusCode.TemporaryRedirect, "https://localhost:8080/admin", body)
        );

        await Assert.ThrowsAsync<DownloadException>(() =>
            Downloads.DownloadAsync(new Uri("https://evil.com/redirect"), null, fetch.Fetch, default)
        );
        Assert.Single(fetch.Calls);
        Assert.True(body.IsDisposed);
    }

    // Partial: upstream asserts `redirect: 'manual'` on the fetch options; a FetchFunction has no such option (it must not follow
    // redirects, and the default client does not).
    [Fact]
    public async Task Partial_should_follow_redirects_to_safe_URLs()
    {
        byte[] content = [1, 2, 3];
        var fetch = new RecordingFetch(
            () => Redirect(HttpStatusCode.Found, "https://cdn.example.com/image.png"),
            () => Ok(content, "image/png")
        );

        var result = await Downloads.DownloadAsync(
            new Uri("https://example.com/image.png"),
            null,
            fetch.Fetch,
            default
        );

        Assert.Equal(content, result.Data);
        Assert.Equal("image/png", result.MediaType);
        Assert.Equal(2, fetch.Calls.Count);
        Assert.Equal("https://cdn.example.com/image.png", fetch.Calls[1].Url);
    }
}
