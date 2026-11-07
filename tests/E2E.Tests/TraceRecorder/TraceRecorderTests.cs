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
/// The port's recorder keeps a plain action list: no CSS selectors, secret names, or entry validation. So
/// upstream's "rejects a stored check or upload that lost a field" is not ported whole. The bare-point its are in
/// <see cref="TraceRecorderBarePointTapsTests"/> and <see cref="TraceRecorderBarePointDragsTests"/>. The recorded model is one string, the model's <see cref="IAgentModel.Name"/>, where upstream's is a provider
/// and a model: its two oversized cases and its blank cases are one theory each. Parts of ported its that need one of those features are named on each test.
/// </summary>
public sealed class TraceRecorderTests
{
    private static readonly JsonSerializerOptions Relaxed = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static readonly SemanticNode UpgradeButton = new() { Ref = "n42", Role = "button", Name = "Upgrade", Text = "Upgrade", TestId = "upgrade-cta" };

    private static readonly SemanticNode PasswordField = new() { Ref = "n7", Role = "textbox", Name = "Password", InputPurpose = "password", States = new NodeStates { Secure = true } };

    private static CacheEntry Step => new() { Test = "tests/billing::upgrade", Engine = "web" };

    internal static Recorder MakeRecorder(params Secret[] secrets)
    {
        var redactor = Redactor.For(secrets);
        return new Recorder(() => redactor);
    }

    /// <summary>Upstream's <c>makeRecorder({ maxActions })</c>.</summary>
    internal static Recorder MakeRecorder(int maxActions, params Secret[] secrets)
    {
        var redactor = Redactor.For(secrets);
        return new Recorder(() => redactor, maxActions: maxActions);
    }

    internal static CacheEntry? Finalize(Recorder recorder, string route = "/settings") => recorder.Finalize(Step, route, route, [], []);

    internal static CacheEntry RoundTrip(CacheEntry entry)
    {
        var store = new FileStepCache(CoreTests.TempCache());
        store.Write("abc123", entry);
        return Assert.IsType<CacheEntry>(store.Read("abc123").Entry);
    }

    /// <summary>Partial port of "records durable descriptors and readable summaries": the port keeps no CSS selector.</summary>
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
        Assert.Null(entry.Truncated);
        Assert.Equal(
            [("navigate", "/billing", null, null, null, null), ("tap", null, "button", "Upgrade", "upgrade-cta", null), ("fill", null, "button", "Upgrade", "upgrade-cta", "yearly")],
            entry.Actions.Select(action => (action.Kind, action.Url, action.Role, action.Name, action.TestId, action.Value)));
        Assert.Equal(
            ["navigate to \"/billing\"", "tap button \"Upgrade\"", "type \"yearly\" into button \"Upgrade\""],
            entry.Actions.Select(action => action.Summary));
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

    /// <summary>Partial port of "records a secret fill by name only, with page strings redacted": the port records no <c>secret</c> field.</summary>
    [Fact]
    public void Records_a_secret_fill_as_its_marker_with_page_strings_redacted()
    {
        var recorder = MakeRecorder(Secret.Create("member-password", "hunter2", "password"));
        recorder.Record("fill", PasswordField, value: "<secret:member-password>");
        recorder.Record("tap", new SemanticNode { Ref = "n42", Role = "button", Name = "Greeting hunter2", TestId = "upgrade-cta" });

        var entry = recorder.Finalize(Step, "/settings", "/settings", [], [], recordedBy: "example/hunter2")!;

        var serialized = JsonSerializer.Serialize(entry, Relaxed);
        Assert.DoesNotContain("hunter2", serialized, StringComparison.Ordinal);
        Assert.Contains("<secret:member-password>", serialized, StringComparison.Ordinal);
        Assert.Equal("<secret:member-password>", entry.Actions[0].Value);
        Assert.Equal("Greeting <secret:member-password>", entry.Actions[1].Name);
        Assert.Equal("fill secret \"member-password\" into textbox \"Password\"", entry.Actions[0].Summary);
        Assert.Equal("example/<secret:member-password>", entry.RecordedBy);
        // Descriptor redaction alone never poisons the trace.
        Assert.Null(entry.Truncated);
    }

    [Fact]
    public void Masks_a_secret_resolved_after_capture_in_the_container_the_node_sat_within()
    {
        var recorder = MakeRecorder(Secret.Create("token", "token-2718-value", "generic-secret"));
        recorder.Record("tap", UpgradeButton, within: "Row token-2718-value");

        var entry = Finalize(recorder)!;

        var serialized = JsonSerializer.Serialize(entry, Relaxed);
        Assert.DoesNotContain("token-2718-value", serialized, StringComparison.Ordinal);
        Assert.Contains("Row <secret:token>", serialized, StringComparison.Ordinal);
    }

    /// <summary>Partial port of "omits an oversized recorded provider" and "omits an oversized recorded model": the port records one model name.</summary>
    [Fact]
    public void Omits_an_oversized_recorded_model_and_keeps_one_at_the_limit()
    {
        var recorder = MakeRecorder();
        recorder.Record("tap", UpgradeButton);

        var atLimit = recorder.Finalize(Step, "/settings", "/settings", [], [], recordedBy: new string('x', 300));
        var oversized = recorder.Finalize(Step, "/settings", "/settings", [], [], recordedBy: new string('x', 301));

        Assert.Equal(new string('x', 300), atLimit?.RecordedBy);
        Assert.NotNull(oversized);
        Assert.Null(oversized.RecordedBy);
    }

    /// <summary>Partial port of "omits blank recording identity": the port records one model name.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Omits_a_blank_recording_identity(string recordedBy)
    {
        var recorder = MakeRecorder();
        recorder.Record("tap", UpgradeButton);

        Assert.Null(recorder.Finalize(Step, "/settings", "/settings", [], [], recordedBy: recordedBy)?.RecordedBy);
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

    [Fact]
    public void Folds_consecutive_identical_scrolls_into_a_repeat_count_that_reads_back()
    {
        var recorder = MakeRecorder(3);
        for (var i = 0; i < 5; i++)
        {
            recorder.RecordScroll(new RecordedAction { Kind = "scroll", Direction = "down" });
        }

        recorder.RecordScroll(new RecordedAction { Kind = "scroll", Direction = "up" });

        var entry = Finalize(recorder)!;

        Assert.Equal([("down", (int?)5), ("up", null)], entry.Actions.Select(action => (action.Direction, action.Times)));
        Assert.Null(entry.Truncated);
        var read = RoundTrip(entry);
        Assert.Equal(("scroll", "down", (int?)5), (read.Actions[0].Kind, read.Actions[0].Direction, read.Actions[0].Times));
    }

    [Fact]
    public void Keeps_the_smallest_coverage_when_folding_scrolls_and_none_when_a_repeat_lacks_one()
    {
        var recorder = MakeRecorder();
        recorder.RecordScroll(new RecordedAction { Kind = "scroll", Direction = "down", Role = "group", Name = "Rows", Spans = 0.6 });
        recorder.RecordScroll(new RecordedAction { Kind = "scroll", Direction = "down", Role = "group", Name = "Rows", Spans = 0.4 });
        var folded = Finalize(recorder)!.Actions[0];
        Assert.Equal(("scroll", 2, 0.4), (folded.Kind, folded.Times, folded.Spans));

        var paged = MakeRecorder();
        paged.Record(new RecordedAction { Kind = "scrollUntil", Text = "Row 4322", Direction = "down", Role = "group", Name = "Rows", Spans = 0.6, Screens = 7 });
        paged.Record(new RecordedAction { Kind = "scrollUntil", Text = "Row 12", Direction = "up", Screens = 1 });
        var entry = Finalize(paged)!;
        Assert.Equal(["scroll down on group \"Rows\" until \"Row 4322\" shows (7 screens)", "scroll up until \"Row 12\" shows (1 screen)"], entry.Actions.Select(action => action.Summary));
        Assert.Equal([("Row 4322", "down", (double?)0.6, "group"), ("Row 12", "up", null, null)], entry.Actions.Select(action => (action.Text, action.Direction, action.Spans, action.Role)));
        Assert.Equal(entry.Actions.Select(action => action.Spans), RoundTrip(entry).Actions.Select(action => action.Spans));
        Assert.Equal(entry.Actions.Select(action => action.Summary), RoundTrip(entry).Actions.Select(action => action.Summary));

        var mixed = MakeRecorder();
        mixed.RecordScroll(new RecordedAction { Kind = "scroll", Direction = "down", Role = "group", Name = "Rows", Spans = 0.6 });
        mixed.RecordScroll(new RecordedAction { Kind = "scroll", Direction = "down", Role = "group", Name = "Rows" });
        var unfolded = Finalize(mixed)!.Actions[0];
        Assert.Equal(2, unfolded.Times);
        Assert.Null(unfolded.Spans);
    }

    /// <summary>Partial port of "marks a gap that ends replay for a mutating project tool": the port's gap is an action of kind <c>tool</c> whose text is the tool name, where upstream's holds only a summary.</summary>
    [Fact]
    public void Marks_a_gap_that_ends_replay_by_tool_name_for_a_mutating_project_tool()
    {
        var recorder = MakeRecorder();
        recorder.Record("tap", UpgradeButton);
        recorder.RecordGap("seed_cart");

        var entry = Finalize(recorder)!;

        Assert.Equal(("tool", "seed_cart", "tool seed_cart"), (entry.Actions[1].Kind, entry.Actions[1].Text, entry.Actions[1].Summary));
    }

    [Fact]
    public void Poisons_a_trace_whose_target_is_an_unnamed_twin_no_named_row_tells_apart_and_keeps_one_that_a_row_does()
    {
        var icon = new SemanticNode { Ref = "i", Role = "button" };
        var orphan = MakeRecorder();
        orphan.Record("tap", icon, position: new TracePosition { Index = 1, Of = 2 });
        Assert.True(Finalize(orphan)?.Truncated);

        var rowed = MakeRecorder();
        rowed.Record("tap", icon, within: "Alpha", position: new TracePosition { Index = 0, Of = 1 });
        Assert.Null(Finalize(rowed)?.Truncated);
    }

    /// <summary>Port of "poisons the trace instead of bending a replay input". Difference: the port's typed value is a <c>fill</c> where upstream's is a <c>type</c>.</summary>
    [Fact]
    public void Poisons_the_trace_instead_of_bending_a_replay_input()
    {
        // A typed value carrying a registered secret would be altered by redaction; a value over the cap cannot be stored whole. Both poison.
        var leaking = MakeRecorder(Secret.Create("pw", "hunter2", "password"));
        leaking.Record("fill", UpgradeButton, value: "say hunter2");
        var leaked = Finalize(leaking);
        Assert.True(leaked?.Truncated);
        Assert.DoesNotContain("hunter2", JsonSerializer.Serialize(leaked, Relaxed), StringComparison.Ordinal);

        var oversized = MakeRecorder();
        oversized.Record(new RecordedAction { Kind = "navigate", Url = "/callback?token=" + new string('x', Trace.MaxTraceInputChars) });
        Assert.True(Finalize(oversized)?.Truncated);

        // Every other replay input, and a location, is kept or poisoned the same way (upstream's one `verbatim`).
        Action<Recorder>[] inputs =
        [
            recorder => recorder.Record("press", UpgradeButton, key: "hunter2"),
            recorder => recorder.Record("select", UpgradeButton, value: "hunter2"),
            recorder => recorder.Record(new RecordedAction { Kind = "upload", Role = "button", Name = "Upgrade", Paths = ["fixtures/hunter2.txt"] }),
            recorder => recorder.Record(new RecordedAction { Kind = "scrollUntil", Text = "hunter2", Direction = "down" }),
            recorder => recorder.Record(new RecordedAction { Kind = "typeText", Value = "hunter2", Replace = false }),
            recorder => recorder.Record(new RecordedAction { Kind = "pressKey", Key = "hunter2" }),
        ];
        foreach (var record in inputs)
        {
            var other = MakeRecorder(Secret.Create("pw", "hunter2", "password"));
            record(other);
            var poisoned = Finalize(other);
            Assert.True(poisoned?.Truncated);
            Assert.DoesNotContain("hunter2", JsonSerializer.Serialize(poisoned, Relaxed), StringComparison.Ordinal);
        }

        var located = MakeRecorder(Secret.Create("pw", "hunter2", "password"));
        located.Record("tap", UpgradeButton);
        var path = located.Finalize(Step, "/pay?token=hunter2", "/settings", [], []);
        Assert.True(path?.Truncated);
        Assert.DoesNotContain("hunter2", JsonSerializer.Serialize(path, Relaxed), StringComparison.Ordinal);
    }

    /// <summary>A secret fill holds its marker, a name, so it never poisons the trace, even when the secret's value is a piece of the marker. No upstream <c>it</c>: upstream's <c>typeSecret</c> holds the name in its own field.</summary>
    [Fact]
    public void Keeps_a_secret_fill_marker_without_poisoning_the_trace()
    {
        var recorder = MakeRecorder(Secret.Create("password", "secret", "password"));
        recorder.Record("fill", PasswordField, value: "<secret:password>");

        var entry = Finalize(recorder)!;

        Assert.Null(entry.Truncated);
        Assert.Equal("<secret:password>", entry.Actions[0].Value);
    }

    [Fact]
    public void Truncates_past_the_action_cap_instead_of_dropping_the_flag()
    {
        var recorder = MakeRecorder(2);
        recorder.Record("tap", UpgradeButton);
        recorder.Record("tap", UpgradeButton);
        recorder.Record("tap", UpgradeButton);

        var entry = Finalize(recorder);

        Assert.Equal(2, entry?.Actions.Count);
        Assert.True(entry?.Truncated);
    }

    [Fact]
    public void Returns_no_trace_for_a_step_that_committed_nothing()
    {
        Assert.Null(Finalize(MakeRecorder()));
    }

    [Fact]
    public void Records_every_node_verb_a_state_a_drag_with_its_destination_files_and_a_back_step_and_reads_them_back()
    {
        var recorder = MakeRecorder();
        foreach (var kind in new[] { "hover", "doubleTap", "longPress", "secondaryTap", "scrollTo", "uncheck" })
        {
            recorder.Record(kind, UpgradeButton);
        }

        var drag = Recorder.ActionOf("drag", Recorder.DescribeTarget(UpgradeButton, Redactor.None));
        drag.Destination = Recorder.DescribeTarget(new SemanticNode { Ref = "e9", Role = "region", Name = "Done column" }, Redactor.None, "Board");
        recorder.Record(drag);
        var upload = Recorder.ActionOf("upload", Recorder.DescribeTarget(UpgradeButton, Redactor.None));
        upload.Paths = ["fixtures/a.txt", "fixtures/b.txt"];
        recorder.Record(upload);
        recorder.Record(new RecordedAction { Kind = "back" });
        recorder.Record(new RecordedAction { Kind = "resize", Size = new TraceViewport(390, 844) });
        recorder.Record("hoverAt", new PlacedPoint(new Point(10, 20)), new TraceViewport(1280, 720));
        var entry = Finalize(recorder, "/board")!;

        var read = RoundTrip(entry);

        Assert.Null(entry.Truncated);
        Assert.Equal(
            [
                "hover over button \"Upgrade\"",
                "double-tap button \"Upgrade\"",
                "long-press button \"Upgrade\"",
                "secondary-tap button \"Upgrade\"",
                "scroll button \"Upgrade\" into view",
                "uncheck button \"Upgrade\"",
                "drag button \"Upgrade\" to region \"Done column\" in \"Board\"",
                "upload \"fixtures/a.txt\", \"fixtures/b.txt\" to button \"Upgrade\"",
                "navigate back",
                "resize the viewport to 390x844",
                "hover over the point (10, 20)",
            ],
            entry.Actions.Select(action => action.Summary));
        Assert.Equal("uncheck", entry.Actions[5].Kind);
        Assert.Equal(("region", "Done column", "Board"), (entry.Actions[6].Destination?.Role, entry.Actions[6].Destination?.Name, entry.Actions[6].Destination?.Within));
        Assert.Equal(["fixtures/a.txt", "fixtures/b.txt"], entry.Actions[7].Paths);
        Assert.Equal((new Point(10, 20), new TraceViewport(1280, 720)), (read.Actions[10].Point!.Value, read.Actions[10].Viewport));
        Assert.Equal(new TraceViewport(390, 844), read.Actions[9].Size);
        Assert.Equal("resize", entry.Actions[9].Kind);
        Assert.Equal(JsonSerializer.Serialize(entry.Actions, Relaxed), JsonSerializer.Serialize(read.Actions, Relaxed));
    }

    /// <summary>
    /// .NET-only: no upstream <c>it</c> covers the kinds the port records beyond upstream's grammar. A <c>check</c> and a <c>clear</c> are
    /// their own kinds in the port; upstream's <c>check</c> carries <c>checked</c> and its <c>type</c> carries an empty value.
    /// </summary>
    [Fact]
    public void Summarizes_a_check_and_a_clear_the_port_records_as_their_own_kinds()
    {
        var recorder = MakeRecorder();
        recorder.Record("check", UpgradeButton);
        recorder.Record("clear", UpgradeButton);

        Assert.Equal(["check button \"Upgrade\"", "type \"\" into button \"Upgrade\""], Finalize(recorder)!.Actions.Select(action => action.Summary));
    }

    /// <summary>Partial port of "rejects a stored check, upload, drag, or resize that lost a field": the upload's paths, the drag's destination, and the resize's size, which the port checks on the action itself. The check's <c>checked</c> (a typed bool) and the resize's non-integer side (a typed int) cannot be malformed in a typed entry, and the unknown name is a store concern.</summary>
    [Fact]
    public void Partial_Rejects_a_stored_check_upload_drag_or_resize_that_lost_a_field()
    {
        var resize = new RecordedAction { Kind = "resize", Size = new TraceViewport(390, 844) };
        Assert.True(resize.HasValidSize());

        resize.Size = null;
        Assert.False(resize.HasValidSize());

        resize.Size = new TraceViewport(200, 4096);
        Assert.True(resize.HasValidSize());

        resize.Size = new TraceViewport(199, 844);
        Assert.False(resize.HasValidSize());

        resize.Size = new TraceViewport(390, 4097);
        Assert.False(resize.HasValidSize());

        Assert.False(new RecordedAction { Kind = "tap", Role = "button", Name = "Upgrade", Size = new TraceViewport(390, 844) }.HasValidSize());

        var upload = new RecordedAction { Kind = "upload", Role = "button", Name = "Upgrade", Paths = ["fixtures/a.txt"] };
        Assert.True(upload.HasValidPaths());

        upload.Paths = [];
        Assert.False(upload.HasValidPaths());

        upload.Paths = [""];
        Assert.False(upload.HasValidPaths());

        var drag = new RecordedAction { Kind = "drag", Role = "button", Name = "Upgrade", Destination = new RecordedTarget { Role = "region", Name = "Done column" } };
        Assert.True(drag.HasValidDestination());

        drag.Destination = null;
        Assert.False(drag.HasValidDestination());

        drag.Destination = new RecordedTarget();
        Assert.False(drag.HasValidDestination());

        var tap = new RecordedAction { Kind = "tap", Role = "button", Name = "Upgrade", Destination = new RecordedTarget { Role = "region", Name = "Done column" } };
        Assert.False(tap.HasValidDestination());
    }
}
