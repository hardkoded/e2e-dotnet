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
    /// only the secure-field masks, because the port's locators do not resolve through its selector engines (the check that it
    /// names locators waits for that).
    /// </summary>
    [Fact]
    public async Task Partial_refuses_an_unsupported_context_before_any_locator_or_capture_can_run()
    {
        var context = DispatchProxy.Create<IBrowserContext, UnsupportedContext>();
        var failure = await Assert.ThrowsAsync<EngineException>(() => E2E.Engine.CdpSelectors.RegisterCdpSelectorsAsync(context, Budget, CancellationToken.None));
        Assert.Equal("UNSUPPORTED_CAPABILITY", failure.Code);
        Assert.Matches("secure-field masks", failure.Message);
    }

    /// <summary>Upstream "tolerates only the exact owned-name duplicates from a version that already seeded the context".</summary>
    [Fact]
    public async Task Tolerates_only_the_exact_owned_name_duplicates_from_a_version_that_already_seeded_the_context()
    {
        var channel = new FakeChannel();
        foreach (var (name, _) in SelectorEngines.All)
        {
            channel.Rejections.Enqueue($"\"{name}\" selector engine has been already registered");
        }

        channel.Rejections.Enqueue("\"other\" selector engine has been already registered");
        var context = DispatchProxy.Create<IBrowserContext, UnsupportedContext>();
        await E2E.Engine.CdpSelectors.RegisterCdpSelectorsAsync(context, channel, Budget, CancellationToken.None);
        var failure = await Assert.ThrowsAsync<PlaywrightException>(() => E2E.Engine.CdpSelectors.RegisterCdpSelectorsAsync(context, channel, Budget, CancellationToken.None));
        Assert.Contains("\"other\"", failure.Message);
    }

    /// <summary>The stand-in for the context's <c>registerSelectorEngine</c> channel; each queued message rejects one call.</summary>
    private sealed class FakeChannel : E2E.Engine.CdpSelectors.ISelectorChannel
    {
        public Queue<string> Rejections { get; } = new();

        public Task RegisterSelectorEngineAsync(string name, string source, bool contentScript, TimeSpan timeout, CancellationToken cancellationToken) =>
            Rejections.TryDequeue(out var message) ? Task.FromException(new PlaywrightException(message)) : Task.CompletedTask;
    }

    /// <summary>A context with no message channel to send through.</summary>
    public class UnsupportedContext : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => throw new NotSupportedException();
    }
}
