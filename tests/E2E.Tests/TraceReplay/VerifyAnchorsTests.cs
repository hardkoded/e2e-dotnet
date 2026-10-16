// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using static E2E.Tests.StepCache.StepTraceSessionTests;

namespace E2E.Tests.TraceReplay;

/// <summary>
/// <c>verifyAnchors</c> is upstream's end check of a replay: every recorded end anchor must be present again. The port's cache
/// entry records the nodes that appeared and went (<c>Appeared</c> and <c>Gone</c>),
/// not end anchors, and its end check reads that delta from the replay's own screens inside the act. So the port has no
/// <c>verifyAnchors</c>, and its upstream <c>it</c>s are not ported: "holds trivially for a trace without anchors, without
/// observing", "holds when every anchor is present, counting an ambiguous match as presence", "fails when any anchor is
/// missing once the clock leaves no room to wait", "treats a surface that cannot be observed as a mismatch, never as a step
/// failure", and "waits out a slow effect before giving up". <c>StepTraceSessionTests</c> covers the delta check through a
/// replayed act.
/// </summary>
public sealed class VerifyAnchorsTests
{
    /// <summary>Partial: the hard stop is raised in the look of the act's end check, not in <c>verifyAnchors</c>.</summary>
    [Fact]
    public async Task Partial_Rethrows_a_runtime_hard_stop_raised_while_looking()
    {
        var directory = CoreTests.TempCache();
        var stopping = false;
        var replaying = false;
        DocumentWorld World() => new DocumentWorld().Map("/home", page =>
        {
            var status = page.Status("Idle");
            page.Button("Upgrade", () =>
            {
                status.Name = status.Text = "Upgraded";
                stopping = replaying;
            });
        });
        var recorded = await StepAsync(World(), new FileStepCache(directory), "/home", ctx => Expect.That(ctx.Screen.GetByRole("status")).ToBeVisibleAsync(), Script(ModelResponses.Tap("button", "Upgrade")));
        Assert.Null(recorded.Error);
        replaying = true;

        // The tap succeeds, and the look that checks its end state is the one that runs out of time.
        var replay = await StepAsync(
            World(),
            new FileStepCache(directory),
            "/home",
            null,
            Script(),
            wrap: engine => new LosingEngine(engine, () => stopping, () => stopping = false, () => new AgentException("STEP_TIMEOUT", "out of time")));

        Assert.Equal("STEP_TIMEOUT", Assert.IsType<AgentException>(replay.Error).Code);
    }
}
