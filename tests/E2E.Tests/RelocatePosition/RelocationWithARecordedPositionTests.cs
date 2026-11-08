// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Nodes;
using E2E.Cache;
using E2E.Engine;
using E2E.Internal;
using E2E.Tests.StepCache;
using static E2E.Tests.RelocatePosition.DescribePositionTests;
using Recorder = E2E.Cache.TraceRecorder;

namespace E2E.Tests.RelocatePosition;

/// <summary>
/// Ports the <c>relocation with a recorded position</c> describe of upstream's <c>relocate-position.test.ts</c>. The port records no action
/// summary, so the summary is <see cref="DefaultAgent.Describe"/>, the prose the hand-off and the <c>OnAction</c> event read.
/// </summary>
public sealed class RelocationWithARecordedPositionTests
{
    private static RecordedTarget Descriptor() => new() { Role = "button", Name = "Set up", Position = new TracePosition { Index = 1, Of = 3 } };

    internal static void AssertAmbiguous(RelocationResult result, params string[] candidates)
    {
        var failed = Assert.IsType<RelocationResult.Failed>(result);
        Assert.Equal(RelocationFailure.TargetAmbiguous, failed.Failure);
        Assert.Equal(candidates, failed.Candidates);
    }

    [Fact]
    public void Relocates_to_the_same_twin_when_the_live_screen_shows_the_same_number_of_them()
    {
        var result = Relocate.RelocateDescriptor(Descriptor(), Screen(Button("p"), Button("q"), Button("r")), Redactor.None);

        Assert.Equal(new RelocationResult.Found("q"), result);
    }

    [Fact]
    public void Diverges_as_ambiguous_when_a_twin_appeared_or_vanished()
    {
        AssertAmbiguous(Relocate.RelocateDescriptor(Descriptor(), Screen(Button("p"), Button("q")), Redactor.None), "p", "q");
        AssertAmbiguous(Relocate.RelocateDescriptor(Descriptor(), Screen(Button("p"), Button("q"), Button("r"), Button("s")), Redactor.None), "p", "q", "r", "s");
    }

    [Fact]
    public void Still_diverges_without_a_recorded_position()
    {
        var result = Relocate.RelocateDescriptor(new RecordedTarget { Role = "button", Name = "Set up" }, Screen(Button("p"), Button("q")), Redactor.None);

        AssertAmbiguous(result, "p", "q");
    }

    [Fact]
    public void Names_the_position_in_the_action_summary_and_carries_it_on_the_target()
    {
        var b = Button("b");
        var recorder = new Recorder(() => Redactor.None);

        var recorded = recorder.Record("tap", b, position: new TracePosition { Index = 1, Of = 2 });

        Assert.Equal("tap button \"Set up\" (2 of 2)", DefaultAgent.Describe(recorded));
        Assert.Equal((1, 2), (recorded.Position?.Index, recorded.Position?.Of));
    }

    [Fact]
    public async Task Survives_the_store_and_rejects_a_malformed_position()
    {
        var directory = CoreTests.TempCache();
        var store = new FileStepCache(directory);
        var recorder = new Recorder(() => Redactor.None);
        recorder.Record(new RecordedAction { Kind = "tap", Role = "button", Name = "Set up", Position = new TracePosition { Index = 1, Of = 3 } });
        store.Write(StepCacheHarness.Key, recorder.Finalize(StepCacheHarness.ExampleStep, "/integrations", "/integrations", [], [])!);

        var stored = Assert.IsType<CacheEntry>(store.Read(StepCacheHarness.Key).Entry);

        Assert.Equal((1, 3), (stored.Actions[0].Position?.Index, stored.Actions[0].Position?.Of));
        // The unbroken entry replays as a hit, so each rejection below is the position's.
        Assert.NotEqual("invalid-entry", await ReasonAsync(stored));
        var file = Directory.GetFiles(directory).Single();
        var document = File.ReadAllText(file);
        foreach (var position in new[] { "{\"index\":3,\"of\":3}", "{\"index\":-1,\"of\":3}", "{\"index\":0,\"of\":0}", "{\"index\":\"1\",\"of\":3}", "\"second\"" })
        {
            var broken = JsonNode.Parse(document)!;
            broken["actions"]![0]!["position"] = JsonNode.Parse(position);
            File.WriteAllText(file, broken.ToJsonString());

            var lookup = store.Read(StepCacheHarness.Key);

            Assert.Equal("invalid-entry", lookup.Entry is { } entry ? await ReasonAsync(entry) : lookup.Reason);
        }
    }

    // What a session reads of an entry a store returned: the step's cache reason.
    private static async Task<string?> ReasonAsync(CacheEntry entry)
    {
        var session = StepCacheHarness.MakeSession(StepCacheHarness.FakeContext(() => new CacheLookup { Entry = entry }), StepCacheHarness.MakeHost(["/"], [StepCacheHarness.Node("n1", "button", "Set up")]));
        await session.BeginAsync();
        return session.CacheInfo?.Reason;
    }
}
