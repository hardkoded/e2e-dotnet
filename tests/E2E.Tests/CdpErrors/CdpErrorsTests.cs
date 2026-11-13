// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Tests.AttemptSession;
using Microsoft.Playwright;

namespace E2E.Tests.CdpErrors;

/// <summary>Ports <c>packages/web/tests/unit/cdp-errors.test.ts</c>: a failed connection with handshake headers keeps no header value.</summary>
public sealed class CdpErrorsTests
{
    [Theory]
    [InlineData("x")]
    [InlineData("Bearer synthetic-token")]
    [InlineData("Custom synthetic-token")]
    public async Task Keeps_authenticated_connection_errors_free_of_arbitrary_header_values(string value)
    {
        var cause = new TimeoutException($"WebSocket error: {value}, synthetic-token", new PlaywrightException(value));
        var playwright = Playwright(() => cause);
        var failure = await Assert.ThrowsAnyAsync<Exception>(() => BrowserConnection.ConnectCdpAsync(
            playwright,
            new CdpTarget { Endpoint = "wss://browser.example", Headers = new Dictionary<string, string> { ["Custom"] = value } },
            TimeSpan.FromSeconds(1),
            CancellationToken.None));
        Assert.IsType<TimeoutException>(failure);
        Assert.Equal("CDP connection failed; check the endpoint, handshake headers, and connection timeout", failure.Message);
        Assert.Null(failure.InnerException);
        Assert.DoesNotContain("synthetic-token", failure.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preserves_the_original_connection_failure_when_there_are_no_handshake_headers()
    {
        var cause = new PlaywrightException("connection refused");
        var playwright = Playwright(() => cause);
        var failure = await Assert.ThrowsAnyAsync<Exception>(() => BrowserConnection.ConnectCdpAsync(playwright, new CdpTarget { Endpoint = "wss://browser.example" }, TimeSpan.FromSeconds(1), CancellationToken.None));
        Assert.Same(cause, failure);
    }

    /// <summary>A Playwright whose Chromium refuses every CDP connection with <paramref name="failure"/>; stands in for upstream's mocked <c>connectOverCDP</c>.</summary>
    private static IPlaywright Playwright(Func<Exception> failure)
    {
        var (chromium, chromiumFake) = Fake<IBrowserType>.New();
        chromiumFake.Members["ConnectOverCDPAsync"] = _ => Task.FromException<IBrowser>(failure());
        var (playwright, playwrightFake) = Fake<IPlaywright>.New();
        playwrightFake.Members["get_Chromium"] = _ => chromium;
        return playwright;
    }
}
