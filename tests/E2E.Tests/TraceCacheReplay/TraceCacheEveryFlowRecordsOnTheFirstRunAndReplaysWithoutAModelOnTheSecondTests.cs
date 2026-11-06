// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.RegularExpressions;
using static E2E.Tests.TraceCacheReplay.TraceCacheFlows;

namespace E2E.Tests.TraceCacheReplay;

/// <summary>
/// Upstream's <c>describe('trace cache: every flow records on the first run and replays without a model on the second')</c> of
/// <c>trace-cache-replay.test.ts</c>. Upstream runs the flows through the runner and reads the report and the entry files. The port
/// has no runner and no report, so each flow is one session over one cache directory, and the steps are read from the session.
/// </summary>
[Collection(BrowserCollection.Name)]
public sealed partial class TraceCacheEveryFlowRecordsOnTheFirstRunAndReplaysWithoutAModelOnTheSecondTests(TraceCacheEveryFlowRecordsOnTheFirstRunAndReplaysWithoutAModelOnTheSecondTests.Runs runs)
    : IClassFixture<TraceCacheEveryFlowRecordsOnTheFirstRunAndReplaysWithoutAModelOnTheSecondTests.Runs>
{
    /// <summary>One entry per act step every flow makes.</summary>
    private static readonly int EntryCount = Flows.Sum(flow => flow.Actions.Count);

    // Partial: the port has no report, so "reports both in the schema" (assertValidReport) has nothing to check; both runs pass every flow.
    [Fact]
    public void Partial_passes_every_flow_on_both_runs_and_reports_both_in_the_schema()
    {
        Assert.Equal([], runs.Recorded.Where(flow => flow.Error is not null).Select(flow => flow.Title + ": " + flow.Error!.Message));
        Assert.Equal([], runs.Replayed.Where(flow => flow.Error is not null).Select(flow => flow.Title + ": " + flow.Error!.Message));
    }

    // Upstream's schemaVersion 'trace-1' is the entry's Schema 1 here.
    [Fact]
    public void Misses_every_step_on_the_first_run_and_writes_one_entry_per_act_step()
    {
        Assert.True(runs.RecordedModelCalls > 0);
        foreach (var flow in Flows)
        {
            var steps = ActSteps(runs.Recorded, flow.Title);
            Assert.Equal(flow.Actions.Count, steps.Count);
            foreach (var step in steps)
            {
                Assert.Equal(("missed", "no-entry", 0), (step.Cache?.Mode, step.Cache?.Reason, step.Cache?.ReplayedActions));
                Assert.True(step.Metrics?.ModelCalls > 0, flow.Title);
            }

            Assert.Equal(flow.Actions.Count, EntriesFor(runs.Directory, flow.Title).Count);
        }

        var entries = ReadEntries(runs.Directory);
        Assert.Equal(EntryCount, entries.Count);
        Assert.All(entries, item => Assert.Equal(1, item.Entry.Schema));
    }

    // Partial (#1281): upstream also expects each replayed step's explanation to contain "zero-turn" (the replay summary
    // "replayed N recorded action(s) zero-turn from the trace cache; recorded verdict: ..."). The port answers "Replayed recorded actions.".
    [Fact]
    public void Partial_replays_every_step_on_the_second_run_with_zero_model_calls_across_the_whole_run()
    {
        Assert.Equal(0, runs.ReplayModelCalls);
        foreach (var flow in Flows)
        {
            var steps = ActSteps(runs.Replayed, flow.Title);
            Assert.Equal(flow.Actions, steps.Select(step => step.Cache?.TotalActions ?? -1));
            foreach (var (step, index) in steps.Select((step, index) => (step, index)))
            {
                ExpectReplayed(step, flow.Actions[index]);
            }
        }

        Assert.Equal(EntryCount, ReadEntries(runs.Directory).Count);
    }

    [Fact]
    public void Never_rewrites_an_entry_the_cache_replayed_whole_the_file_keeps_its_bytes_and_its_modification_time()
    {
        Assert.Equal(runs.RecordedCounter, runs.CounterEntryFile());
    }

    [Fact]
    public void Keeps_a_unique_value_as_a_slot_in_the_typed_text_the_percent_encoded_path_and_the_anchors()
    {
        var entry = Assert.Single(EntriesFor(runs.Directory, "creates a company"));
        Assert.Equal(["fill", "tap"], entry.Actions.Select(action => action.Kind));
        Assert.Equal("{{param:/name}}", entry.Actions[0].Value);
        Assert.Equal("/companies/new", entry.Route);
        Assert.Equal("/companies/{{param:/name|uri}}", entry.EndRoute);
        Assert.Contains(entry.Appeared, anchor => anchor is { Role: "heading", Name: "{{param:/name}}" });
        Assert.DoesNotMatch(@"E2E \d+ Company", JsonSerializer.Serialize(entry));
    }

    [Fact]
    public void Spells_a_unique_value_in_a_query_string_the_way_the_form_submission_did()
    {
        var entry = Assert.Single(EntriesFor(runs.Directory, "searches"));
        Assert.Equal("/search?q={{param:/q|form}}", entry.EndRoute);
        Assert.Contains(entry.Appeared, anchor => anchor is { Role: "heading", Name: "Results for {{param:/q}}" });
        Assert.DoesNotMatch(@"E2E[ +]\d+[ +]widget", JsonSerializer.Serialize(entry));
    }

    [Fact]
    public void Records_no_anchor_made_of_a_result_count_a_millisecond_timing_or_a_bare_badge_number()
    {
        var entry = Assert.Single(EntriesFor(runs.Directory, "searches"));
        Assert.Contains(entry.Appeared, anchor => anchor is { Role: "status", Name: "Search state", Text: "done" });
        foreach (var anchor in entry.Appeared)
        {
            foreach (var text in new[] { anchor.Name, anchor.Text })
            {
                if (text is null)
                {
                    continue;
                }

                var shown = JsonSerializer.Serialize(anchor);
                Assert.False(Results().IsMatch(text), shown);
                Assert.False(Millis().IsMatch(text), shown);
                Assert.False(Bare().IsMatch(text), shown);
            }
        }
    }

    [Fact]
    public void Records_the_start_path_with_the_minted_id_the_recording_run_opened_a_replay_under_another_id_leaves_the_entry_as_recorded()
    {
        Assert.Equal("/records/1a2b3c4d5e6f", runs.RecordedArchive.Route);
        Assert.Contains(runs.RecordedArchive.Appeared, anchor => anchor is { Role: "status", Name: "Record state", Text: "archived" });
        var afterReplay = Assert.Single(EntriesFor(runs.Directory, "archives a record"));
        Assert.Equal(JsonSerializer.Serialize(runs.RecordedArchive), JsonSerializer.Serialize(afterReplay));
    }

    [Fact]
    public void Records_a_placeholder_named_field_unnamed_twins_by_their_place_in_their_group_and_same_named_buttons_by_position()
    {
        var entry = Assert.Single(EntriesFor(runs.Directory, "fills the twins form"));
        var actions = entry.Actions;
        Assert.Equal(("textbox", "Nickname", "Nickname"), (actions[0].Role, actions[0].Name, actions[0].Placeholder));
        Assert.Equal(("textbox", "Codenames", 0, 2), (actions[2].Role, actions[2].Within, actions[2].Position?.Index, actions[2].Position?.Of));
        Assert.Equal(("textbox", "Codenames", 1, 2), (actions[3].Role, actions[3].Within, actions[3].Position?.Index, actions[3].Position?.Of));
        foreach (var anonymous in new[] { actions[2], actions[3] })
        {
            Assert.Null(anonymous.Name);
            Assert.Null(anonymous.Placeholder);
        }

        Assert.Equal(("button", "Add", 1, 3), (actions[4].Role, actions[4].Name, actions[4].Position?.Index, actions[4].Position?.Of));
    }

    [Fact]
    public void Records_a_repeated_control_by_the_row_it_sits_in()
    {
        var entry = Assert.Single(EntriesFor(runs.Directory, "reserves offer B"));
        var action = entry.Actions[0];
        Assert.Equal(("tap", "button", "Reserve now", "Offer B"), (action.Kind, action.Role, action.Name, action.Within));
    }

    [Fact]
    public void Keeps_the_two_entries_of_the_mixed_test_apart_and_re_stages_both_after_the_replay()
    {
        var entries = EntriesFor(runs.Directory, "mixes deterministic and agent steps");
        var anchors = entries.Select(entry => entry.Appeared.FirstOrDefault(anchor => anchor.Name == "Counter")?.Text).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(["1", "3"], anchors);
    }

    private static List<StepRecord> ActSteps(List<FlowOutcome> outcomes, string title) => [.. outcomes.Single(outcome => outcome.Title == title).ActSteps];

    [GeneratedRegex(@"\d+ results")]
    private static partial Regex Results();

    [GeneratedRegex(@"\d+ms")]
    private static partial Regex Millis();

    [GeneratedRegex(@"^\d+\z")]
    private static partial Regex Bare();

    /// <summary>Both passes over the flows, sharing one cache: the first records, the second replays.</summary>
    public sealed class Runs : IAsyncLifetime
    {
        private FlowsApp? _app;

        public string Directory { get; } = Path.Combine(Path.GetTempPath(), "e2e-flows", Guid.NewGuid().ToString("n"));

        internal List<FlowOutcome> Recorded { get; private set; } = [];

        internal List<FlowOutcome> Replayed { get; private set; } = [];

        public int RecordedModelCalls { get; private set; }

        public int ReplayModelCalls { get; private set; }

        /// <summary>The archive entry as the first run wrote it; the second run opens the record under another id.</summary>
        internal CacheEntry RecordedArchive { get; private set; } = new();

        /// <summary>The counter entry's bytes and modification time as the first run wrote them.</summary>
        internal (string Bytes, DateTime Written) RecordedCounter { get; private set; }

        /// <summary>The counter flow's entry file: its bytes and modification time, to prove a replay left it alone.</summary>
        internal (string Bytes, DateTime Written) CounterEntryFile()
        {
            var file = Assert.Single(ReadEntries(Directory), item => item.Entry.Test == "increments the counter").File;
            return (File.ReadAllText(file), File.GetLastWriteTimeUtc(file));
        }

        public async Task InitializeAsync()
        {
            _app = await FlowsApp.StartAsync();
            var recorded = new Counter();
            Recorded = await RunAsync(_app, Directory, Flows, Variant.Record, recorded);
            RecordedModelCalls = recorded.Value;
            RecordedArchive = Assert.Single(EntriesFor(Directory, "archives a record"));
            RecordedCounter = CounterEntryFile();

            // The twins form moves every node, and the offers reverse, under the URL the recording saw.
            _app.Variant = "b";
            var replayed = new Counter();
            Replayed = await RunAsync(_app, Directory, Flows, Variant.Replay, replayed);
            ReplayModelCalls = replayed.Value;
        }

        public Task DisposeAsync()
        {
            _app?.Dispose();
            try
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
            }

            return Task.CompletedTask;
        }
    }
}
