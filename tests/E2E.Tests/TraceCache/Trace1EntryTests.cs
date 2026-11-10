// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Engine;
using E2E.Internal;
using E2E.Tests.StepCache;
using Recorder = E2E.Cache.TraceRecorder;
using static E2E.Tests.StepCache.StepCachePipelineTests;

namespace E2E.Tests.TraceCache;

/// <summary>
/// The stored entry, <c>FileStepCache</c> and its schema 1. The port has no <c>createdAt</c>, summaries, executor, or
/// <c>typeSecret</c> action, so "missing createdAt", "unparseable createdAt", "unknown action name", "tap without a target",
/// "type without a value", "select with an empty value", "empty summary", "oversized summary", and "missing executor name" are not
/// ported. "empty descriptor", "empty anchor descriptor", and the oversized descriptor field are ported in
/// <see cref="Rejects_a_target_that_names_nothing_or_carries_an_oversized_field"/>. The caps and the anchor states are checked where the
/// replay reads the entry, so their its run at the session seam like the resize ones.
/// </summary>
public sealed class Trace1EntryTests
{
    private const string Key = StepCacheHarness.Key;

    private static readonly JsonSerializerOptions EntryJson = new(JsonSerializerDefaults.Web);

    private static CacheEntry Entry(params RecordedAction[] actions) => new() { Route = "/storage", EndRoute = "/storage", Actions = [.. actions] };

    private static CacheEntry RoundTrip(CacheEntry entry)
    {
        var store = new FileStepCache(CoreTests.TempCache());
        store.Write(Key, entry);
        return store.Read(Key).Entry!;
    }

    private static CacheLookup ReadJson(string json)
    {
        var directory = CoreTests.TempCache();
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, Key + ".json"), json);
        return new FileStepCache(directory).Read(Key);
    }

    private static CacheLookup ReadRecordedBy(string recordedBy) =>
        ReadJson("{\"actions\":[{\"kind\":\"back\"}],\"route\":\"/\",\"endRoute\":\"/\",\"recordedBy\":" + recordedBy + "}");

    /// <summary>
    /// Port of "bounds recorded %s without losing the hit". An oversized recorded model reads as absent and the entry still reads.
    /// Difference: the port records one string, <c>IAgentModel.Name</c>, so there is one case, not one per field.
    /// </summary>
    [Fact]
    public void Bounds_the_recorded_model_without_losing_the_hit()
    {
        var atLimit = ReadRecordedBy("\"" + new string('x', 300) + "\"");
        var oversized = ReadRecordedBy("\"" + new string('x', 301) + "\"");

        Assert.Equal(new string('x', 300), CacheEntry.ReadRecordedBy(atLimit.Entry!.RecordedBy));
        Assert.NotNull(oversized.Entry);
        Assert.Null(CacheEntry.ReadRecordedBy(oversized.Entry.RecordedBy));
    }

    /// <summary>Port of "ignores blank recording identity %j without losing the hit". A value that is not a string reads as absent too.</summary>
    [Theory]
    [InlineData("\"\"")]
    [InlineData("\"  \"")]
    [InlineData("1")]
    [InlineData("null")]
    [InlineData("{\"provider\":\"test\",\"model\":\"A\"}")]
    public void Ignores_a_blank_or_malformed_recording_identity_without_losing_the_hit(string recordedBy)
    {
        var read = ReadRecordedBy(recordedBy);

        Assert.NotNull(read.Entry);
        Assert.Null(CacheEntry.ReadRecordedBy(read.Entry.RecordedBy));
    }

    /// <summary>
    /// Partial port of "round-trips every action variant through JSON": the port's actions are <c>fill</c>, <c>check</c>,
    /// <c>uncheck</c>, <c>clear</c>, <c>scrollTo</c>, and <c>scrollUntil</c> beside the shared ones, with no <c>typeSecret</c>
    /// and no verdict summary, and the entry carries no executor.
    /// </summary>
    [Fact]
    public void Round_trips_every_action_kind_the_port_records_through_the_file_store()
    {
        var target = (Role: "textbox", Name: "Email");
        RecordedAction Action(string kind) => new() { Kind = kind, Role = target.Role, Name = target.Name, TestId = "email" };
        var actions = new[]
        {
            new RecordedAction { Kind = "navigate", Url = "/settings" },
            Action("tap"),
            Action("doubleTap"),
            new RecordedAction { Kind = "fill", Role = target.Role, Name = target.Name, Value = "x" },
            new RecordedAction { Kind = "press", Role = target.Role, Name = target.Name, Key = "Enter" },
            new RecordedAction { Kind = "typeText", Value = "a@b.c", Replace = false },
            new RecordedAction { Kind = "pressKey", Key = "Enter" },
            new RecordedAction { Kind = "select", Role = "combobox", Name = "Plan", Value = "Pro" },
            Action("check"),
            Action("uncheck"),
            Action("clear"),
            Action("scrollTo"),
            new RecordedAction { Kind = "scroll", Direction = "down", Times = 3 },
            new RecordedAction { Kind = "scroll", Direction = "up", Role = "group", Name = "Rows" },
            new RecordedAction { Kind = "scrollUntil", Direction = "down", Text = "Row 40" },
            new RecordedAction { Kind = "back" },
        };

        var read = RoundTrip(Entry(actions));

        Assert.Equal(JsonSerializer.Serialize(actions, EntryJson), JsonSerializer.Serialize(read.Actions, EntryJson));
    }

    [Fact]
    public void Round_trips_an_empty_typed_value_clearing_a_field_is_a_recordable_action()
    {
        RecordedAction[] actions =
        [
            new() { Kind = "fill", Role = "textbox", Name = "Name", Value = "" },
            new() { Kind = "typeText", Value = "", Replace = true },
        ];

        var read = RoundTrip(Entry(actions));

        Assert.Equal(JsonSerializer.Serialize(actions, EntryJson), JsonSerializer.Serialize(read.Actions, EntryJson));
    }

    /// <summary>Partial port of "round-trips the postcondition: end path and end anchors": the port stores the end route, and the anchors are <c>appeared</c>.</summary>
    [Fact]
    public void Round_trips_the_end_route_and_the_appeared_anchors()
    {
        var entry = Entry(new RecordedAction { Kind = "tap", Role = "button", Name = "Save" });
        entry.EndRoute = "/settings";
        entry.Appeared = [new RecordedTarget { Role = "status", Name = "Marker", Text = "saved" }, new RecordedTarget { Text = "Playbook saved" }];

        var read = RoundTrip(entry);

        Assert.Equal("/settings", read.EndRoute);
        Assert.Equal(JsonSerializer.Serialize(entry.Appeared, EntryJson), JsonSerializer.Serialize(read.Appeared, EntryJson));
    }

    [Fact]
    public void Round_trips_both_sides_of_the_delta_anchor_states_included_and_a_long_location_whole()
    {
        var longPath = "/search?q=" + new string('x', 1_000);
        var entry = Entry(new RecordedAction { Kind = "tap", Role = "button", Name = "Save" });
        entry.Route = longPath;
        entry.Appeared = [new RecordedTarget { Role = "switch", Name = "Email notifications", States = ["checked", "pressed"] }];
        entry.Gone = [new RecordedTarget { Role = "listitem", Name = "Item A" }];

        var read = RoundTrip(entry);

        Assert.Equal(longPath, read.Route);
        Assert.Equal(["checked", "pressed"], Assert.Single(read.Appeared).States);
        Assert.Equal(("listitem", "Item A"), (Assert.Single(read.Gone).Role, Assert.Single(read.Gone).Name));
    }

    [Fact]
    public void Drops_unknown_fields_instead_of_carrying_them()
    {
        var directory = CoreTests.TempCache();
        var store = new FileStepCache(directory);
        store.Write(Key, Entry(new RecordedAction { Kind = "tap", Role = "button", Name = "Save" }));
        var path = Path.Combine(directory, Key + ".json");
        var document = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        document["extra"] = "x";
        document["actions"]![0]!["extra"] = "x";
        File.WriteAllText(path, document.ToJsonString());

        var entry = store.Read(Key).Entry;
        Assert.NotNull(entry);
        store.Write(Key, entry);

        Assert.DoesNotContain("\"extra\"", File.ReadAllText(path), StringComparison.Ordinal);
    }

    public static TheoryData<string, string> Rejected => new()
    {
        { "not an object", "\"nope\"" },
        { "wrong schema version", """{ "schema": 2, "actions": [{ "kind": "tap", "role": "button" }], "appeared": [], "gone": [] }""" },
        { "non-string descriptor field", """{ "schema": 1, "actions": [{ "kind": "tap", "role": 7 }], "appeared": [], "gone": [] }""" },
        { "non-array anchors", """{ "schema": 1, "actions": [{ "kind": "tap", "role": "button" }], "appeared": { "role": "status" }, "gone": [] }""" },
        { "non-string anchor field", """{ "schema": 1, "actions": [{ "kind": "tap", "role": "button" }], "appeared": [{ "text": 42 }], "gone": [] }""" },
        { "non-array gone anchors", """{ "schema": 1, "actions": [{ "kind": "tap", "role": "button" }], "appeared": [], "gone": { "role": "status" } }""" },
    };

    [Theory]
    [MemberData(nameof(Rejected))]
    public void Rejects(string label, string json)
    {
        var lookup = ReadJson(json);

        Assert.True(lookup.Entry is null, label);
        Assert.Equal("invalid-entry", lookup.Reason);
    }

    /// <summary>Partial port of "rejects empty actions" and "rejects bad scroll direction": a store may return both, and the replay treats each as a miss.</summary>
    [Theory]
    [InlineData("empty actions", null)]
    [InlineData("bad scroll direction", "diagonal")]
    public async Task Rejects_an_entry_the_replay_cannot_use_as_a_miss(string label, string? direction)
    {
        RecordedAction[] actions = direction is null ? [] : [new RecordedAction { Kind = "scroll", Direction = direction }];
        var cache = new FixedCache(new CacheEntry { Route = "/storage", Actions = [.. actions] });

        var step = await StepAsync(StorageWorld(), cache, "/storage", null, Script(ModelResponses.Tap("button", "Save marker")));

        Assert.True(step.Error is null, label);
        Assert.Equal(("missed", "invalid-entry", 0), (step.Cache?.Mode, step.Cache?.Reason, step.Cache?.ReplayedActions));
    }

    public static TheoryData<string> OverCap =>
    [
        "too many actions",
        "oversized input value",
        "oversized start path",
        "too many anchors",
        "too many gone anchors",
        "unknown anchor state",
        "unsorted anchor states",
        "repeated anchor state",
        "empty anchor states",
    ];

    /// <summary>
    /// Port of the "rejects %s" cases that name a cap or an anchor state. Difference: the port reads the entry at the replay, not in the
    /// file store, so each is a miss with the reason <c>invalid-entry</c>, as the resize cases are.
    /// </summary>
    [Theory]
    [MemberData(nameof(OverCap))]
    public async Task Rejects_an_entry_over_a_cap_or_with_malformed_anchor_states_as_a_miss(string label)
    {
        var tap = new RecordedAction { Kind = "tap", Role = "button", Name = "Save marker" };
        var entry = new CacheEntry { Route = "/storage", Actions = [tap] };
        RecordedTarget Switch(params string[] states) => new() { Role = "switch", Name = "On", States = [.. states] };
        switch (label)
        {
            case "too many actions":
                entry.Actions = [.. Enumerable.Range(0, Trace.MaxTraceActions + 1).Select(_ => tap)];
                break;
            case "oversized input value":
                entry.Actions = [new RecordedAction { Kind = "fill", Role = "textbox", Name = "Name", Value = new string('v', Trace.MaxTraceInputChars + 1) }];
                break;
            case "oversized start path":
                entry.Route = "/" + new string('x', Trace.MaxTraceInputChars + 1);
                break;
            case "too many anchors":
                entry.Appeared = [.. Enumerable.Range(0, Trace.MaxTraceAnchors + 1).Select(index => new RecordedTarget { Text = "a" + index })];
                break;
            case "too many gone anchors":
                entry.Gone = [.. Enumerable.Range(0, Trace.MaxTraceAnchors + 1).Select(index => new RecordedTarget { Text = "a" + index })];
                break;
            case "unknown anchor state":
                entry.Appeared = [Switch("focused")];
                break;
            case "unsorted anchor states":
                entry.Appeared = [Switch("selected", "checked")];
                break;
            case "repeated anchor state":
                entry.Appeared = [Switch("checked", "checked")];
                break;
            case "empty anchor states":
                entry.Appeared = [Switch()];
                break;
        }

        var step = await StepAsync(StorageWorld(), new FixedCache(entry), "/storage", null, Script(ModelResponses.Tap("button", "Save marker")));

        Assert.True(step.Error is null, label);
        Assert.Equal(("missed", "invalid-entry", 0), (step.Cache?.Mode, step.Cache?.Reason, step.Cache?.ReplayedActions));
    }

    /// <summary>Partial port of "rejects resize below the viewport limits" and "rejects resize above the viewport limits": a store may return both, and the replay treats each as a miss.</summary>
    [Theory]
    [InlineData("resize below the viewport limits", 100, 844)]
    [InlineData("resize above the viewport limits", 390, 5000)]
    public async Task Rejects_a_resize_outside_the_viewport_limits_as_a_miss(string label, int width, int height)
    {
        var cache = new FixedCache(new CacheEntry { Route = "/storage", Actions = [new RecordedAction { Kind = "resize", Size = new E2E.Engine.TraceViewport(width, height) }] });

        var step = await StepAsync(StorageWorld(), cache, "/storage", null, Script(ModelResponses.Tap("button", "Save marker")));

        Assert.True(step.Error is null, label);
        Assert.Equal(("missed", "invalid-entry", 0), (step.Cache?.Mode, step.Cache?.Reason, step.Cache?.ReplayedActions));
    }

    /// <summary>
    /// Port of "rejects %s" for the target cases: a node action, a point's place, or an anchor whose descriptor names nothing or holds a field
    /// over its budget. Read through <see cref="TraceRead.ReadTraceEntry(CacheEntry?)"/> directly, as upstream's <c>readTraceEntry</c> reads them.
    /// Upstream's "tap without a target", "empty descriptor", and "empty anchor descriptor" are ported as they are; the others are .NET-only
    /// cases of the same rule (the descriptor's text budget, and a position that names no node).
    /// </summary>
    public static TheoryData<string> UnnamedOrOversizedTargets =>
    [
        "tap without a target",
        "empty descriptor",
        "oversized descriptor field",
        "empty anchor descriptor",
        "oversized anchor field",
        "position alone does not name a target",
        "tap under a point whose node names nothing",
        "scroll with a target that names nothing",
        "scroll until with a target that names nothing",
        "scroll until whose target text is over budget",
    ];

    [Theory]
    [MemberData(nameof(UnnamedOrOversizedTargets))]
    public void Rejects_a_target_that_names_nothing_or_carries_an_oversized_field(string label)
    {
        var oversized = new string('x', Trace.MaxTraceDescriptorChars + 1);
        var tap = new RecordedAction { Kind = "tap", Role = "button", Name = "Save" };
        var entry = Entry(tap);
        switch (label)
        {
            case "tap without a target":
                entry = Entry(new RecordedAction { Kind = "tap" });
                break;
            case "empty descriptor":
                entry = Entry(new RecordedAction { Kind = "tap", Role = "", Name = "" });
                break;
            case "oversized descriptor field":
                entry = Entry(new RecordedAction { Kind = "tap", Role = "button", Name = oversized });
                break;
            case "empty anchor descriptor":
                entry.Appeared = [new RecordedTarget()];
                break;
            case "oversized anchor field":
                entry.Gone = [new RecordedTarget { Text = oversized }];
                break;
            case "position alone does not name a target":
                entry = Entry(new RecordedAction { Kind = "tap", Position = new TracePosition { Index = 0, Of = 2 } });
                break;
            case "tap under a point whose node names nothing":
                entry = Entry(new RecordedAction
                {
                    Kind = "tapAt",
                    Point = new E2E.Engine.Point(10, 10),
                    Viewport = new E2E.Engine.TraceViewport(390, 844),
                    Under = new RecordedPointPlace { Target = new RecordedTarget(), Fx = 0.5, Fy = 0.5 },
                });
                break;
            case "scroll with a target that names nothing":
                entry = Entry(new RecordedAction { Kind = "scroll", Direction = "down", Role = "" });
                break;
            case "scroll until with a target that names nothing":
                entry = Entry(new RecordedAction { Kind = "scrollUntil", Direction = "down", Text = "Row 40", Role = "" });
                break;
            case "scroll until whose target text is over budget":
                entry = Entry(new RecordedAction { Kind = "scrollUntil", Direction = "down", Text = "Row 40", TargetText = oversized });
                break;
        }

        Assert.Null(TraceRead.ReadTraceEntry(entry));
    }

    /// <summary>
    /// The other side of the rule: a target at the descriptor budget reads, a scroll with no target reads as no target, a scroll whose only
    /// target field is its position reads (the port's recorder writes that for an unnamed list container), and a point's node that names itself
    /// reads. .NET-only: upstream has no such cases, but they pin the boundary the rejections above sit next to.
    /// </summary>
    [Fact]
    public void Reads_a_target_at_the_descriptor_budget_and_a_scroll_with_no_target()
    {
        var atBudget = new string('x', Trace.MaxTraceDescriptorChars);
        var atLimit = Entry(new RecordedAction { Kind = "tap", Role = "button", Name = atBudget });
        var scroll = Entry(new RecordedAction { Kind = "scroll", Direction = "down" });
        var positioned = Entry(new RecordedAction { Kind = "scroll", Direction = "down", Position = new TracePosition { Index = 0, Of = 2 } });
        var pagedUntil = Entry(new RecordedAction { Kind = "scrollUntil", Direction = "down", Text = "Row 40", Role = "row", TargetText = "Row 40 detail" });
        var point = Entry(new RecordedAction
        {
            Kind = "tapAt",
            Point = new E2E.Engine.Point(10, 10),
            Viewport = new E2E.Engine.TraceViewport(390, 844),
            Under = new RecordedPointPlace { Target = new RecordedTarget { Role = "button", Name = "Save" }, Fx = 0.5, Fy = 0.5 },
        });

        Assert.NotNull(TraceRead.ReadTraceEntry(atLimit));
        Assert.NotNull(TraceRead.ReadTraceEntry(scroll));
        Assert.NotNull(TraceRead.ReadTraceEntry(positioned));
        Assert.NotNull(TraceRead.ReadTraceEntry(point));
        Assert.NotNull(TraceRead.ReadTraceEntry(pagedUntil));
    }

    /// <summary>
    /// .NET-only: a node whose text is longer than the descriptor budget is recorded bounded, so the reader accepts what the recorder wrote.
    /// Upstream's <c>describeTarget</c> bounds each field the same way (<c>field</c>).
    /// </summary>
    [Fact]
    public void Records_a_long_node_text_bounded_to_the_descriptor_budget_so_the_reader_accepts_it()
    {
        var node = new SemanticNode { Ref = "n1", Role = "button", Name = "Save", Text = new string('x', 350) };
        var target = Recorder.DescribeTarget(node, Redactor.None);
        var entry = Entry(new RecordedAction { Kind = "tap", Role = target.Role, Name = target.Name, Text = target.Text });

        Assert.True(target.Text?.Length <= Trace.MaxTraceDescriptorChars);
        Assert.NotNull(TraceRead.ReadTraceEntry(entry));
    }

    /// <summary>
    /// .NET-only: a node with no accessible name takes its label from its text, which can be longer than the descriptor budget; the recorded name
    /// is bounded too, and the input purpose the recorder writes is kept (a password field is named by it). Anchors are bounded the same way,
    /// so a projected anchor of a long status line reads back.
    /// </summary>
    [Fact]
    public void Bounds_a_nameless_nodes_label_and_an_anchor_text_and_keeps_the_input_purpose()
    {
        var nameless = new SemanticNode { Ref = "n2", Role = "textbox", Text = new string('y', 350), InputPurpose = "password" };
        var target = Recorder.DescribeTarget(nameless, Redactor.None);
        var entry = Entry(new RecordedAction { Kind = "fill", Role = target.Role, Name = target.Name, Value = "x" });

        Assert.True(target.Name?.Length <= Trace.MaxTraceDescriptorChars);
        Assert.Equal("password", target.InputPurpose);
        Assert.NotNull(TraceRead.ReadTraceEntry(entry));

        var nodes = new Dictionary<string, SemanticNode> { ["n3"] = new SemanticNode { Ref = "n3", Role = "status", Text = new string('z', 350) } };
        var anchors = Anchors.Project(nodes, text => text, text => text);
        var status = new CacheEntry { Route = "/storage", Actions = [new RecordedAction { Kind = "tap", Role = "button", Name = "Save" }], Appeared = [.. anchors.Select(anchor => anchor.Target)] };
        Assert.NotNull(TraceRead.ReadTraceEntry(status));
    }

    /// <summary>.NET-only: a target text the stored action does not own (a tap's) is a miss, since only a <c>scrollUntil</c> reads it.</summary>
    [Fact]
    public void Rejects_a_target_text_on_an_action_that_reads_none()
    {
        var entry = Entry(new RecordedAction { Kind = "tap", Role = "button", Name = "Save", TargetText = "stray" });

        Assert.Null(TraceRead.ReadTraceEntry(entry));
    }

    /// <summary>
    /// .NET-only: a scrollUntil's paged text is its <see cref="RecordedAction.Text"/>, not its target's text, so the target's text is kept
    /// in <see cref="RecordedAction.TargetText"/> and survives the file store round trip, where the paged text does too.
    /// </summary>
    [Fact]
    public void Keeps_a_scroll_until_target_text_apart_from_the_paged_text()
    {
        var entry = Entry(new RecordedAction { Kind = "scrollUntil", Direction = "down", Text = "Row 40", Role = "row", TargetText = "Row 40 detail" });

        var read = RoundTrip(entry);

        var action = Assert.Single(read.Actions);
        Assert.Equal(("Row 40", "Row 40 detail"), (action.Text, action.TargetText));
    }

    private sealed class FixedCache(CacheEntry entry) : IStepCache
    {
        public bool Writable => true;

        public CacheLookup Read(string key) => new() { Entry = entry };

        public int? Write(string key, CacheEntry written) => 0;

        public void Delete(string key)
        {
        }
    }
}
