// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using E2E.Decision.Sdk;
using static E2E.Decision.Tests.Download.DownloadTestHelpers;

namespace E2E.Decision.Tests.Download;

/// <summary>The AI SDK's <c>download.test.ts</c>, <c>describe('download')</c>.</summary>
public sealed class DownloadTests
{
    [Fact]
    public async Task Should_download_data_successfully_and_match_expected_bytes()
    {
        byte[] expectedBytes = [1, 2, 3, 4, 5, 6, 7, 8];
        var fetch = new RecordingFetch(() => Ok(expectedBytes, "application/octet-stream"));

        var result = await Downloads.DownloadAsync(new Uri("http://example.com/file"), null, fetch.Fetch, default);

        Assert.NotNull(result);
        Assert.Equal(expectedBytes, result.Data);
        Assert.Equal("application/octet-stream", result.MediaType);
        var call = Assert.Single(fetch.Calls);
        Assert.Equal("http://example.com/file", call.Url);
        Assert.NotEmpty(call.HeaderNames);
    }

    // Upstream's fetch handles data: URLs itself; the port's default client (HttpClient) does not, so this needs the download to decode them.
    [Fact]
    public async Task Should_allow_inline_data_URLs()
    {
        var result = await Downloads.DownloadAsync(new Uri("data:text/plain;base64,aGVsbG8="), null, null, default);

        Assert.Equal("hello"u8.ToArray(), result.Data);
        Assert.Equal("text/plain", result.MediaType);
    }

    [Fact]
    public async Task Should_throw_DownloadError_when_response_is_not_ok()
    {
        var fetch = new RecordingFetch(() => new HttpResponseMessage(HttpStatusCode.NotFound));

        var error = await Assert.ThrowsAsync<DownloadException>(() =>
            Downloads.DownloadAsync(new Uri("http://example.com/file"), null, fetch.Fetch, default)
        );

        Assert.Equal(404, error.StatusCode);
        Assert.Equal("Not Found", error.StatusText);
    }

    [Fact]
    public async Task Should_throw_DownloadError_when_fetch_throws_an_error()
    {
        FetchFunction fetch = (_, _) => throw new HttpRequestException("Network error");

        await Assert.ThrowsAsync<DownloadException>(() =>
            Downloads.DownloadAsync(new Uri("http://example.com/file"), null, fetch, default)
        );
    }

    [Fact]
    public async Task Should_cancel_the_body_on_non_ok_response_prevents_socket_leak()
    {
        var body = new TrackedContent(new byte[10]);
        var fetch = new RecordingFetch(() => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = body });

        await Assert.ThrowsAsync<DownloadException>(() =>
            Downloads.DownloadAsync(new Uri("http://example.com/not-found"), null, fetch.Fetch, default)
        );

        Assert.True(body.IsDisposed);
    }

    [Fact]
    public async Task Should_cancel_the_body_when_Content_Length_exceeds_limit_prevents_socket_leak()
    {
        var body = new TrackedContent(new byte[10]);
        body.Headers.ContentLength = 3L * 1024 * 1024 * 1024;
        var fetch = new RecordingFetch(() => Ok([], "application/octet-stream", body));

        await Assert.ThrowsAsync<DownloadException>(() =>
            Downloads.DownloadAsync(new Uri("http://example.com/large"), null, fetch.Fetch, default)
        );

        Assert.True(body.IsDisposed);
    }

    [Fact]
    public async Task Should_abort_when_response_exceeds_default_size_limit()
    {
        var body = new ByteArrayContent(new byte[10]);
        body.Headers.ContentLength = 3L * 1024 * 1024 * 1024;
        var fetch = new RecordingFetch(() => Ok([], "application/octet-stream", body));

        var error = await Assert.ThrowsAsync<DownloadException>(() =>
            Downloads.DownloadAsync(new Uri("http://example.com/large"), null, fetch.Fetch, default)
        );

        Assert.Contains("exceeded maximum size", error.Message);
    }

    // Partial: upstream's fetch rejects with an AbortError and the download wraps it in a DownloadError. The port rethrows the caller's
    // cancellation as cancellation (a deliberate port rule), so this asserts that instead; the signal reaching fetch is checked.
    [Fact]
    public async Task Partial_should_pass_abortSignal_to_fetch()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var fetch = new RecordingFetch(() => throw new OperationCanceledException("The operation was aborted."));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Downloads.DownloadAsync(new Uri("http://example.com/file"), null, fetch.Fetch, cts.Token)
        );

        var call = Assert.Single(fetch.Calls);
        Assert.Equal("http://example.com/file", call.Url);
        Assert.True(call.Token.IsCancellationRequested);
    }
}
