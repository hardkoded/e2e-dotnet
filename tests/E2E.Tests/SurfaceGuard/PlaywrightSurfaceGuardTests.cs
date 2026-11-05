// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.SurfaceGuard;

/// <summary>
/// The surface's single operation entry (upstream <c>surface-guard.test.ts</c>, <c>PlaywrightSurface.guard</c>). The port's entry is the
/// web session's <c>RunAsync</c>, reached through <c>WebEngine.GuardWithLatchedAsync</c>; only this <c>it</c> is ported, because the port's
/// entry takes no latch-first or cancellation hooks the other three exercise (hardkoded/e2e-dotnet#1107).
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed class PlaywrightSurfaceGuardTests
{
    [Fact]
    public async Task Fails_an_operation_with_the_error_a_handler_latched_while_it_ran_which_caused_it()
    {
        using var site = await TinySite.StartAsync("<!DOCTYPE html><html><body></body></html>");
        var session = await WebEngineTests.StartAsync(site, new ScriptedModel(_ => ModelResponses.Done("passed", "unused")), cache: null);
        await WebEngineTests.RunAsync(session, async () =>
        {
            var refused = new TestException("INVALID_ARGUMENT", "route.fulfill has an invalid header name: \"bad name\"");
            var step = WebEngine.GuardWithLatchedAsync<int>(session.Engine, "navigation", refused, () => throw new InvalidOperationException("page.goto: net::ERR_FAILED"));
            var failure = await Assert.ThrowsAsync<TestException>(() => step);
            Assert.Same(refused, failure);
            Assert.Equal("ok", await WebEngine.GuardWithLatchedAsync(session.Engine, "step", null, () => Task.FromResult("ok")));
        });
    }
}
