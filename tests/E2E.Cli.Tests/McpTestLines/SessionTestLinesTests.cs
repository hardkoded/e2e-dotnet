// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Mcp;
using E2E.Cli.Tests.Helpers;
using E2E.Engine;
using static E2E.Cli.Tests.McpTestLines.TestLineHarness;

namespace E2E.Cli.Tests.McpTestLines;

/// <summary>
/// The test line a session action answers with: built from the target as the newest observation listed it, tried against the live
/// screen (here resolved by the port's locator resolver), printed only when it reaches the target alone, narrowed when it reaches
/// twins too, and collected by <c>test_lines</c>.
/// </summary>
public sealed class SessionTestLinesTests
{
    private static readonly Fake Save = new("n2", "button", "Save", Y: 40);

    [Fact]
    public async Task Answers_every_grammar_action_so_a_new_verb_needs_a_line_or_a_reason()
    {
        var engine = new FakeEngine();
        await using var context = await SessionContext.StartAsync(engine);
        const EngineCapabilities all = EngineCapabilities.Observation | EngineCapabilities.Actions | EngineCapabilities.Location | EngineCapabilities.Keyboard
            | EngineCapabilities.Scroll | EngineCapabilities.History | EngineCapabilities.Screenshot | EngineCapabilities.TapModifiers;
        var catalog = SessionCatalog.Create(context, all, secrets: ["apiKey"], projectRoot: "/project", attemptDir: "/project/.e2e/results/attempt");

        // Only what looks at the screen or reads the draft prints no line.
        Assert.Equal(["observe", "screenshot", "locate", "test_lines"], catalog.Entries.Select(pair => pair.Key).Where(name => !SessionTestLines.HasLine(name)));
        Assert.Equal(
            ["tap", "double_tap", "type", "press", "select", "check", "uncheck", "clear", "scroll_to", "scroll", "navigate", "back", "type_secret"],
            catalog.Entries.Select(pair => pair.Key).Where(SessionTestLines.HasLine));
    }

    [Fact]
    public async Task Answers_an_action_with_its_line_after_the_lead_and_test_lines_collects_it()
    {
        var session = new TestLineHarness([Save]);
        var text = await session.RunAsync("tap", """{ "target": "n2" }""");
        Assert.Matches(@"^done #tap\nTest line: await Screen\.GetByRole\(""button"", ""Save""\)\.TapAsync\(\);\n\n", text);
        var draft = session.Lines.Draft();
        Assert.Contains("\nawait Screen.GetByRole(\"button\", \"Save\").TapAsync();\n", draft, StringComparison.Ordinal);
        Assert.Contains("(1 lines)", draft, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Falls_back_to_the_label_the_placeholder_and_the_text_in_that_order()
    {
        var email = new Fake("n3", "textbox", "Email", Placeholder: "you@example.test", Y: 40);

        // The live node reads as another role, so the role and name query reaches nothing and the next one is tried.
        var labelled = new TestLineHarness([email], [email with { Role = "generic", Placeholder = null }]);
        Assert.Equal("Test line: await Screen.GetByLabel(\"Email\").FillAsync(\"Grace\");", NoteOf(await labelled.RunAsync("type", """{ "target": "n3", "text": "Grace" }""")));
        var placeheld = new TestLineHarness([email], [email with { Role = "generic", Name = null }]);
        Assert.Equal(
            "Test line: await Screen.GetByPlaceholder(\"you@example.test\").FillAsync(\"Grace\");",
            NoteOf(await placeheld.RunAsync("type", """{ "target": "n3", "text": "Grace" }""")));
        var texted = new TestLineHarness([email with { Text = "Email" }], [email with { Role = "generic", Name = null, Placeholder = null, Text = "Email" }]);
        Assert.Equal("Test line: await Screen.GetByText(\"Email\").FillAsync(\"Grace\");", NoteOf(await texted.RunAsync("type", """{ "target": "n3", "text": "Grace" }""")));
    }

    [Fact]
    public async Task Names_the_test_id_of_a_control_with_nothing_else_to_name_it()
    {
        var star = new Fake("n4", "button", TestId: "star", Y: 80);
        var session = new TestLineHarness([Save, star]);
        Assert.Equal("Test line: await Screen.GetByTestId(\"star\").TapAsync();", NoteOf(await session.RunAsync("tap", """{ "target": "n4" }""")));
    }

    [Fact]
    public async Task Never_prints_a_locator_whose_one_match_is_another_node()
    {
        var caption = new Fake("n9", Text: "Save", Y: 300);
        var session = new TestLineHarness([Save, caption], [caption]);
        var text = await session.RunAsync("tap", """{ "target": "n2" }""");
        Assert.Equal("No test line: no locator from its role and name, label, placeholder, text, or test id finds it on the live screen; try locate.", NoteOf(text));
        Assert.Contains("// tap: no test line: no locator from its role and name", session.Lines.Draft(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Keeps_the_line_of_a_control_that_moved_since_the_observation_by_what_it_reads()
    {
        var session = new TestLineHarness([Save], [Save with { Y = 400 }]);
        Assert.Equal("Test line: await Screen.GetByRole(\"button\", \"Save\").TapAsync();", NoteOf(await session.RunAsync("tap", """{ "target": "n2" }""")));
    }

    [Fact]
    public async Task Narrows_twins_to_the_nodes_that_render_before_anything_else()
    {
        var hidden = new Fake("n5", Text: "Sale", Y: 500);
        var sale = new Fake("n6", Text: "Sale", Y: 120);
        var session = new TestLineHarness([hidden, sale], [hidden with { Hidden = true }, sale]);
        Assert.Equal(
            "Test line: await Screen.GetByText(\"Sale\", new TextMatchOptions { Visible = true }).TapAsync();",
            NoteOf(await session.RunAsync("tap", """{ "target": "n6" }""")));
    }

    [Fact]
    public async Task Scopes_a_control_repeated_per_row_under_the_row_it_sits_in()
    {
        var rows = new Fake[]
        {
            new("n10", "listitem", Y: 150, Children: [new Fake("n11", Text: "Offer A", Y: 150), new Fake("n12", "button", "Reserve", Y: 160)]),
            new("n20", "listitem", Y: 190, Children: [new Fake("n21", Text: "Offer B", Y: 190), new Fake("n22", "button", "Reserve", Y: 200)]),
        };
        var session = new TestLineHarness([new Fake("n9", "list", Y: 140, Children: rows)]);
        Assert.Equal(
            "Test line: await Screen.GetByRole(\"listitem\").Filter(\"Offer B\").GetByRole(\"button\", \"Reserve\").TapAsync();",
            NoteOf(await session.RunAsync("tap", """{ "target": "n22" }""")));
    }

    [Fact]
    public async Task Pins_twins_nothing_else_tells_apart_by_position_and_says_so_in_a_comment()
    {
        var first = new Fake("n30", "button", "Add", Y: 100);
        var second = new Fake("n31", "button", "Add", Y: 140);
        var session = new TestLineHarness([first, second]);
        Assert.Equal(
            "Test line: await Screen.GetByRole(\"button\", \"Add\").Nth(1).TapAsync(); // 2 of 2 matches, by position; a unique name or test id holds up better",
            NoteOf(await session.RunAsync("tap", """{ "target": "n31" }""")));
        Assert.Contains("GetByRole(\"button\", \"Add\").First().TapAsync();", NoteOf(await session.RunAsync("tap", """{ "target": "n30" }""")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pins_a_twin_by_its_turn_not_its_old_box_after_the_page_shifted()
    {
        // A banner pushed every row down 40px: the second row's button now sits where the first one was.
        Fake[] observed = [.. new[] { 100, 140, 180 }.Select((y, index) => new Fake("n4" + index, "button", "Add", Y: y))];
        var session = new TestLineHarness(observed, [.. observed.Select(node => node with { Y = node.Y + 40 })]);
        Assert.Matches(@"^Test line: await Screen\.GetByRole\(""button"", ""Add""\)\.Nth\(1\)\.TapAsync\(\);", NoteOf(await session.RunAsync("tap", """{ "target": "n41" }""")));
    }

    [Fact]
    public async Task Pins_a_twin_the_look_lists_out_of_the_observation_order_by_the_box_it_never_left()
    {
        var shadow = new Fake("n45", "button", "Keep", Y: 100);
        var light = new Fake("n46", "button", "Keep", Y: 140);
        var session = new TestLineHarness([shadow, light], [light, shadow]);
        Assert.Matches(@"^Test line: await Screen\.GetByRole\(""button"", ""Keep""\)\.Nth\(1\)\.TapAsync\(\);", NoteOf(await session.RunAsync("tap", """{ "target": "n45" }""")));
    }

    [Fact]
    public async Task Partial_Pins_no_twin_that_both_moved_and_changed_places()
    {
        var first = new Fake("n47", "button", "Keep", Y: 100);
        var second = new Fake("n48", "button", "Keep", Y: 140);
        var session = new TestLineHarness([first, second], [second with { Y = 100 }, first with { Y = 300 }]);
        Assert.Equal(
            "No test line: it shares Screen.GetByRole(\"button\", \"Keep\") with 1 other nodes, and nothing on the screen tells it apart for certain.",
            NoteOf(await session.RunAsync("tap", """{ "target": "n47" }""")));
    }

    [Fact]
    public async Task Pins_no_twin_when_the_live_screen_holds_another_number_of_them_than_the_observation_did()
    {
        Fake[] observed = [.. new[] { 100, 140 }.Select((y, index) => new Fake("n5" + index, "button", "Add", Y: y))];
        var appeared = new Fake("n59", "button", "Add", Y: 60);
        var session = new TestLineHarness(observed, [appeared, .. observed]);
        Assert.Equal(
            "No test line: it shares Screen.GetByRole(\"button\", \"Add\") with 2 other nodes, and nothing on the screen tells it apart for certain.",
            NoteOf(await session.RunAsync("tap", """{ "target": "n51" }""")));
    }

    [Fact]
    public async Task Tries_a_bounded_number_of_locators_then_falls_back_to_the_position()
    {
        Fake[] cards = [.. new[] { 100, 140, 180 }.Select((y, index) => new Fake("n7" + index, "button", "Open", "Open card", Placeholder: "Card", Y: y))];
        var article = new Fake("n82", "article", "Board", Y: 90, Children: cards);
        var group = new Fake("n81", "group", "Lane", Y: 90, Children: [article]);
        var region = new Fake("n80", "region", "Work", Y: 90, Children: [group]);
        var session = new TestLineHarness([region]);
        Assert.Matches(
            @"^Test line: await Screen\.GetByRole\(""button"", ""Open""\)\.Nth\(1\)\.TapAsync\(\); // 2 of 3 matches",
            NoteOf(await session.RunAsync("tap", """{ "target": "n71" }""")));
        Assert.Equal(10, session.Resolves);
    }

    [Fact]
    public async Task Partial_Writes_a_secret_by_its_name_never_by_its_value()
    {
        var key = new Fake("n42", "textbox", "API key", Y: 120);
        var session = new TestLineHarness([key]);
        Assert.Equal(
            "Test line: await Screen.GetByRole(\"textbox\", \"API key\").FillAsync(Secrets.Get(\"apiKey\"));",
            NoteOf(await session.RunAsync("type_secret", """{ "target": "n42", "secret": "apiKey" }""")));
        Assert.DoesNotContain("sk-live-0000", session.Lines.Draft(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Partial_Maps_each_verb_the_port_has_onto_the_locator_method_that_repeats_it()
    {
        var field = new Fake("n50", "textbox", "Notes", Y: 40);
        var plan = new Fake("n51", "combobox", "Plan", Y: 80);
        var terms = new Fake("n52", "checkbox", "Terms", Y: 120);
        var card = new Fake("n53", "listitem", Text: "Card", Y: 160);
        var session = new TestLineHarness([field, plan, terms, card, Save]);
        (string Line, string Tool, string Args)[] lines =
        [
            ("await Screen.GetByRole(\"button\", \"Save\").DoubleTapAsync();", "double_tap", """{ "target": "n2" }"""),
            ("await Screen.GetByRole(\"textbox\", \"Notes\").PressAsync(\"Enter\");", "press", """{ "target": "n50", "key": "Enter" }"""),
            ("await Screen.GetByRole(\"combobox\", \"Plan\").SelectOptionAsync(\"Pro\");", "select", """{ "target": "n51", "value": "Pro" }"""),
            ("await Screen.GetByRole(\"checkbox\", \"Terms\").CheckAsync();", "check", """{ "target": "n52" }"""),
            ("await Screen.GetByRole(\"checkbox\", \"Terms\").UncheckAsync();", "uncheck", """{ "target": "n52" }"""),
            ("await Screen.GetByRole(\"textbox\", \"Notes\").ClearAsync();", "clear", """{ "target": "n50" }"""),
            ("await Screen.GetByRole(\"button\", \"Save\").ScrollIntoViewAsync();", "scroll_to", """{ "target": "n2" }"""),
            ("await App.OpenAsync(\"/settings\");", "navigate", """{ "url": "http://127.0.0.1:4100/settings" }"""),
            ("await App.BackAsync();", "back", "{}"),
        ];
        foreach (var (line, tool, args) in lines)
        {
            Assert.Equal("Test line: " + line, NoteOf(await session.RunAsync(tool, args)));
        }
    }

    [Fact]
    public async Task Partial_Says_why_an_action_has_no_line_for_no_node_a_scroll_a_modifier_and_a_stale_ref()
    {
        var field = new Fake("n60", "textbox", "Notes", Y: 40);
        var session = new TestLineHarness([field, Save]);
        Assert.Matches(@"^No test line: the key went to whatever had focus", NoteOf(await session.RunAsync("press", """{ "key": "Enter" }""")));
        Assert.Matches(@"^No test line: a test action brings its node into view itself", NoteOf(await session.RunAsync("scroll", """{ "direction": "down" }""")));
        Assert.Matches(@"^No test line: scrolling to a text has none", NoteOf(await session.RunAsync("scroll_to", """{ "text": "Row 40" }""")));
        Assert.Matches(@"^No test line: the secret is not a config secret", NoteOf(await session.RunAsync("type_secret", """{ "target": "n60", "secret": "unknown" }""")));
        Assert.Matches(@"^No test line: a locator tap takes no modifier keys", NoteOf(await session.RunAsync("tap", """{ "target": "n2", "modifiers": ["Shift"] }""")));
        Assert.Equal("No test line: node n404 is not on the newest screen.", NoteOf(await session.RunAsync("tap", """{ "target": "n404" }""")));
    }

    [Fact]
    public async Task Prints_no_line_for_typed_text_that_holds_a_secret_value()
    {
        var field = new Fake("n60", "textbox", "Notes", Y: 40);
        var session = new TestLineHarness([field]);
        var text = await session.RunAsync("type", """{ "target": "n60", "text": "sk-live-0000" }""");
        Assert.Equal("No test line: the text holds a secret value; fill it with type_secret for a line.", NoteOf(text));
    }

    [Fact]
    public async Task Prints_and_records_nothing_for_an_action_that_failed()
    {
        var session = new TestLineHarness([Save]);
        await Assert.ThrowsAsync<ToolFailedException>(() =>
            session.RunAsync("tap", """{ "target": "n2" }""", () => throw new ToolFailedException("tap n2 failed: ACTION_FAILED: covered by an overlay")));
        Assert.Matches(@"^No test lines yet\.", session.Lines.Draft());
    }

    [Fact]
    public async Task Records_the_opening_of_the_app_as_App_OpenAsync_and_drafts_the_lines_in_order()
    {
        var session = new TestLineHarness([Save]);
        session.Lines.Opened();
        await session.RunAsync("tap", """{ "target": "n2" }""");
        await session.RunAsync("scroll", """{ "direction": "down" }""");
        Assert.Equal(
            string.Join(
                '\n',
                "Draft test body from this session's actions, in order (2 lines):",
                "",
                "await App.OpenAsync();",
                "await Screen.GetByRole(\"button\", \"Save\").TapAsync();",
                "// scroll: no test line: a test action brings its node into view itself",
                "",
                "Paste it into a test method, and add an Expect after each step that should change the screen."),
            session.Lines.Draft());
    }
}
