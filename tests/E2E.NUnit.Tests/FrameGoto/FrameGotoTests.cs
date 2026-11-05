// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.NUnit.Tests.Helpers;

namespace E2E.NUnit.Tests.FrameGoto;

/// <summary>
/// <c>test.describe('frame goto')</c> of upstream's <c>apps/testbed/tests/frame-goto.e2e.ts</c> (Playwright's
/// <c>frame-goto.spec.ts</c>). The stalled <c>/frames/script.js</c> is a route that sends the request to <c>/never</c>. Not ported,
/// as upstream: should navigate subframes, should reject when frame detaches, should return matching responses (no
/// <c>frame.goto</c> and no Frame objects).
/// </summary>
public sealed class FrameGotoTests : PlaywrightTest
{
    [Test]
    public async Task Should_continue_after_client_redirect()
    {
        await Browser.RouteAsync("**/frames/script.js", route => route.ContinueAsync(new RouteContinueOverrides { Url = $"{PREFIX}/never" }));
        var url = $"{PREFIX}/frames/child-redirect.html";
        var error = await Record(() => Browser.GotoAsync(url, new GotoOptions { Timeout = TimeSpan.FromSeconds(5), WaitUntil = GotoWaitUntil.NetworkIdle }));
        Contain("timed out in browser.goto", error?.Message ?? "no error");
    }
}
