// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;

namespace E2E.Tests.SurfaceLifecycle;

/// <summary>
/// Tests of <c>surface-lifecycle.test.ts</c> outside any <c>describe</c>. Only the test the origin-scoping commit changes is here.
/// Not ported: "opens no session for an attempt the runner gave up on while its basic-auth password resolved" (its origin-scoped
/// credential assertion needs a mocked Playwright surface). Partial: the port has no <c>init</c>; the engine refuses at
/// <c>StartAsync</c>, which has the app's base URL where upstream's <c>init</c> has <c>app.origin</c>.
/// </summary>
public sealed class SurfaceLifecycleTests
{
    [Fact]
    public async Task Partial_Refuses_headers_or_basicAuth_from_a_runner_that_passes_no_app_origin()
    {
        foreach (var options in new[]
        {
            new WebEngineOptions { Headless = true, Headers = new Dictionary<string, string> { ["x-gate"] = "on" } },
            new WebEngineOptions { Headless = true, BasicAuth = new WebBasicAuth("ada", "pw") },
        })
        {
            var error = await Assert.ThrowsAsync<EngineException>(() => new WebEngine(options).StartAsync(new EngineStartOptions(), CancellationToken.None));
            Assert.Equal("INVALID_CONFIG", error.Code);
        }
    }
}
