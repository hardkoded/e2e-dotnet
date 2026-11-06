// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Engine;
using E2E.Internal;

namespace E2E.Tests.AgentObservation;

/// <summary>
/// The nested <c>describe.each</c> of <c>describe('prepareObservation')</c> in <c>agent-observation.test.ts</c>: a secret an engine collapsed, then cut at the name and text limits.
/// </summary>
public sealed class CollapsedSecretCutTests
{
    private static readonly System.Text.Json.JsonSerializerOptions Relaxed = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static string Json<T>(T value) => System.Text.Json.JsonSerializer.Serialize(value, Relaxed);

    /// <summary>
    /// Partial: upstream <c>it.each</c> "keeps no part of it when the cut falls %s, in the text, the tree, or the anchors" in <c>describe.each</c> "a secret of %s an engine collapsed, then cut at the name and text limits" (6 secrets x 3 cut positions = 18 cases) reads the
    /// text, the executor tree and the anchors from <c>prepareObservation</c> and <c>describeDelta</c>. The port has no <c>PreparedObservation</c>:
    /// it renders the text, projects the tree with <c>ProjectTree</c>, and projects the anchors with <c>Anchors.Project</c> as the step cache does.
    /// </summary>
    [Theory]
    [MemberData(nameof(CollapsedSecretCuts))]
    public void Partial_keeps_no_part_of_it_when_the_cut_falls_in_the_text_the_tree_or_the_anchors(string kind, string lead, string separator, string tail, string where, int kept)
    {
        var label = $"a secret of {kind} an engine collapsed, then cut {where}";
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
        Assert.Equal(ObservationLimits.Text, Cut(ObservationLimits.Text).Length);
        var observation = new Observation { Route = "/", Roots = [tree] };
        var text = SnapshotText.Render(observation, redactor);
        var projected = global::E2E.AgentObservation.ProjectTree([tree], redactor, null);
        var anchors = Anchors.Describe(
            [],
            Anchors.Project(observation, redactor.Redact, redactor.Redact, (value, limit) => global::E2E.AgentObservation.RedactText(value, redactor, limit)),
            false).Appeared;
        Assert.True(string.Join(' ', anchors.Select(anchor => anchor.Name ?? anchor.Text)).Contains("<secret:multi>", StringComparison.Ordinal), label + ": the anchors lose the secret's name");
        string[] fragments = [lead, lead[..8], collapsed[..30], tail[..8]];
        foreach (var shown in new[] { text, Json(projected), Json(anchors) })
        {
            Assert.True(shown.Contains("<secret:multi>", StringComparison.Ordinal), label + ": no marker in " + shown);
            foreach (var fragment in fragments)
            {
                Assert.True(!shown.Contains(fragment, StringComparison.Ordinal), $"{label}: '{fragment}' survives in {shown}");
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
