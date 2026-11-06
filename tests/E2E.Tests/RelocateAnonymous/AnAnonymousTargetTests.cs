// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cache;
using E2E.Engine;
using E2E.Internal;
using E2E.Tests.StepCache;
using Recorder = E2E.Cache.TraceRecorder;
using static E2E.Tests.RelocatePosition.DescribePositionTests;
using static E2E.Tests.RelocatePosition.RelocationWithARecordedPositionTests;

namespace E2E.Tests.RelocateAnonymous;

/// <summary>
/// Ports <c>relocate-anonymous.test.ts</c>'s "an anonymous target": a control with only a role relocates by its place among the unnamed
/// controls of its kind. The trace reader of "survives the trace reader with a count of one" is the store's read: what the recorder
/// finalized, written and read back.
/// </summary>
public sealed class AnAnonymousTargetTests
{
    private static SemanticNode Textbox(string id, string? name = null) => new() { Ref = id, Role = "textbox", Name = name };

    private static RecordedTarget Recorded(int index, int of) => new() { Role = "textbox", Position = new TracePosition { Index = index, Of = of } };

    private static void AssertNotFound(RelocationResult result) => Assert.Equal(new RelocationResult.Failed(RelocationFailure.TargetNotFound), result);

    [Fact]
    public void Records_its_place_among_the_unnamed_controls_of_its_kind_named_ones_aside()
    {
        var name = Textbox("name");
        var site = Textbox("site", "https://example.com");
        var vat = Textbox("vat");
        var form = Screen(name, site, vat);

        var first = Relocate.DescribePosition(name, null, form, Redactor.None);
        var second = Relocate.DescribePosition(vat, null, form, Redactor.None);

        Assert.Equal((0, 2), (first?.Index, first?.Of));
        Assert.Equal((1, 2), (second?.Index, second?.Of));
        // The named one relocates by name and needs no position.
        Assert.Null(Relocate.DescribePosition(site, null, form, Redactor.None));
    }

    [Fact]
    public void Records_a_position_even_when_it_is_the_only_one_since_without_it_nothing_could_relocate_it()
    {
        var name = Textbox("name");
        var form = Screen(name, Textbox("site", "https://example.com"));

        var position = Relocate.DescribePosition(name, null, form, Redactor.None);

        Assert.Equal((0, 1), (position?.Index, position?.Of));
    }

    [Fact]
    public void Relocates_the_only_unnamed_control_of_its_role_never_to_a_named_control_of_the_same_role()
    {
        var result = Relocate.RelocateDescriptor(Recorded(0, 1), Screen(Textbox("a", "Email"), Textbox("b")), Redactor.None);

        Assert.Equal(new RelocationResult.Found("b"), result);
    }

    [Fact]
    public void Hands_off_a_place_counted_among_unnamed_twins_with_no_container_to_tell_them_apart_since_rows_reorder()
    {
        var result = Relocate.RelocateDescriptor(Recorded(1, 2), Screen(Textbox("a", "Email"), Textbox("b"), Textbox("c")), Redactor.None);

        AssertNotFound(result);
    }

    [Fact]
    public void Diverges_when_the_number_of_unnamed_controls_changed_or_none_is_left()
    {
        var recorded = Recorded(0, 1);

        AssertAmbiguous(Relocate.RelocateDescriptor(recorded, Screen(Textbox("a"), Textbox("b")), Redactor.None), "a", "b");
        AssertNotFound(Relocate.RelocateDescriptor(recorded, Screen(Textbox("a", "Email")), Redactor.None));
        // Counted among two with no container, even one survivor is never taken for it.
        AssertNotFound(Relocate.RelocateDescriptor(Recorded(1, 2), Screen(Textbox("a", "Email"), Textbox("b")), Redactor.None));
    }

    [Fact]
    public void Never_lets_a_churned_test_id_fall_back_to_an_anonymous_position()
    {
        // Recorded among test-id twins; when the id churns, the position says nothing about unnamed textboxes.
        var recorded = new RecordedTarget { Role = "textbox", TestId = "field-zz9", Position = new TracePosition { Index = 0, Of = 2 } };

        AssertNotFound(Relocate.RelocateDescriptor(recorded, Screen(Textbox("a"), Textbox("b")), Redactor.None));
    }

    [Fact]
    public void Survives_the_trace_reader_with_a_count_of_one()
    {
        var store = new FileStepCache(CoreTests.TempCache());
        var recorder = new Recorder(() => Redactor.None);
        recorder.Record(new RecordedAction { Kind = "type", Role = "textbox", Value = "x", Position = new TracePosition { Index = 0, Of = 1 } });
        store.Write("abc123", recorder.Finalize(StepCacheHarness.ExampleStep, "/form", "/form", [], [])!);

        var action = Assert.IsType<CacheEntry>(store.Read("abc123").Entry).Actions[0];

        Assert.Equal(("textbox", 0, 1), (action.Role, action.Position?.Index, action.Position?.Of));
    }

    [Fact]
    public void Stays_unrelocatable_without_a_recorded_position_as_before()
    {
        AssertNotFound(Relocate.RelocateDescriptor(new RecordedTarget { Role = "textbox" }, Screen(Textbox("a")), Redactor.None));
    }
}
