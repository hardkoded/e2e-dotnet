// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;
using E2E.Decision.Tests.Download;
using static E2E.Decision.Tests.Download.DownloadTestHelpers;

namespace E2E.Decision.Tests.CreateDownload;

/// <summary>The AI SDK's <c>create-download.test.ts</c> (both `it`s sit outside any `describe`). The single-request form of <c>createDownload</c> is not ported.</summary>
public sealed class CreateDownloadTests
{
    // Partial: upstream asserts `redirect: 'manual'` on the fetch options; a FetchFunction has no such option. The signal is the caller's
    // token linked with the download's own bound, so the test checks that cancelling the caller's reaches fetch.
    [Fact]
    public async Task Partial_forwards_the_fetch_transport_abort_signal_and_download_size_limit()
    {
        using var cts = new CancellationTokenSource();
        var cancelledInFetch = false;
        var calls = new List<string>();
        FetchFunction fetch = (request, token) =>
        {
            calls.Add(request.RequestUri!.ToString());
            cts.Cancel();
            cancelledInFetch = token.IsCancellationRequested;
            return Task.FromResult(Ok("too large"u8.ToArray(), "text/plain"));
        };
        var download = Sdk.CreateDownload.Create(new CreateDownloadOptions { Fetch = fetch, MaxBytes = 3 });

        await Assert.ThrowsAsync<DownloadException>(() =>
            download([new DownloadRequest(new Uri("https://files.example.com/file"), false)], cts.Token)
        );

        Assert.Equal("https://files.example.com/file", Assert.Single(calls));
        Assert.True(cancelledInFetch);
    }

    [Fact]
    public async Task Acts_as_an_experimental_download_function_when_called_with_a_batch()
    {
        var fetch = new RecordingFetch(() => Ok("Hello World"u8.ToArray(), "text/plain"));
        var download = Sdk.CreateDownload.Create(new CreateDownloadOptions { Fetch = fetch.Fetch });

        var results = await download(
            [
                new DownloadRequest(new Uri("https://files.example.com/supported"), true),
                new DownloadRequest(new Uri("https://files.example.com/unsupported"), false),
            ],
            default
        );

        Assert.Equal(2, results.Count);
        Assert.Null(results[0]);
        Assert.Equal("Hello World"u8.ToArray(), results[1]!.Data);
        Assert.Equal("text/plain", results[1]!.MediaType);
        var call = Assert.Single(fetch.Calls);
        Assert.Equal("https://files.example.com/unsupported", call.Url);
    }
}
