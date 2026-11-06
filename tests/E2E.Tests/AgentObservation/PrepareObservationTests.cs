// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E.Tests.AgentObservation;

/// <summary>
/// The port renders the screen as one text block in its own format, with no PreparedObservation (kind, node index, tree, path, bytes), no
/// pixel evidence beside the tree, and no <c>redactNodesAgain</c>. So these upstream its are not ported: "keeps unavailable semantics separate from
/// an empty tree and requires permitted masked evidence", "returns evidence without a comparable shape without repeating capture" (the settle loop
/// always has a shape), and "redacts an earlier capture again with a secret resolved since, keeping which nodes are leaves". The three
/// <c>Partial_</c> redaction tests at the end port the others against the rendered text, <c>ProjectTree</c>, and the anchors. The pixel tests
/// Grants_pixels_that_cover_every_secure_node_sized_for_the_model and Withholds_pixels_the_engine_cannot_count_the_masks_of_or_cannot_capture
/// are .NET-only: upstream proves a granted or uncounted capture through its runner.
/// </summary>
public sealed class PrepareObservationTests
{
    private static readonly System.Text.Json.JsonSerializerOptions Relaxed = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static string Json<T>(T value) => System.Text.Json.JsonSerializer.Serialize(value, Relaxed);

    /// <summary>Partial: upstream "carries an engine-reported cut through as truncated" checks its own marker and format. The port renders the screen in its own format and marker, and counts no bytes or nodes beside the text.</summary>
    [Fact]
    public void Partial_carries_an_engine_reported_cut_through_as_truncated_with_the_ports_marker()
    {
        var tree = new SemanticNode { Ref = "n1", Role = "document", Children = [new SemanticNode { Ref = "n2", Role = "button", Name = "One" }] };
        var text = SnapshotText.Render(new Observation { Route = "/", Roots = [tree], Truncated = true }, Redactor.None);
        Assert.Contains("button \"One\"", text, StringComparison.Ordinal);
        Assert.EndsWith("\n[observation truncated: the engine stopped listing nodes at its limit]", text, StringComparison.Ordinal);
    }

    /// <summary>Partial: upstream "truncates at the byte limit while keeping the root and flagging truncation" reads the flag, the node index (201 nodes), and the text from <c>prepareObservation</c>. The port has no node index: it checks the text and the cut flag of <c>Render</c>.</summary>
    [Fact]
    public void Partial_truncates_at_the_byte_limit_while_keeping_the_root_and_flagging_truncation()
    {
        var children = Enumerable.Range(0, 200).Select(index => new SemanticNode { Ref = "c" + index, Role = "button", Name = "Button number " + index }).ToList();
        var tree = new SemanticNode { Ref = "n1", Role = "document", Children = children };
        var text = SnapshotText.Render(new Observation { Route = "/", Roots = [tree] }, Redactor.None, null, 256, out var cut);
        Assert.True(cut);
        Assert.StartsWith("#n1 document", text, StringComparison.Ordinal);
        Assert.Contains("[observation truncated", text, StringComparison.Ordinal);
    }

    /// <summary>Partial: upstream "reports the byte cut, not the engine cut, when both apply" reads <c>prepared.bytes</c> and the engine marker's words. The port's engine marker is its own, and it counts the text's bytes itself.</summary>
    [Fact]
    public void Partial_reports_the_byte_cut_not_the_engine_cut_when_both_apply()
    {
        var children = Enumerable.Range(0, 200).Select(index => new SemanticNode { Ref = "c" + index, Role = "button", Name = "Button " + index }).ToList();
        var tree = new SemanticNode { Ref = "n1", Role = "document", Children = children };
        var text = SnapshotText.Render(new Observation { Route = "/", Roots = [tree], Truncated = true }, Redactor.None, null, 256, out var cut);
        Assert.True(cut);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(text) <= 256);
        Assert.EndsWith("[observation truncated at the resolved observation byte limit]", text, StringComparison.Ordinal);
        Assert.DoesNotContain("off screen", text, StringComparison.Ordinal);
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

    /// <summary>Partial: the port reads the route from <c>Routes.LocationOf</c>, and a bare word becomes a rooted path.</summary>
    [Theory]
    [InlineData("https://app.example.test/companies?search=a#/orders/42", "/companies?search=a#/orders/42")]
    [InlineData("https://app.example.test/companies#top", "/companies#top")]
    [InlineData("Settings", "/Settings")]
    public void Partial_keeps_the_whole_address_after_the_origin_as_the_path_fragment_included(string location, string route)
    {
        Assert.Equal(route, E2E.Internal.Routes.LocationOf(location));
    }

    /// <summary>Partial: the port renders the name and text, with no test id, anchors, or node index to read back.</summary>
    [Fact]
    public void Collapses_a_name_so_a_secret_its_written_form_hides_is_replaced()
    {
        var tree = new SemanticNode
        {
            Ref = "n1",
            Children = [new SemanticNode { Ref = "n2", Role = "heading", Name = "say correct\n\n  horse battery   staple now", TestId = "correct\t\thorse battery staple" }],
        };
        var text = SnapshotText.Render(new Observation { Route = "/", Roots = [tree] }, Redactor.For([Secret.Create("phrase", "correct horse battery staple")]));
        Assert.Contains("heading \"say <secret:phrase> now\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("horse", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Partial: upstream "redacts the leading part of a secret a field cut at its limit ends with, and leaves a cut plain value and a whole field alone"
    /// reads the same fields from <c>prepareObservation</c>'s text, its executor tree, and its node index. The port has no node index and no
    /// <c>PreparedObservation</c>: it checks the rendered text and <c>ProjectTree</c>.
    /// </summary>
    [Fact]
    public void Partial_redacts_the_leading_part_of_a_secret_a_field_cut_at_its_limit_ends_with_and_leaves_a_cut_plain_value_and_a_whole_field_alone()
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
        var text = SnapshotText.Render(new Observation { Route = "/", Roots = [tree] }, redactor);
        var projected = global::E2E.AgentObservation.ProjectTree([tree], redactor, null).Single().Children;

        var cutLines = text.Split('\n').Where(line => line.StartsWith(" #n2 ", StringComparison.Ordinal) || line.StartsWith(" #n3 ", StringComparison.Ordinal) || line.StartsWith(" #n4 ", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, cutLines.Count);
        foreach (var line in cutLines)
        {
            Assert.DoesNotContain(Token[..12], line, StringComparison.Ordinal);
        }

        // The textbox's value holds the secret, so its selection is withheld rather than redacted.
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(string.Join('\n', cutLines), "<secret:apiKey>").Count);
        Assert.Null(projected[2].Selection);
        Assert.Equal(new string('0', ObservationLimits.Text - kept.Length) + "<secret:apiKey>", projected[0].Text);
        Assert.DoesNotContain(Token[..12], Json(projected.Take(3)), StringComparison.Ordinal);
        Assert.Equal(CutText("plain-control-plain"), projected[3].Text);
        // A field under its limit was not cut, so a fragment inside it is only app text.
        Assert.Equal("whole " + Token[..20], projected[4].Text);
        // A value longer than the limit is a native control's, which no engine cuts.
        Assert.Equal("0" + CutText(Token[..20]), projected[5].Value);
    }

    /// <summary>
    /// Partial: upstream "redacts every string field a node carries, in the text, the executor tree, and the node index alike" also checks
    /// <c>selector</c>, <c>framePath</c>, an unknown extra field, and a secret used as a role, in a node index. The port has no node index, no
    /// selectors and no frame paths, and renders a node's role as the engine reported it. It checks the test id, the attributes and the placeholder in the text and the executor tree.
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
                    Attributes = new Dictionary<string, string> { ["placeholder"] = Token, ["title"] = Token },
                },
            ],
        };
        var text = SnapshotText.Render(new Observation { Route = "/", Roots = [tree] }, redactor);
        var projected = global::E2E.AgentObservation.ProjectTree([tree], redactor, null);
        Assert.Contains("testid=\"<secret:probe>\"", text, StringComparison.Ordinal);
        var attributes = projected.Single().Children.Single().Attributes!;
        Assert.Equal("<secret:probe>", attributes["placeholder"]);
        Assert.Equal("<secret:probe>", attributes["title"]);
        foreach (var shown in new[] { text, Json(projected) })
        {
            Assert.DoesNotContain(Token, shown, StringComparison.Ordinal);
            Assert.DoesNotContain(Token[..12], shown, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Partial: upstream <c>describe.each</c> "a secret of %s an engine collapsed, then cut at the name and text limits" (6 secrets x 3 cut positions = 18 cases) reads the
    /// text, the executor tree and the anchors from <c>prepareObservation</c> and <c>describeDelta</c>. The port has no <c>PreparedObservation</c>:
    /// it renders the text, projects the tree with <c>ProjectTree</c>, and projects the anchors with <c>Anchors.Project</c> as the step cache does.
    /// </summary>
    [Theory]
    [MemberData(nameof(CollapsedSecretCuts))]
    public void Partial_keeps_no_part_of_a_secret_an_engine_collapsed_then_cut_at_the_name_and_text_limits(string kind, string lead, string separator, string tail, string where, int kept)
    {
        Assert.NotEmpty(kind);
        Assert.NotEmpty(where);
        var redactor = Redactor.For([Secret.Create("multi", lead + separator + tail)]);
        var collapsed = lead + " " + tail;
        string Cut(int limit) => new string('x', limit - kept - 1) + " " + collapsed[..kept];
        var tree = new SemanticNode
        {
            Ref = "n1",
            Role = "document",
            Children =
            [
                new SemanticNode { Ref = "n2", Role = "heading", Name = Cut(ObservationLimits.Name) },
                new SemanticNode { Ref = "n3", Text = Cut(ObservationLimits.Text) },
            ],
        };
        Assert.Equal(ObservationLimits.Name, Cut(ObservationLimits.Name).Length);
        var observation = new Observation { Route = "/", Roots = [tree] };
        var text = SnapshotText.Render(observation, redactor);
        var projected = global::E2E.AgentObservation.ProjectTree([tree], redactor, null);
        var anchors = Anchors.Describe(
            [],
            Anchors.Project(observation, redactor.Redact, redactor.Redact, (value, limit) => global::E2E.AgentObservation.RedactText(value, redactor, limit)),
            false).Appeared;
        Assert.Contains("<secret:multi>", string.Join(' ', anchors.Select(anchor => anchor.Name ?? anchor.Text)), StringComparison.Ordinal);
        string[] fragments = [lead, lead[..8], collapsed[..30], tail[..8]];
        foreach (var shown in new[] { text, Json(projected), Json(anchors) })
        {
            Assert.Contains("<secret:multi>", shown, StringComparison.Ordinal);
            foreach (var fragment in fragments)
            {
                Assert.DoesNotContain(fragment, shown, StringComparison.Ordinal);
            }
        }
    }

    public static TheoryData<string, string, string, string, string, int> CollapsedSecretCuts()
    {
        const string Letters = "leadqzrtmwxplkdsnvbhcjfg";
        const string LettersTail = "tailyqaeuoirktywzxnumbvcxzlkjhgfdsapoiuytrewqmnbvcxzlkj";
        (string Kind, string Lead, string Separator, string Tail)[] secrets =
        [
            ("letters with a line break", Letters, "\n", LettersTail),
            ("hex with a line break", "0f3a9c71e4b2d85601aa7c3e", "\n", "9b84f0c2d17e6a35b9f04c8d2e71a6b3c95d08f4e2a7b61c3d9e0f5"),
            ("letters with a tab", Letters, "\t", LettersTail),
            ("letters with a CRLF", Letters, "\r\n", LettersTail),
            ("letters with a no-break space", Letters, "\u00A0", LettersTail),
            ("letters with repeated spaces", Letters, "   ", LettersTail),
        ];
        var data = new TheoryData<string, string, string, string, string, int>();
        foreach (var (kind, lead, separator, tail) in secrets)
        {
            data.Add(kind, lead, separator, tail, "in the second part", lead.Length + 15);
            data.Add(kind, lead, separator, tail, "on the collapsed space", lead.Length + 1);
            data.Add(kind, lead, separator, tail, "in the first part", 10);
        }

        return data;
    }
}
