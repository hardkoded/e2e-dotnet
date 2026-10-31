// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using E2E.Engine;
using static E2E.Mobile.Tests.RawNodes;
using static E2E.Mobile.Tests.Screens;

namespace E2E.Mobile.Tests.Engine;

public sealed partial class SnapshotGenerationsTests
{
    [GeneratedRegex(@"~s(\d+)\z")]
    private static partial Regex PinnedGeneration();

    private static LocatorExpression TestId(string value) => LocatorExpression.For("testid", TextMatch.FromString(value), new TextMatchOptions(), nameof(value));

    /// <summary>
    /// agent-device's ref admission: every capture starts a new generation, and a ref pinned with <c>~s&lt;generation&gt;</c>
    /// is refused unless it names the newest.
    /// </summary>
    private static void Generations(Harness h, Func<IEnumerable<RawNode>>? nodes = null)
    {
        var generation = 6;
        h.Fake.Respond(DeviceMethods.CaptureSnapshot, _ =>
        {
            generation++;
            return FakeDeviceClient.SnapshotOf(nodes?.Invoke() ?? SettingsNodes, ("refsGeneration", JsonValue.Create(generation)));
        });
        void Admit(string reference)
        {
            if (PinnedGeneration().Match(reference) is { Success: true } pinned && int.Parse(pinned.Groups[1].Value) != generation)
            {
                throw new AgentDeviceError(
                    "COMMAND_FAILED",
                    $"Ref {reference} was minted from a superseded snapshot generation",
                    new Dictionary<string, object?> { ["reason"] = "ref_generation_mismatch", ["dispatched"] = "no" });
            }
        }

        h.Fake.Respond(DeviceMethods.InteractionsPress, options =>
        {
            Admit((string)options!["ref"]!);
            return new JsonObject();
        });
        h.Fake.Respond(DeviceMethods.InteractionsDrag, options =>
        {
            Admit((string)options!["source"]!);
            Admit((string)options["destination"]!);
            return new JsonObject();
        });
    }

    private static async Task<DeviceSession> OpenTestAttempt(Harness h)
    {
        var session = await OpenAttempt(h);
        session.Origin = ActionOrigin.Test;
        return session;
    }

    [Fact]
    public async Task Pins_every_ref_action_to_the_capture_that_issued_its_ref()
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        Generations(h);
        var root = await RootOf(session);
        var (about, toggle, back) = (Named(root, "About"), Named(root, "Airplane Mode"), Named(root, "Back"));
        var before = h.Fake.Calls.Count;
        await session.PerformAsync(about, new LocatorAction.Tap(), CancellationToken.None);
        await session.PerformAsync(toggle, new LocatorAction.Check(), CancellationToken.None);
        await session.DragToAsync(about, back);
        JsonAssert.Calls(
            h.Fake.Calls,
            before,
            ("interactions.press", """{"ref":"@e4~s7","settle":true,"settleQuietMs":150}"""),
            ("interactions.press", """{"ref":"@e6~s7","settle":true,"settleQuietMs":150}"""),
            ("interactions.drag", """{"source":"@e4~s7","destination":"@e3~s7"}"""));
    }

    [Fact]
    public async Task Refuses_a_ref_from_a_superseded_capture_as_stale_instead_of_acting_on_the_newer_one()
    {
        var h = Harness.Create();
        var session = await OpenAttempt(h);
        Generations(h);
        var about = await Observed(session, "About");
        await session.LocateAsync(TestId("BackButton"), TimeSpan.FromSeconds(5), CancellationToken.None);
        var stale = await Assert.ThrowsAsync<EngineException>(() => session.PerformAsync(about, new LocatorAction.Tap(), CancellationToken.None));
        Assert.Equal("NODE_STALE", stale.Code);
        Assert.True(stale.Retryable);
        JsonAssert.Args("""{"ref":"@e4~s7","settle":true,"settleQuietMs":150}""", h.Fake.LastArgs("interactions.press"));
    }

    [Fact]
    public async Task Drags_between_nodes_located_in_separate_captures_from_the_newest_one()
    {
        var h = Harness.Create(options => options with { TransitionMilliseconds = 0 });
        var session = await OpenTestAttempt(h);
        Generations(h);
        var about = Assert.Single(await session.LocateAsync(TestId("ABOUT"), TimeSpan.FromSeconds(5), CancellationToken.None));
        var back = Assert.Single(await session.LocateAsync(TestId("BackButton"), TimeSpan.FromSeconds(5), CancellationToken.None));
        await session.DragToAsync(about, back);
        JsonAssert.Args("""{"source":"@e4~s8","destination":"@e3~s8"}""", h.Fake.LastArgs("interactions.drag"));
    }

    [Fact]
    public async Task Pins_a_control_found_again_after_a_transition_to_the_capture_it_was_found_in()
    {
        var h = Harness.Create();
        var session = await OpenTestAttempt(h);
        Generations(h);
        var about = await Observed(session, "About");
        await session.PerformAsync(about, new LocatorAction.Tap(), CancellationToken.None);
        JsonAssert.Args("""{"ref":"@e4~s8"}""", h.Fake.LastArgs("interactions.press"));
    }

    [Fact]
    public async Task Drags_from_a_source_found_again_after_a_transition_to_a_target_located_before_it()
    {
        var h = Harness.Create();
        var session = await OpenTestAttempt(h);
        Generations(h);
        var about = Assert.Single(await session.LocateAsync(TestId("ABOUT"), TimeSpan.FromSeconds(5), CancellationToken.None));
        var back = Assert.Single(await session.LocateAsync(TestId("BackButton"), TimeSpan.FromSeconds(5), CancellationToken.None));
        await session.DragToAsync(about, back);

        // The launch leaves the source in transition, so it is found again in a third capture.
        JsonAssert.Args("""{"source":"@e4~s9","destination":"@e3~s9"}""", h.Fake.LastArgs("interactions.drag"));
    }

    [Fact]
    public async Task Refuses_a_drag_whose_source_a_newer_capture_lists_nowhere_it_was_before_sending_it()
    {
        var h = Harness.Create(options => options with { TransitionMilliseconds = 0 });
        var session = await OpenTestAttempt(h);
        var twin = Submit("@e11", 600, "About", "cell", "ABOUT");
        IEnumerable<RawNode> nodes = [.. SettingsNodes, twin];
        Generations(h, () => nodes);
        var about = (await session.LocateAsync(TestId("ABOUT"), TimeSpan.FromSeconds(5), CancellationToken.None))[0];
        nodes = [.. SettingsNodes.Where(node => node.Label != "About"), twin];
        var back = Assert.Single(await session.LocateAsync(TestId("BackButton"), TimeSpan.FromSeconds(5), CancellationToken.None));
        var stale = await Assert.ThrowsAsync<EngineException>(() => session.DragToAsync(about, back));
        Assert.Equal("NODE_STALE", stale.Code);
        Assert.True(stale.Retryable);
        Assert.DoesNotContain("interactions.drag", h.Fake.Methods());
    }
}
