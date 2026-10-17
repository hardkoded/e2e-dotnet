// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Encodings.Web;
using System.Text.Json;
using E2E.Cache;
using E2E.Engine;
using E2E.Internal;
using Recorder = E2E.Cache.TraceRecorder;

namespace E2E.Tests.TraceRecorder;

/// <summary>
/// The port's recorder keeps a plain action list: no recorded model, summaries, CSS selectors, positions, scroll spans, truncation
/// flag, action cap, secret names, or entry validation, and no hover, long-press, secondary-tap, drag, upload, or point verbs. So
/// these upstream its are not ported: "omits an oversized recorded provider", "omits an oversized recorded model", "omits blank
/// recording identity", "poisons a trace whose target is an unnamed twin no named row tells apart, and keeps one that a row does",
/// "masks a secret resolved after capture in the container the node sat within", "poisons the trace instead of bending a replay
/// input", "keeps the smallest coverage when folding scrolls, and none when a repeat lacks one", "truncates past the action cap
/// instead of dropping the flag", "rejects a stored check, upload, or drag that lost a field", and every it of "TraceRecorder:
/// bare-point taps". Parts of ported its that need one of those features are named on each test.
/// </summary>
public sealed class TraceRecorderTests
{
    private static readonly JsonSerializerOptions Relaxed = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static readonly SemanticNode UpgradeButton = new() { Ref = "n42", Role = "button", Name = "Upgrade", Text = "Upgrade", TestId = "upgrade-cta" };

    private static readonly SemanticNode PasswordField = new() { Ref = "n7", Role = "textbox", Name = "Password", InputPurpose = "password", States = new NodeStates { Secure = true } };

    private static CacheEntry Step => new() { Test = "tests/billing::upgrade", Engine = "web" };

    private static Recorder MakeRecorder(params Secret[] secrets)
    {
        var redactor = Redactor.For(secrets);
        return new Recorder(() => redactor);
    }

    private static CacheEntry? Finalize(Recorder recorder, string route = "/settings") => recorder.Finalize(Step, route, route, [], []);

    private static CacheEntry RoundTrip(CacheEntry entry)
    {
        var store = new FileStepCache(CoreTests.TempCache());
        store.Write("abc123", entry);
        return Assert.IsType<CacheEntry>(store.Read("abc123").Entry);
    }

    /// <summary>Partial port of "records durable descriptors and readable summaries": the port keeps no summary and no CSS selector.</summary>
    [Fact]
    public void Records_the_durable_descriptors_of_each_action()
    {
        var recorder = MakeRecorder();
        recorder.Record(new RecordedAction { Kind = "navigate", Url = "/billing" });
        recorder.Record("tap", UpgradeButton);
        recorder.Record("fill", UpgradeButton, value: "yearly");

        var entry = Finalize(recorder, "/settings");

        Assert.NotNull(entry);
        Assert.Equal("/settings", entry.Route);
        Assert.Equal(
            [("navigate", "/billing", null, null, null, null), ("tap", null, "button", "Upgrade", "upgrade-cta", null), ("fill", null, "button", "Upgrade", "upgrade-cta", "yearly")],
            entry.Actions.Select(action => (action.Kind, action.Url, action.Role, action.Name, action.TestId, action.Value)));
    }

    /// <summary>Partial port of "always finalizes into a trace the reader accepts": the reader is the file store, which has no entry validation beyond reading the entry back.</summary>
    [Fact]
    public void Always_finalizes_into_an_entry_the_store_reads_back()
    {
        var recorder = MakeRecorder();
        recorder.Record("tap", UpgradeButton);
        recorder.RecordGap("seed_cart");

        var read = RoundTrip(Finalize(recorder)!);

        Assert.Equal(2, read.Actions.Count);
    }

    /// <summary>Partial port of "records a secret fill by name only, with page strings redacted": the port records no model, no <c>secret</c> field, and has no truncation flag to leave unset.</summary>
    [Fact]
    public void Records_a_secret_fill_as_its_marker_with_page_strings_redacted()
    {
        var recorder = MakeRecorder(Secret.Create("member-password", "hunter2", "password"));
        recorder.Record("fill", PasswordField, value: "<secret:member-password>");
        recorder.Record("tap", new SemanticNode { Ref = "n42", Role = "button", Name = "Greeting hunter2", TestId = "upgrade-cta" });

        var entry = Finalize(recorder)!;

        var serialized = JsonSerializer.Serialize(entry, Relaxed);
        Assert.DoesNotContain("hunter2", serialized, StringComparison.Ordinal);
        Assert.Contains("<secret:member-password>", serialized, StringComparison.Ordinal);
        Assert.Equal("<secret:member-password>", entry.Actions[0].Value);
        Assert.Equal("Greeting <secret:member-password>", entry.Actions[1].Name);
    }

    [Fact]
    public void Masks_a_secret_resolved_after_the_node_was_captured()
    {
        var redactor = Redactor.None;
        var recorder = new Recorder(() => redactor);
        var field = new SemanticNode { Ref = "n7", Role = "textbox", Name = "Code token-2718-value", InputPurpose = "password" };
        // A provider-backed fill resolves the value only inside the action.
        redactor = Redactor.For([Secret.Create("token", "token-2718-value")]);
        recorder.Record("fill", field, value: "<secret:token>");

        var serialized = JsonSerializer.Serialize(Finalize(recorder), Relaxed);

        Assert.DoesNotContain("token-2718-value", serialized, StringComparison.Ordinal);
        Assert.Contains("Code <secret:token>", serialized, StringComparison.Ordinal);
    }

    /// <summary>Partial port of "folds consecutive identical scrolls into one action with a repeat count": the port has no action cap to stay under and no truncation flag to leave unset.</summary>
    [Fact]
    public void Folds_consecutive_identical_scrolls_into_a_repeat_count_that_reads_back()
    {
        var recorder = MakeRecorder();
        for (var i = 0; i < 5; i++)
        {
            recorder.RecordScroll(new RecordedAction { Kind = "scroll", Direction = "down" });
        }

        recorder.RecordScroll(new RecordedAction { Kind = "scroll", Direction = "up" });

        var entry = Finalize(recorder)!;

        Assert.Equal([("down", (int?)5), ("up", null)], entry.Actions.Select(action => (action.Direction, action.Times)));
        var read = RoundTrip(entry);
        Assert.Equal(("scroll", "down", (int?)5), (read.Actions[0].Kind, read.Actions[0].Direction, read.Actions[0].Times));
    }

    /// <summary>Partial port of "marks a gap that ends replay for a mutating project tool": the port's gap is an action of kind <c>tool</c> whose text is the tool name, and it has no summary.</summary>
    [Fact]
    public void Marks_a_gap_that_ends_replay_by_tool_name_for_a_mutating_project_tool()
    {
        var recorder = MakeRecorder();
        recorder.Record("tap", UpgradeButton);
        recorder.RecordGap("seed_cart");

        var entry = Finalize(recorder)!;

        Assert.Equal(("tool", "seed_cart"), (entry.Actions[1].Kind, entry.Actions[1].Text));
    }

    [Fact]
    public void Returns_no_trace_for_a_step_that_committed_nothing()
    {
        Assert.Null(Finalize(MakeRecorder()));
    }

    /// <summary>Partial port of "records every node verb, a state, a drag with its destination, files, and a back step, and reads them back": the port has no hover, long-press, secondary-tap, drag, upload, or point verb, and no summary.</summary>
    [Fact]
    public void Records_every_node_verb_a_state_and_a_back_step_and_reads_them_back()
    {
        var recorder = MakeRecorder();
        foreach (var kind in new[] { "tap", "doubleTap", "scrollTo", "uncheck", "check", "clear" })
        {
            recorder.Record(kind, UpgradeButton);
        }

        recorder.Record(new RecordedAction { Kind = "back" });
        var entry = Finalize(recorder)!;

        var read = RoundTrip(entry);

        Assert.Equal(["tap", "doubleTap", "scrollTo", "uncheck", "check", "clear", "back"], entry.Actions.Select(action => action.Kind));
        Assert.Equal(JsonSerializer.Serialize(entry.Actions, Relaxed), JsonSerializer.Serialize(read.Actions, Relaxed));
    }
}
