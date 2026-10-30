// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using E2E.Engine;
using Microsoft.Playwright;

namespace E2E.Tests.CdpSelectors;

/// <summary>The CDP compatibility boundary must support Playwright's proxy channel and fail closed otherwise (upstream <c>cdp-selectors.test.ts</c>).</summary>
[Collection(BrowserCollection.Name)]
public sealed class CdpSelectorsTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Partial of upstream "refuses an unsupported context before any locator or capture can run, naming both": the message names
    /// only the secure-field masks, because the port's locators do not resolve through its selector engines.
    /// </summary>
    [Fact]
    public async Task Partial_refuses_an_unsupported_context_before_any_locator_or_capture_can_run()
    {
        var context = DispatchProxy.Create<IBrowserContext, UnsupportedContext>();
        var failure = await Assert.ThrowsAsync<EngineException>(() => E2E.Engine.CdpSelectors.RegisterCdpSelectorsAsync(context, Budget, CancellationToken.None));
        Assert.Equal("UNSUPPORTED_CAPABILITY", failure.Code);
        Assert.Matches("secure-field masks", failure.Message);
    }

    /// <summary>
    /// Partial of upstream "tolerates only the exact owned-name duplicates from a version that already seeded the context": the
    /// port reaches Playwright's real message channel, which a fake cannot stand in for, so this registers twice in a real default
    /// context and checks the second registration is tolerated. It cannot check that another name's duplicate still throws.
    /// </summary>
    [Fact]
    public async Task Partial_tolerates_an_owned_name_duplicate_in_a_context_that_already_has_the_engines()
    {
        await using var remote = await RemoteChrome.LaunchAsync();
        using var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.ConnectOverCDPAsync(remote.Endpoint);
        try
        {
            var context = browser.Contexts[0];
            await E2E.Engine.CdpSelectors.RegisterCdpSelectorsAsync(context, Budget, CancellationToken.None);
            await E2E.Engine.CdpSelectors.RegisterCdpSelectorsAsync(context, Budget, CancellationToken.None);
        }
        finally
        {
            await browser.CloseAsync();
        }
    }

    /// <summary>A context with no message channel to send through.</summary>
    public class UnsupportedContext : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => throw new NotSupportedException();
    }
}
