// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cache;
using E2E.Engine;
using E2E.Internal;
using Recorder = E2E.Cache.TraceRecorder;
using static E2E.Tests.RelocatePosition.DescribePositionTests;
using static E2E.Tests.RelocatePosition.RelocationWithARecordedPositionTests;

namespace E2E.Tests.RelocatePlaceholder;

/// <summary>Ports <c>relocate-placeholder.test.ts</c>'s "relocation by placeholder": a field named only by its placeholder is recorded and relocated by it.</summary>
public sealed class RelocationByPlaceholderTests
{
    private static SemanticNode Textbox(string id, string placeholder, string? name = null) =>
        new() { Ref = id, Role = "textbox", Name = name, Placeholder = placeholder, Attributes = new Dictionary<string, string> { ["type"] = "search", ["placeholder"] = placeholder } };

    private static RecordedTarget Describe(SemanticNode node) => Recorder.DescribeTarget(node, Redactor.None);

    [Fact]
    public void Records_the_placeholder_on_the_descriptor_beside_the_name_the_engine_derived_from_it()
    {
        var descriptor = Describe(Textbox("n1", "Search products", "Search products"));

        Assert.Equal(("textbox", "Search products", "Search products"), (descriptor.Role, descriptor.Name, descriptor.Placeholder));
        Assert.Null(descriptor.Text);
        Assert.Null(descriptor.TestId);
        Assert.Null(descriptor.InputPurpose);
    }

    [Fact]
    public void Relocates_an_unnamed_field_by_its_placeholder_alone()
    {
        var descriptor = Describe(Textbox("n1", "Search products"));

        Assert.Equal("textbox", descriptor.Role);
        Assert.Equal("Search products", descriptor.Placeholder);
        Assert.Null(descriptor.Name);
        Assert.Null(descriptor.Text);
        var nodes = Screen(Textbox("m1", "Filter tags"), Textbox("m2", "Search products"));

        Assert.Equal(new RelocationResult.Found("m2"), Relocate.RelocateDescriptor(descriptor, nodes, Redactor.None));
    }

    [Fact]
    public void Fails_instead_of_guessing_when_the_placeholder_changed_or_repeats()
    {
        var descriptor = Describe(Textbox("n1", "Search products"));

        var renamed = Relocate.RelocateDescriptor(descriptor, Screen(Textbox("m1", "Find products")), Redactor.None);
        var twins = Relocate.RelocateDescriptor(descriptor, Screen(Textbox("m1", "Search products"), Textbox("m2", "Search products")), Redactor.None);

        Assert.Equal(new RelocationResult.Failed(RelocationFailure.TargetNotFound), renamed);
        AssertAmbiguous(twins, "m1", "m2");
    }
}
