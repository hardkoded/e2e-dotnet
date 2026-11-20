// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Decision.Sdk;

namespace E2E.Decision.Tests.DownloadFunction;

/// <summary>The AI SDK's <c>download-function.test.ts</c>, <c>describe('createDefaultDownloadFunction')</c>.</summary>
public sealed class CreateDefaultDownloadFunctionTests
{
    [Fact]
    public async Task Should_pass_the_abort_signal_to_downloads()
    {
        using var cts = new CancellationTokenSource();
        var calls = new List<(DownloadRequest Request, CancellationToken Token)>();
        DownloadFile download = (request, token) =>
        {
            calls.Add((request, token));
            return Task.FromResult(new DownloadedFile([1, 2, 3], "text/plain"));
        };

        await Sdk.CreateDefaultDownloadFunction.Create(download, cts.Token)(
            [new DownloadRequest(new Uri("https://example.com/file.txt"), false)],
            default
        );

        var call = Assert.Single(calls);
        Assert.Equal(new DownloadRequest(new Uri("https://example.com/file.txt"), false), call.Request);
        Assert.Equal(cts.Token, call.Token);
    }
}
