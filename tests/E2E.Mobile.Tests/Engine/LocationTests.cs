// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Mobile.Tests.Screens;

namespace E2E.Mobile.Tests.Engine;

public sealed class LocationTests
{
    [Fact]
    public async Task Locates_against_a_fresh_snapshot_and_keeps_observation_ids_valid()
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        var about = await Observed(session, "About");
        var before = h.Fake.Calls.Count;
        var located = await session.LocateAsync(LocatorQuery.ForRole("button", null, new RoleOptions()), TimeSpan.FromSeconds(5), CancellationToken.None);
        var back = Assert.Single(located);
        Assert.Equal("Back", back.Name);
        await session.PerformAsync(back, new LocatorAction.Tap(), CancellationToken.None);
        await session.PerformAsync(about, new LocatorAction.Tap(), CancellationToken.None);
        JsonAssert.Calls(
            h.Fake.Calls.Skip(before).Where(call => call.Method == "interactions.press"),
            0,
            ("interactions.press", """{"ref":"@e3","settle":true,"settleQuietMs":150}"""),
            ("interactions.press", """{"ref":"@e4","settle":true,"settleQuietMs":150}"""));
    }
}
