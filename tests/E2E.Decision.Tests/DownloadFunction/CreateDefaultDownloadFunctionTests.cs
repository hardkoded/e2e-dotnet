// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;

namespace E2E.Decision.Tests.DownloadFunction;

/// <summary>The AI SDK's <c>download-function.test.ts</c>, <c>describe('createDefaultDownloadFunction')</c>.</summary>
public sealed class CreateDefaultDownloadFunctionTests
{
    // Partial: the port has no createDefaultDownloadFunction (it wraps upstream's single-request download, which the port lacks). The
    // closest seam is DecisionState.PrepareAsync, which hands the caller's token and the request to the download function.
    [Fact]
    public async Task Partial_should_pass_the_abort_signal_to_downloads()
    {
        using var cts = new CancellationTokenSource();
        CancellationToken? received = null;
        IReadOnlyList<DownloadRequest>? requests = null;
        Sdk.DownloadFunction download = (r, token) =>
        {
            received = token;
            requests = r;
            return Task.FromResult<IReadOnlyList<DownloadedFile?>>([new DownloadedFile([1, 2, 3], "text/plain")]);
        };

        await DecisionState.PrepareAsync(
            [new DecisionFileUrlPart("text/plain", new Uri("https://example.com/file.txt"))],
            download,
            cts.Token
        );

        Assert.Equal(cts.Token, received);
        var request = Assert.Single(requests!);
        Assert.Equal(new Uri("https://example.com/file.txt"), request.Url);
        Assert.False(request.IsUrlSupportedByModel);
    }
}
