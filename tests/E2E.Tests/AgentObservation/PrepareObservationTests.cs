// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E.Tests.AgentObservation;

/// <summary>
/// <c>describe('prepareObservation')</c> of <c>agent-observation.test.ts</c>, over <c>AgentObservation.PrepareObservation</c>. Partial (the test name carries
/// <c>Partial_</c>): "keeps unavailable semantics separate from an empty tree and requires permitted masked evidence" (the port counts the secure nodes
/// from the tree, where upstream's engine reports <c>secureNodeCount</c>; the pixel observation has empty <c>Nodes</c> and <c>Tree</c>, where upstream's has neither property); "redacts every string field a node carries" (the port's nodes have no <c>selector</c>,
/// <c>framePath</c>, or unknown extra field). The nested <c>describe.each</c> (18 cases) is <c>CollapsedSecretCutTests</c>. The pixel tests
/// Grants_pixels_that_cover_every_secure_node_sized_for_the_model and Withholds_pixels_the_engine_cannot_count_the_masks_of_or_cannot_capture
/// are .NET-only: upstream proves a granted or uncounted capture through its runner.
/// </summary>
public sealed class PrepareObservationTests
{
    private static readonly System.Text.Json.JsonSerializerOptions Relaxed = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // Relaxed escaping keeps "<secret:name>" readable in the serialized tree.
    private static string Json<T>(T value) => System.Text.Json.JsonSerializer.Serialize(value, Relaxed);

    private static PreparedObservation Prepare(Observation observation, Redactor? redactor = null, int maxBytes = 4_096, bool pixelsAllowed = false) =>
        global::E2E.AgentObservation.PrepareObservation(observation, new PrepareObservationOptions { Redactor = redactor ?? Redactor.None, MaxBytes = maxBytes, PixelsAllowed = pixelsAllowed });

    /// <summary>
    /// Partial: the pixel observation has empty <c>Nodes</c> and <c>Tree</c>, where upstream's has neither property (<c>not.toHaveProperty</c>). The pixel observation reports
    /// <c>secureNodeCount: 1</c> with <c>maskedRegionCount: 0</c> from the engine, as upstream's does.
    /// </summary>
    [Fact]
    public void Partial_keeps_unavailable_semantics_separate_from_an_empty_tree_and_requires_permitted_masked_evidence()
    {
        Observation Unavailable(int masked) => new() { Route = "/", Roots = [], TreeUnavailable = true, SecureNodeCount = 1, Pixels = Shot(maskedRegionCount: masked) };
        var unproven = Assert.Throws<TestException>(() => Prepare(Unavailable(0), pixelsAllowed: true));
        Assert.Contains("proven-masked screenshot", unproven.Message, StringComparison.Ordinal);
        var prepared = Prepare(Unavailable(1), pixelsAllowed: true);
        Assert.Equal(PreparedKind.Pixels, prepared.Kind);
        Assert.Contains("semantic capture unavailable", prepared.Text, StringComparison.Ordinal);
        Assert.Contains("previous node ids are no longer valid", prepared.Text, StringComparison.Ordinal);
        Assert.Contains("never infer absence", prepared.Text, StringComparison.Ordinal);
        Assert.Empty(prepared.Nodes);
        Assert.Empty(prepared.Tree);
        Assert.NotNull(prepared.Pixels);
        Assert.Contains("permitted", Assert.Throws<TestException>(() => Prepare(Unavailable(1))).Message, StringComparison.Ordinal);
        var empty = Prepare(new Observation { Route = "/", Roots = [new SemanticNode { Ref = "root" }] });
        Assert.Equal(PreparedKind.Semantic, empty.Kind);
        Assert.DoesNotContain("unavailable", empty.Text, StringComparison.Ordinal);
    }

    /// <summary>.NET-only: the port counts secure nodes from the tree when an engine reports none, and never lets a reported count weaken that proof.</summary>
    [Fact]
    public void Counts_secure_nodes_from_the_tree_when_the_engine_reports_none()
    {
        var secure = new SemanticNode { Ref = "n1", Role = "textbox", States = new NodeStates { Secure = true } };
        Assert.Null(global::E2E.AgentObservation.PixelsForModel(new Observation { Route = "/", Roots = [secure] }, Shot(maskedRegionCount: 0)).Pixels);
        Assert.NotNull(global::E2E.AgentObservation.PixelsForModel(new Observation { Route = "/", Roots = [secure] }, Shot(maskedRegionCount: 1)).Pixels);
        Assert.Null(global::E2E.AgentObservation.PixelsForModel(new Observation { Route = "/", Roots = [secure], SecureNodeCount = 0 }, Shot(maskedRegionCount: 0)).Pixels);
        Assert.NotNull(global::E2E.AgentObservation.PixelsForModel(new Observation { Route = "/", Roots = [], TreeUnavailable = true }, Shot(maskedRegionCount: 0)).Pixels);
    }

    [Fact]
    public async Task Returns_evidence_without_a_comparable_shape_without_repeating_capture()
    {
        var captures = 0;
        var result = await global::E2E.AgentObservation.SettleAsync(
            _ =>
            {
                captures++;
                return Task.FromResult("pixels");
            },
            _ => null,
            new SettleOptions<string> { StableWait = TimeSpan.FromMilliseconds(30) },
            TimeProvider.System,
            CancellationToken.None);
        Assert.Equal("pixels", result);
        Assert.Equal(1, captures);
    }

    [Fact]
    public void Carries_an_engine_reported_cut_through_as_truncated_with_its_own_marker()
    {
        var tree = new SemanticNode { Ref = "n1", Role = "document", Children = [new SemanticNode { Ref = "n2", Role = "button", Name = "One" }] };
        var prepared = Prepare(new Observation { Route = "/", Roots = [tree], Truncated = true });
        Assert.Equal(PreparedKind.Semantic, prepared.Kind);
        Assert.True(prepared.Truncated);
        Assert.Equal(["#n1 document", " #n2 button \"One\"", "[observation truncated: the engine stopped listing nodes at its limit]"], prepared.Text.Split('\n'));
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(prepared.Text), prepared.Bytes);
        Assert.Equal(2, prepared.Nodes.Count);
    }

    [Fact]
    public void Truncates_at_the_byte_limit_while_keeping_the_root_and_flagging_truncation()
    {
        var children = Enumerable.Range(0, 200).Select(index => new SemanticNode { Ref = "c" + index, Role = "button", Name = "Button number " + index }).ToList();
        var tree = new SemanticNode { Ref = "n1", Role = "document", Children = children };
        var prepared = Prepare(new Observation { Route = "/", Roots = [tree] }, maxBytes: 256);
        Assert.Equal(PreparedKind.Semantic, prepared.Kind);
        Assert.True(prepared.Truncated);
        Assert.StartsWith("#n1 document", prepared.Text, StringComparison.Ordinal);
        Assert.Contains("[observation truncated", prepared.Text, StringComparison.Ordinal);
        Assert.Equal(201, prepared.Nodes.Count);
    }

    [Fact]
    public void Reports_the_byte_cut_not_the_engine_cut_when_both_apply()
    {
        var children = Enumerable.Range(0, 200).Select(index => new SemanticNode { Ref = "c" + index, Role = "button", Name = "Button " + index }).ToList();
        var tree = new SemanticNode { Ref = "n1", Role = "document", Children = children };
        var prepared = Prepare(new Observation { Route = "/", Roots = [tree], Truncated = true }, maxBytes: 256);
        Assert.True(prepared.Truncated);
        Assert.True(prepared.Bytes <= 256);
        Assert.EndsWith("[observation truncated at the resolved observation byte limit]", prepared.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("engine stopped", prepared.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Redacts_an_earlier_capture_again_with_a_secret_resolved_since_keeping_which_nodes_are_leaves()
    {
        var tree = new SemanticNode
        {
            Ref = "n1",
            Role = "list",
            Children = [new SemanticNode { Ref = "n2", Role = "listitem", Name = "tok_9f8e7d6c5b4a3210", Children = [new SemanticNode { Ref = "n3", Text = "tok_9f8e7d6c5b4a3210" }] }],
        };
        var prepared = Prepare(new Observation { Route = "/", Roots = [tree] });
        var after = Redactor.For([Secret.Create("token", "tok_9f8e7d6c5b4a3210")]);
        var again = global::E2E.AgentObservation.RedactNodesAgain(prepared.Nodes, after);
        Assert.DoesNotContain("tok_9f8e7d6c5b4a3210", Json(again.Values), StringComparison.Ordinal);
        Assert.Equal("<secret:token>", again["n2"].Name);
        Assert.Equal("<secret:token>", again["n2"].Children.Single().Text);
        Assert.Same(again["n3"], again["n2"].Children.Single());
        Assert.Empty(again["n3"].Children);
    }

    [Fact]
    public void Withholds_pixels_whose_masking_the_engine_cannot_prove_and_keeps_the_tree()
    {
        // One secure node, zero masked regions: the image is not provably redacted.
        var observation = WithASecureNode();
        var outcome = global::E2E.AgentObservation.PixelsForModel(observation, Shot(maskedRegionCount: 0));

        Assert.Null(outcome.Pixels);
        Assert.Equal(VisionDegradation.MaskingUnproven, outcome.Withheld);
        Assert.Contains("textbox \"Password\"", SnapshotText.Render(observation, Redactor.None), StringComparison.Ordinal);
    }

    [Fact]
    public void Grants_pixels_that_cover_every_secure_node_sized_for_the_model()
    {
        var outcome = global::E2E.AgentObservation.PixelsForModel(WithASecureNode(), Shot(maskedRegionCount: 1));

        Assert.Null(outcome.Withheld);
        Assert.Equal(1, outcome.Pixels!.MaskedRegionCount);
        Assert.Equal((4, 2), Pixels.ReadPngSize(outcome.Pixels.Png));
    }

    [Fact]
    public void Withholds_pixels_the_engine_cannot_count_the_masks_of_or_cannot_capture()
    {
        var observation = WithASecureNode();
        Assert.Equal(VisionDegradation.MaskingUnproven, global::E2E.AgentObservation.PixelsForModel(observation, Shot(maskedRegionCount: null)).Withheld);
        Assert.Equal(VisionDegradation.UnsupportedCapability, global::E2E.AgentObservation.PixelsForModel(observation, null).Withheld);
    }

    private static Observation WithASecureNode() => new()
    {
        Route = "/",
        Roots =
        [
            new SemanticNode
            {
                Ref = "n1",
                Children = [new SemanticNode { Ref = "n2", Role = "textbox", Name = "Password", InputPurpose = "password", States = new NodeStates { Secure = true } }],
            },
        ],
    };

    private static EngineScreenshot Shot(int? maskedRegionCount) => new(E2E.Tests.Agent.ScreenshotEngine.Png(), 1, maskedRegionCount);

    [Fact]
    public void Serializes_the_tree_with_node_references_and_indentation()
    {
        var tree = new SemanticNode
        {
            Ref = "n1",
            Role = "document",
            Name = "Home",
            Children =
            [
                new SemanticNode { Ref = "n2", Role = "heading", Name = "Welcome" },
                new SemanticNode { Ref = "n3", Role = "button", Name = "Buy", States = new NodeStates { Disabled = true } },
            ],
        };
        var text = SnapshotText.Render(new Observation { Route = "/", Roots = [tree] }, Redactor.None);
        Assert.Equal(
            ["#n1 document \"Home\"", " #n2 heading \"Welcome\"", " #n3 button \"Buy\" [disabled]"],
            text.Split('\n'));
    }

    [Fact]
    public void Masks_secure_fields_and_never_renders_their_value()
    {
        var tree = new SemanticNode
        {
            Ref = "n1",
            Children = [new SemanticNode { Ref = "n2", Role = "textbox", Name = "Password", Value = "should-not-appear", InputPurpose = "password", States = new NodeStates { Secure = true } }],
        };
        var text = SnapshotText.Render(new Observation { Route = "/", Roots = [tree] }, Redactor.None);
        Assert.Contains(" secure", text, StringComparison.Ordinal);
        Assert.Contains("purpose=password", text, StringComparison.Ordinal);
        Assert.DoesNotContain("should-not-appear", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Replaces_every_exact_registered_secret_value_with_its_stable_name()
    {
        var tree = new SemanticNode
        {
            Ref = "n1",
            Children =
            [
                new SemanticNode { Ref = "n2", Role = "status", Text = "signed in as hunter2 (hunter2)" },
                new SemanticNode { Ref = "n3", Role = "textbox", Name = "hunter2", Value = "hunter2" },
            ],
        };
        var text = SnapshotText.Render(new Observation { Route = "/reset?token=hunter2", Roots = [tree] }, Redactor.For([Secret.Create("member", "hunter2")]));
        Assert.DoesNotContain("hunter2", text, StringComparison.Ordinal);
        Assert.Contains("<secret:member>", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Collapses_whitespace_and_strips_control_characters_from_app_text()
    {
        var text = SnapshotText.Render(new Observation { Route = "/", Roots = [new SemanticNode { Ref = "n1", Role = "status", Text = "line\u0007one\n   two  " }] }, Redactor.None);
        Assert.Contains("\"line\uFFFDone two\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_the_whole_address_after_the_origin_as_the_path_fragment_included_for_the_route_check_to_read()
    {
        string? At(string location) => Prepare(new Observation { Route = "/", Location = location, Roots = [new SemanticNode { Ref = "root" }] }, pixelsAllowed: true).Path;
        Assert.Equal("/companies?search=a#/orders/42", At("https://app.example.test/companies?search=a#/orders/42"));
        Assert.Equal("/companies#top", At("https://app.example.test/companies#top"));
        Assert.Equal("Settings", At("Settings"));
    }

    /// <summary>.NET-only: the port's mobile and desktop engines report a route and no location, so the path reads the redacted route; a location is redacted before it is read.</summary>
    [Fact]
    public void Reads_the_redacted_route_as_the_path_when_there_is_no_location_and_redacts_a_location_before_it_is_read()
    {
        var redactor = Redactor.For([Secret.Create("token", "hunter2")]);
        var routed = Prepare(new Observation { Route = "/reset?token=hunter2", Roots = [new SemanticNode { Ref = "root" }] }, redactor);
        Assert.Null(routed.Location);
        Assert.Equal("/reset?token=<secret:token>", routed.Path);
        var located = Prepare(new Observation { Route = "/", Location = "https://app.test/reset?token=hunter2#hunter2", Roots = [new SemanticNode { Ref = "root" }] }, redactor);
        Assert.Equal("https://app.test/reset?token=<secret:token>#<secret:token>", located.Location);
        Assert.DoesNotContain("hunter2", located.Path, StringComparison.Ordinal);
        Assert.StartsWith("/reset?token=", located.Path, StringComparison.Ordinal);
    }

    [Fact]
    public void Collapses_a_field_whose_collapsed_form_shows_a_secret_its_written_form_hides_so_no_later_reader_brings_it_back()
    {
        var tree = new SemanticNode
        {
            Ref = "n1",
            Children = [new SemanticNode { Ref = "n2", Role = "heading", Name = "say correct\n\n  horse battery   staple now", TestId = "correct\t\thorse battery staple" }],
        };
        var redactor = Redactor.For([Secret.Create("phrase", "correct horse battery staple")]);
        var prepared = Prepare(new Observation { Route = "/", Roots = [tree] }, redactor);
        Assert.Equal("say <secret:phrase> now", prepared.Nodes["n2"].Name);
        Assert.Equal("<secret:phrase>", prepared.Nodes["n2"].TestId);
        // The anchors read the redacted nodes, as upstream's describeDelta reads the node index.
        var anchors = Anchors.Describe([], Anchors.Project(new Observation { Route = "/", Roots = prepared.Tree }, value => value, value => value), false).Appeared;
        Assert.DoesNotContain("horse", Json(anchors), StringComparison.Ordinal);
    }

    // upstream's toEqual(tree.children[..]) of a node nothing redacted: every field the node carries reads as it was.
    private static void AssertUntouched(SemanticNode expected, SemanticNode actual)
    {
        Assert.Equal(expected.Ref, actual.Ref);
        Assert.Equal(expected.Role, actual.Role);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Text, actual.Text);
        Assert.Equal(expected.Value, actual.Value);
        Assert.Equal(expected.Selection, actual.Selection);
        Assert.Equal(expected.TestId, actual.TestId);
        Assert.Equal(expected.States, actual.States);
        Assert.Equal(expected.Selector, actual.Selector);
        Assert.Equal(expected.FramePath, actual.FramePath);
    }

    [Fact]
    public void Redacts_the_leading_part_of_a_secret_a_field_cut_at_its_limit_ends_with_and_leaves_a_cut_plain_value_and_a_whole_field_alone()
    {
        const string Token = "cut-secret-Kq7ZrT2mWx9pLd4sNv8bHc3jFg6yQa1eUo5iRk0tYw2zXn7uM";
        var redactor = Redactor.For([Secret.Create("apiKey", Token)]);
        static string CutText(string tail) => new string('0', ObservationLimits.Text - tail.Length) + tail;
        var kept = Token[..59];
        var tree = new SemanticNode
        {
            Ref = "n1",
            Children =
            [
                new SemanticNode { Ref = "n2", Role = "paragraph", Text = CutText(kept) },
                new SemanticNode { Ref = "n3", Role = "button", Name = new string('0', ObservationLimits.Name - 40) + Token[..40] },
                new SemanticNode { Ref = "n4", Role = "textbox", Value = CutText(Token[..20]), Selection = CutText(Token[..12]) },
                new SemanticNode { Ref = "n5", Role = "paragraph", Text = CutText("plain-control-plain") },
                new SemanticNode { Ref = "n6", Role = "paragraph", Text = "whole " + Token[..20] },
                new SemanticNode { Ref = "n7", Role = "textbox", Value = "0" + CutText(Token[..20]) },
            ],
        };
        var prepared = Prepare(new Observation { Route = "/", Roots = [tree] }, redactor, 16_384);
        var text = prepared.Text;
        var projected = global::E2E.AgentObservation.ProjectTree(prepared.Tree, redactor, null).Single().Children;

        var cutLines = text.Split('\n').Where(line => line.StartsWith(" #n2 ", StringComparison.Ordinal) || line.StartsWith(" #n3 ", StringComparison.Ordinal) || line.StartsWith(" #n4 ", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, cutLines.Count);
        foreach (var line in cutLines)
        {
            Assert.DoesNotContain(Token[..12], line, StringComparison.Ordinal);
        }

        // The textbox's value holds the secret, so its selection is withheld rather than redacted.
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(string.Join('\n', cutLines), "<secret:apiKey>").Count);
        Assert.Null(prepared.Nodes["n4"].Selection);
        Assert.Null(projected[2].Selection);
        Assert.Equal(new string('0', ObservationLimits.Text - kept.Length) + "<secret:apiKey>", prepared.Nodes["n2"].Text);
        Assert.DoesNotContain(Token[..12], Json(new[] { "n2", "n3", "n4" }.Select(id => prepared.Nodes[id])), StringComparison.Ordinal);
        Assert.DoesNotContain(Token[..12], Json(projected.Take(3)), StringComparison.Ordinal);
        Assert.Same(prepared.Tree[0].Children[0], prepared.Nodes["n2"]);
        Assert.Equal(CutText("plain-control-plain"), prepared.Nodes["n5"].Text);
        // A field under its limit was not cut, so a fragment inside it is only app text.
        Assert.Equal("whole " + Token[..20], prepared.Nodes["n6"].Text);
        AssertUntouched(tree.Children[4], prepared.Nodes["n6"]);
        // A value longer than the limit is a native control's, which no engine cuts.
        AssertUntouched(tree.Children[5], prepared.Nodes["n7"]);
    }

    /// <summary>
    /// Partial: upstream "redacts every string field a node carries, in the text, the executor tree, and the node index alike" also checks
    /// that a field the contract does not name is dropped (its <c>extra</c> key on a node); a <c>SemanticNode</c> has no such field to carry, so
    /// that part has no C# counterpart. Everything else it checks is here: the role, the attribute names and values, the test id,
    /// <c>selector</c>, and <c>framePath</c> in the text, the executor tree, and the node index.
    /// </summary>
    [Fact]
    public void Partial_redacts_every_string_field_a_node_carries_in_the_text_the_executor_tree_and_the_node_index_alike()
    {
        const string Token = "field-secret-Kq7ZrT2mWx9pLd4sNv8bHc3jFg6y";
        var redactor = Redactor.For([Secret.Create("probe", Token)]);
        var tree = new SemanticNode
        {
            Ref = "n1",
            Role = "document",
            Children =
            [
                new SemanticNode
                {
                    Ref = "n2",
                    Role = "button",
                    Name = "Go",
                    TestId = Token,
                    Placeholder = Token,
                    Selector = $"[data-testid=\"{Token}\"]",
                    Attributes = new Dictionary<string, string> { ["data-" + Token] = Token, ["placeholder"] = Token },
                },
                new SemanticNode { Ref = "n3", Role = Token, Name = "Custom" },
                new SemanticNode
                {
                    Ref = "n4",
                    Role = "iframe",
                    Name = "Frame",
                    Children = [new SemanticNode { Ref = "n5", Role = "button", Name = "Inner", FramePath = [$"iframe[name=\"{Token}\"]"] }],
                },
                new SemanticNode { Ref = "n6", Role = "status", Name = "Extra" },
            ],
        };
        var prepared = Prepare(new Observation { Route = "/", Roots = [tree] }, redactor, 16_384);
        var projected = global::E2E.AgentObservation.ProjectTree(tree.Children, redactor, null);
        Assert.Contains("testid=\"<secret:probe>\"", prepared.Text, StringComparison.Ordinal);
        Assert.Equal(new Dictionary<string, string> { ["data-<secret:probe>"] = "<secret:probe>", ["placeholder"] = "<secret:probe>" }, prepared.Nodes["n2"].Attributes);
        Assert.Equal(["iframe[name=\"<secret:probe>\"]"], prepared.Nodes["n5"].FramePath);
        Assert.Equal(["iframe[name=\"<secret:probe>\"]"], projected[2].Children[0].FramePath);
        Assert.Equal("[data-testid=\"<secret:probe>\"]", prepared.Nodes["n2"].Selector);
        Assert.Equal("<secret:probe>", prepared.Nodes["n3"].Role);
        AssertUntouched(tree.Children[3], prepared.Nodes["n6"]);
        Assert.Equal(new Dictionary<string, string> { ["data-<secret:probe>"] = "<secret:probe>", ["placeholder"] = "<secret:probe>" }, global::E2E.AgentObservation.ProjectTree([tree], redactor, null).Single().Children[0].Attributes);
        Assert.Equal("<secret:probe>", projected[1].Role);
        Assert.Equal("<secret:probe>", global::E2E.Cache.TraceRecorder.DescribeTarget(tree.Children[1], redactor).Role);
        foreach (var shown in new[] { prepared.Text, Json(global::E2E.AgentObservation.ProjectTree(prepared.Tree, redactor, null)), Json(prepared.Nodes.Values) })
        {
            Assert.DoesNotContain(Token, shown, StringComparison.Ordinal);
            Assert.DoesNotContain(Token[..12], shown, StringComparison.Ordinal);
        }
    }
}
