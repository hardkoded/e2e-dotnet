// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using E2E.Engine;

namespace E2E.Tests.Locators;

/// <summary>
/// Upstream's <c>describe('projectExpression')</c> (<c>packages/web/tests/unit/locators.test.ts</c>): which compositions land on Playwright's
/// own locator chain, which ones a display-value query defers until its candidates are read, and which ones the engine cannot express at
/// all. The chain is recorded by <see cref="ChainRecording"/>. Spellings the port reads differently from upstream, in every test below:
/// a role query is the <c>internal:role=</c> selector (Microsoft.Playwright cannot send the <c>u</c> flag of the name pattern through
/// <c>getByRole</c>), so the step reads <c>locator(internal:role=...)</c> where upstream's reads <c>role(...)</c>; and <c>first()</c> is
/// <c>nth(0)</c> here, so a position 0 is the <c>first</c> step.
/// </summary>
public sealed class ProjectExpressionTests
{
    /// <summary>The step every semantic query part starts from: its scope plus the closed shadow roots under it.</summary>
    private const string Roots = "locator(e2e-roots=)";

    private const string ValuedCandidates = "locator(e2e-roots=input:not([type=\"checkbox\" i]):not([type=\"radio\" i]), textarea, select)";

    private static readonly LocatorExpression Shared = new() { Kind = "displayValue", Value = "shared" };

    private static readonly LocatorExpression Textbox = new() { Kind = "role", Role = "textbox" };

    /// <summary>Projects with the default test-id attribute; the attribute itself is exercised in its own case.</summary>
    private static ProjectedLocator Project(LocatorExpression expression) =>
        WebLocators.ProjectExpression(ChainRecording.Page(), expression, new ProjectionOptions("data-testid", FrameHidden: false));

    /// <summary>Projects with a test-id attribute of the case's own.</summary>
    private static ProjectedLocator ProjectWithTestId(LocatorExpression expression, string testIdAttribute) =>
        WebLocators.ProjectExpression(ChainRecording.Page(), expression, new ProjectionOptions(testIdAttribute, FrameHidden: false));

    private static IReadOnlyList<string> Chain(Microsoft.Playwright.ILocator locator) => ChainRecording.ChainOf(locator);

    /// <summary>The step an exact role name projects to: the name with icon-font glyphs allowed around its words.</summary>
    private static string ExactRoleName(string role, string name, string options = "") =>
        $"locator(internal:role={role}{options}[name=/^[\\s\\p{{Co}}]*{name}[\\s\\p{{Co}}]*$/u])";

    [Fact]
    public void Projects_an_exact_label_query_onto_the_label_engine_with_a_label_predicate_and_composes_exactly()
    {
        var label = new LocatorExpression { Kind = "label", Value = "Display name" };
        var projected = Project(label);
        Assert.Equal("Display name", projected.Name!.Value.Match.Text);
        Assert.True(projected.Name.Value.Exact);
        // The label engine runs the reader in the page, with the reader's options, so only the labelled controls are
        // candidates; alone and in composition alike.
        const string ExactLabel =
            """locator(e2e-label={"value":"Display name","testIdAttribute":"data-testid","secureFieldSelector":"input[type=password i], input[autocomplete=current-password]"})""";
        Assert.Equal([ExactLabel], Chain(projected.Locator));
        Assert.Equal([ExactLabel], Chain(projected.Composable!));
        // A position waits for the predicate and also narrows the composable locator.
        var firstLabel = new LocatorExpression { Kind = "index", Source = label, Index = 0 };
        var first = Project(firstLabel);
        Assert.Equal([new PostStep.Position(0)], first.Steps);
        Assert.Equal([ExactLabel, "first"], Chain(first.Composable!));
        var scoped = Project(Textbox with { Parent = firstLabel });
        Assert.Equal([ExactLabel, "first", Roots, "locator(internal:role=textbox)"], Chain(scoped.Locator));
        var rows = Project(new LocatorExpression { Kind = "filter", Source = Textbox, Has = label });
        Assert.Equal([Roots, "locator(internal:role=textbox)", $"filter(has={ExactLabel})"], Chain(rows.Locator));
        var without = Project(new LocatorExpression { Kind = "filter", Source = Textbox, HasNot = label });
        Assert.Equal([Roots, "locator(internal:role=textbox)", $"filter(hasNot={ExactLabel})"], Chain(without.Locator));
        var both = Project(new LocatorExpression { Kind = "filter", Source = Textbox, Has = label, HasNot = label });
        Assert.Equal([Roots, "locator(internal:role=textbox)", $"filter(has={ExactLabel},hasNot={ExactLabel})"], Chain(both.Locator));
        // A substring label query keeps Playwright's own matching.
        var loose = Project(new LocatorExpression { Kind = "label", Value = "Display", Exact = false });
        Assert.Null(loose.Name);
    }

    [Fact]
    public void Narrows_a_visible_query_inside_the_selector_for_every_kind_CSS_candidates_included()
    {
        var visibleLabel = new LocatorExpression { Kind = "label", Value = "Display name", Visible = true };
        var projected = Project(visibleLabel);
        var chain = Chain(projected.Locator);
        Assert.Equal(2, chain.Count);
        Assert.StartsWith("locator(e2e-label=", chain[0], StringComparison.Ordinal);
        Assert.Equal("locator(e2e-shown=)", chain[1]);
        Assert.True(projected.Visible);
        // The composable form narrows the same way, so a has filter on a visible label drops a hidden control.
        Assert.Equal(chain, Chain(projected.Composable!));
        var visibleValue = new LocatorExpression { Kind = "displayValue", Value = "ada", Visible = true };
        Assert.Equal([ValuedCandidates, "locator(e2e-shown=)"], Chain(Project(visibleValue).Locator));
        var visibleText = new LocatorExpression { Kind = "text", Value = "Save", Visible = true };
        Assert.Equal([Roots, "text(Save,exact)", "locator(e2e-shown=)"], Chain(Project(visibleText).Locator));
    }

    [Fact]
    public void Narrows_a_visible_or_role_query_inside_a_hidden_frame_to_nothing_wherever_it_sits_and_leaves_other_queries_alone()
    {
        static LocatorExpression Button(bool visible = false) => new() { Kind = "role", Role = "button", Visible = visible };
        static IReadOnlyList<string> InHiddenFrame(LocatorExpression expression) =>
            ChainRecording.ChainOf(WebLocators.ProjectExpression(ChainRecording.Page(), expression, new ProjectionOptions("data-testid", FrameHidden: true)).Locator);
        Assert.Equal([Roots, "locator(internal:role=button)", "locator(css=:not(*))"], InHiddenFrame(Button(visible: true)));
        Assert.Equal(
            [Roots, "locator(internal:role=button)", "locator(css=:not(*))", "first"],
            InHiddenFrame(new LocatorExpression { Kind = "index", Source = Button(visible: true), Index = 0 }));
        Assert.Equal([Roots, "locator(internal:role=button)", "locator(css=:not(*))"], InHiddenFrame(Button()));
        var text = new LocatorExpression { Kind = "text", Value = "Save" };
        Assert.Equal([Roots, "text(Save,exact)"], InHiddenFrame(text));
        Assert.Equal([Roots, "text(Save,exact)", "locator(css=:not(*))"], InHiddenFrame(text with { Visible = true }));
    }

    [Fact]
    public void Projects_a_testId_query_onto_the_configured_attribute_encoded_as_getByTestId_encodes_it()
    {
        static LocatorExpression TestId(TextMatch value, bool exact = true) => new() { Kind = "testid", Value = value, Exact = exact };
        var exact = ProjectWithTestId(TestId("save \"draft\""), "data-qa");
        Assert.Equal([Roots, "locator(internal:testid=[data-qa=\"save \\\"draft\\\"\"s])"], Chain(exact.Locator));
        // A string is always an exact match, whatever `exact` says: getByTestId has no substring mode.
        var loose = ProjectWithTestId(TestId("save", exact: false), "data-qa");
        Assert.Equal([Roots, "locator(internal:testid=[data-qa=\"save\"s])"], Chain(loose.Locator));
        var pattern = ProjectWithTestId(TestId(new Regex(@"^row-\d+$", RegexOptions.IgnoreCase)), "data-test-id");
        Assert.Equal([Roots, @"locator(internal:testid=[data-test-id=/^row-\d+$/i])"], Chain(pattern.Locator));
        // An attribute with a comma is quoted, as Playwright quotes it.
        var quoted = ProjectWithTestId(TestId("x"), "a,b");
        Assert.Equal([Roots, "locator(internal:testid=[\"a,b\"=\"x\"s])"], Chain(quoted.Locator));
    }

    [Fact]
    public void Passes_role_states_and_the_heading_level_through_to_getByRole()
    {
        var heading = new LocatorExpression { Kind = "role", Role = "heading", Value = "Dashboard", Level = 1 };
        Assert.Equal([Roots, ExactRoleName("heading", "Dashboard", "[level=1]")], Chain(Project(heading).Locator));
        var checkbox = new LocatorExpression { Kind = "role", Role = "checkbox", Checked = true, Disabled = false };
        Assert.Equal([Roots, "locator(internal:role=checkbox[checked=true][disabled=false])"], Chain(Project(checkbox).Locator));
    }

    [Fact]
    public void Spells_the_contract_image_role_as_the_img_role_the_selector_knows()
    {
        var image = new LocatorExpression { Kind = "role", Role = "image", Value = "Map" };
        Assert.Equal([Roots, ExactRoleName("img", "Map")], Chain(Project(image).Locator));
        var tablist = new LocatorExpression { Kind = "role", Role = "tablist" };
        Assert.Equal([Roots, "locator(internal:role=tablist)"], Chain(Project(tablist).Locator));
    }

    [Fact]
    public void Defers_positions_on_a_displayValue_query_until_its_candidates_are_value_filtered()
    {
        var projected = Project(new LocatorExpression
        {
            Kind = "index",
            Source = new LocatorExpression { Kind = "index", Source = Shared, Index = LocatorExpression.LastIndex },
            Index = 0,
        });
        Assert.Equal([ValuedCandidates], Chain(projected.Locator));
        Assert.Equal("shared", projected.DisplayValue!.Value.Match.Text);
        Assert.True(projected.DisplayValue.Value.Exact);
        Assert.Equal([new PostStep.Position(LocatorExpression.LastIndex), new PostStep.Position(0)], projected.Steps);
    }

    [Fact]
    public void Composes_per_element_filters_onto_the_displayValue_candidate_locator()
    {
        var projected = Project(new LocatorExpression
        {
            Kind = "index",
            Source = new LocatorExpression { Kind = "filter", Source = Shared, HasText = "x", Has = Textbox },
            Index = 0,
        });
        Assert.Equal([ValuedCandidates, $"filter(hasText=x,has={Roots}>locator(internal:role=textbox))"], Chain(projected.Locator));
        Assert.NotNull(projected.DisplayValue);
        Assert.Equal([new PostStep.Position(0)], projected.Steps);
    }

    [Fact]
    public void Rejects_a_displayValue_query_as_a_scope_a_has_filter_or_a_hasNot_filter_and_names_only_those_cases()
    {
        const string Message = "displayValue queries cannot scope child queries or serve as a has or hasNot filter in this engine";
        foreach (var expression in new[]
        {
            Textbox with { Parent = Shared },
            new LocatorExpression { Kind = "filter", Source = Textbox, Has = Shared },
            new LocatorExpression { Kind = "filter", Source = Textbox, HasNot = Shared },
        })
        {
            var thrown = Assert.Throws<EngineException>(() => Project(expression));
            Assert.Equal("UNSUPPORTED_CAPABILITY", thrown.Code);
            Assert.Equal(Message, thrown.Message);
        }
    }

    [Fact]
    public void Defers_a_filter_placed_after_a_position_on_a_displayValue_query_to_the_selected_element()
    {
        var projected = Project(new LocatorExpression
        {
            Kind = "filter",
            Source = new LocatorExpression { Kind = "index", Source = Shared, Index = 0 },
            HasText = "x",
            Has = Textbox,
        });
        Assert.Equal([ValuedCandidates], Chain(projected.Locator));
        Assert.Equal(2, projected.Steps.Count);
        Assert.Equal(new PostStep.Position(0), projected.Steps[0]);
        var filter = Assert.IsType<PostStep.Filter>(projected.Steps[1]);
        Assert.Equal("x", filter.Options.HasText);
        Assert.Equal([Roots, "locator(internal:role=textbox)"], Chain(filter.Options.Has!));
    }

    [Fact]
    public void Still_rejects_a_displayValue_has_filter_after_a_position()
    {
        var thrown = Assert.Throws<EngineException>(() => Project(new LocatorExpression
        {
            Kind = "filter",
            Source = new LocatorExpression { Kind = "index", Source = Textbox, Index = 0 },
            Has = Shared,
        }));
        Assert.Equal("UNSUPPORTED_CAPABILITY", thrown.Code);
    }

    [Fact]
    public void Projects_a_union_onto_Playwright_or_and_rejects_a_displayValue_branch()
    {
        var button = new LocatorExpression { Kind = "role", Role = "button" };
        var projected = Project(new LocatorExpression { Kind = "union", Left = Textbox, Right = button });
        Assert.Equal([Roots, "locator(internal:role=textbox)", $"or({Roots}>locator(internal:role=button))"], Chain(projected.Locator));
        Assert.Empty(projected.Steps);
        var thrown = Assert.Throws<EngineException>(() => Project(new LocatorExpression { Kind = "union", Left = Shared, Right = Textbox }));
        Assert.Equal("UNSUPPORTED_CAPABILITY", thrown.Code);
        Assert.False(thrown.Retryable);
    }
}
