// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using E2E.Cli.Mcp;
using E2E.Cli.Tests.Helpers;
using E2E.Engine;

namespace E2E.Cli.Tests.McpTestLines;

/// <summary>
/// Upstream's <c>describe('SessionTestLines')</c> of <c>mcp-test-lines.test.ts</c>: the test line a session action answers with, built from the
/// target as the newest observation listed it, tried against the live screen (here a table of what each locator matches), printed only when it
/// reaches the target alone, narrowed when it reaches twins too, and collected by <c>test_lines</c>.
/// </summary>
public sealed class SessionTestLinesTests
{
    private static readonly FakeNode Save = new("n2", 40, "button", "Save");

    /// <summary>A session over one screen: <c>screen</c> is what observe lists, <c>matches</c> what each locator (by its rendered expression) finds on the live screen.</summary>
    private sealed class Harness
    {
        private readonly Dictionary<string, CatalogTool> _tools = [];
        private int _resolves;

        public Harness(IReadOnlyList<FakeNode> screen, IReadOnlyDictionary<string, FakeNode[]> matches, Func<string, JsonElement, string>? lead = null)
        {
            var root = new FakeNode("root", 0, "document", "Page", Children: screen);
            var observation = new Observation { Route = "/", Roots = [root.Listed()] };
            Lines = new SessionTestLines(new TestLineOptions
            {
                Query = SessionCatalog.LocateQuery,
                Newest = () => observation,
                Resolve = (expression, _) =>
                {
                    Interlocked.Increment(ref _resolves);
                    if (ResolveFailure is { } failure)
                    {
                        throw failure;
                    }

                    return Task.FromResult<IReadOnlyList<SemanticNode>>([.. matches.GetValueOrDefault(expression.Describe(), []).Select(node => node.Read())]);
                },
                Redact = text => text.Replace("sk-live-0000", "<secret:apiKey>", StringComparison.Ordinal),
                AppBase = new AppBase("http://127.0.0.1:4100/", "http://127.0.0.1:4100"),
                Fills = new TestLineFills { Credentials = [("admin", "ada@example.test", "admin.password")], Secrets = ["apiKey"] },
            });
            foreach (var name in new[] { "tap", "double_tap", "type", "type_secret", "press", "select", "check", "uncheck", "clear", "scroll_to", "scroll", "navigate", "back" })
            {
                var fake = new CatalogTool
                {
                    InputSchema = JsonSerializer.SerializeToElement(new { type = "object" }),
                    Execute = (args, _) => Task.FromResult<object?>((lead ?? Lead)(name, args) + "\n\nScreen changes since revision r1"),
                };
                _tools[name] = Lines.Wrap(name, fake);
            }

            _tools["test_lines"] = Lines.Tool();
        }

        public SessionTestLines Lines { get; }

        /// <summary>When set, every locator the session tries on the live screen fails with it.</summary>
        public Exception? ResolveFailure { get; set; }

        /// <summary>How many locators the session tried on the live screen.</summary>
        public int Resolves => Volatile.Read(ref _resolves);

        /// <summary>Replaces a tool with one that fails the way a refused action does.</summary>
        public void Fail(string name, string message) =>
            _tools[name] = Lines.Wrap(name, new CatalogTool { InputSchema = JsonSerializer.SerializeToElement(new { type = "object" }), Execute = (_, _) => throw new ToolFailedException(message) });

        /// <summary>Runs one tool through the session's <c>call</c> path and returns its text.</summary>
        public async Task<string> RunAsync(string tool, Dictionary<string, object?> args) =>
            (string)(await _tools[tool].Execute!(JsonSerializer.SerializeToElement(args), CancellationToken.None))!;

        private static string Lead(string name, JsonElement args) =>
            name == "navigate" ? "Navigated to " + args.GetProperty("url").GetString() + "." : "Tapped #" + (args.TryGetProperty("target", out var target) ? target.GetString() : "") + ".";
    }

    private static Harness Session(FakeNode[] screen, Dictionary<string, FakeNode[]> matches) => new(screen, matches);

    /// <summary>The line a result carries after its lead, or why it has none.</summary>
    private static string NoteOf(string text) => text.Split('\n').ElementAtOrDefault(1) ?? "";

    [Fact]
    public async Task Answers_every_grammar_action_but_the_observation_and_the_pixels_so_a_new_verb_needs_a_line_or_a_reason()
    {
        var engine = new FakeEngine { Also = EngineCapabilities.Scroll | EngineCapabilities.History | EngineCapabilities.Screenshot };
        await using var context = await SessionContext.StartAsync(engine);
        var catalog = SessionCatalog.Create(context, engine.Capabilities, hasSecrets: true);
        var wrapper = new SessionTestLines(new TestLineOptions { Query = SessionCatalog.LocateQuery, Newest = () => null, Resolve = (_, _) => Task.FromResult<IReadOnlyList<SemanticNode>>([]), Redact = text => text });
        var names = catalog.Entries.Select(pair => pair.Key).ToList();
        Assert.Equal(
            ["observe", "tap", "double_tap", "type", "press", "select", "check", "uncheck", "clear", "scroll_to", "scroll", "navigate", "back", "screenshot", "type_secret", "locate", "test_lines"],
            names);

        // A tool the lines wrap again comes back as a new tool; one they leave alone comes back as it is.
        var unwrapped = catalog.Entries.Where(pair => ReferenceEquals(wrapper.Wrap(pair.Key, pair.Value), pair.Value)).Select(pair => pair.Key).Order(StringComparer.Ordinal);
        Assert.Equal(["locate", "observe", "screenshot", "test_lines"], unwrapped);
    }

    [Fact]
    public async Task Answers_an_action_with_its_line_after_the_lead_and_test_lines_collects_it()
    {
        var session = Session([Save], new() { ["getByRole(\"button\", \"Save\")"] = [Save] });
        var text = await session.RunAsync("tap", new() { ["target"] = "n2" });
        Assert.Matches(@"^Tapped #n2\.\nTest line: await Screen\.GetByRole\(""button"", ""Save""\)\.TapAsync\(\);\n\n", text);
        var draft = await session.RunAsync("test_lines", []);
        Assert.Contains("\nawait Screen.GetByRole(\"button\", \"Save\").TapAsync();\n", draft, StringComparison.Ordinal);
        Assert.Contains("(1 lines)", draft, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Falls_back_to_the_label_the_placeholder_and_the_text_in_that_order()
    {
        var email = new FakeNode("n3", 40, "textbox", "Email", Placeholder: "you@example.test");
        var labelled = Session([email], new() { ["getByLabel(\"Email\")"] = [email] });
        Assert.Equal("Test line: await Screen.GetByLabel(\"Email\").FillAsync(\"Grace\");", NoteOf(await labelled.RunAsync("type", new() { ["target"] = "n3", ["text"] = "Grace" })));
        var placeheld = Session([email], new() { ["getByPlaceholder(\"you@example.test\")"] = [email] });
        Assert.Equal("Test line: await Screen.GetByPlaceholder(\"you@example.test\").FillAsync(\"Grace\");", NoteOf(await placeheld.RunAsync("type", new() { ["target"] = "n3", ["text"] = "Grace" })));
    }

    [Fact]
    public async Task Reads_the_test_id_off_the_live_node_for_a_control_with_nothing_else_to_name_it()
    {
        var star = new FakeNode("n4", 80, "button", TestId: "star");
        var session = Session([Save, star], new() { ["getByRole(\"button\")"] = [Save, star], ["getByTestId(\"star\")"] = [star] });
        Assert.Equal("Test line: await Screen.GetByTestId(\"star\").TapAsync();", NoteOf(await session.RunAsync("tap", new() { ["target"] = "n4" })));
    }

    [Fact]
    public async Task Never_prints_a_locator_whose_one_match_is_another_node()
    {
        var caption = new FakeNode("n9", 300, Text: "Save");
        var session = Session([Save, caption], new() { ["getByText(\"Save\")"] = [caption] });
        var text = await session.RunAsync("tap", new() { ["target"] = "n2" });
        Assert.Equal("No test line: no locator from its role and name, label, placeholder, text, or test id finds it on the live screen; try locate.", NoteOf(text));
        Assert.Contains("// tap: no test line: no locator from its role and name", await session.RunAsync("test_lines", []), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Keeps_the_line_of_a_control_that_moved_since_the_observation_by_what_it_reads()
    {
        var moved = Save with { Y = 400 };
        var session = Session([Save], new() { ["getByRole(\"button\", \"Save\")"] = [moved] });
        Assert.Equal("Test line: await Screen.GetByRole(\"button\", \"Save\").TapAsync();", NoteOf(await session.RunAsync("tap", new() { ["target"] = "n2" })));
    }

    [Fact]
    public async Task Narrows_twins_to_the_nodes_that_render_before_anything_else()
    {
        var hidden = new FakeNode("n5", 500, Text: "Sale");
        var sale = new FakeNode("n6", 120, Text: "Sale");
        var session = Session([hidden, sale], new() { ["getByText(\"Sale\")"] = [hidden, sale], ["getByText(\"Sale\", visible: true)"] = [sale] });
        Assert.Equal(
            "Test line: await Screen.GetByText(\"Sale\", new TextMatchOptions { Visible = true }).TapAsync();",
            NoteOf(await session.RunAsync("tap", new() { ["target"] = "n6" })));
    }

    [Fact]
    public async Task Narrows_twins_whose_name_reads_like_an_option_to_the_nodes_that_render()
    {
        var hidden = new FakeNode("n5", 500, "button", "Add new item");
        var shown = new FakeNode("n6", 120, "button", "Add new item");
        var session = Session([hidden, shown], new() { ["getByRole(\"button\", \"Add new item\")"] = [hidden, shown], ["getByRole(\"button\", \"Add new item\", visible: true)"] = [shown] });
        Assert.Equal(
            "Test line: await Screen.GetByRole(\"button\", \"Add new item\", new RoleOptions { Visible = true }).TapAsync();",
            NoteOf(await session.RunAsync("tap", new() { ["target"] = "n6" })));
    }

    [Fact]
    public async Task Keeps_a_multi_line_locator_failure_on_one_line_so_the_draft_stays_a_comment()
    {
        var session = Session([Save], new());
        session.ResolveFailure = new InvalidOperationException("locator failed\n  - waiting for it\n  - still waiting");
        var text = await session.RunAsync("tap", new() { ["target"] = "n2" });
        Assert.Equal(2, text.Split('\n').TakeWhile(line => !line.StartsWith("Screen changes", StringComparison.Ordinal)).Count(line => line.Length > 0));
        Assert.Contains("locator failed - waiting for it - still waiting", text, StringComparison.Ordinal);
        var draft = (await session.RunAsync("test_lines", [])).Split('\n');
        var at = Array.FindIndex(draft, line => line.StartsWith("// tap:", StringComparison.Ordinal));
        Assert.True(at >= 0 && !draft.Skip(at + 1).Any(line => line.StartsWith("  -", StringComparison.Ordinal)), string.Join('\n', draft));
    }

    [Fact]
    public async Task Scopes_a_control_repeated_per_row_under_the_row_it_sits_in()
    {
        var reserveA = new FakeNode("n12", 160, "button", "Reserve");
        var reserveB = new FakeNode("n22", 200, "button", "Reserve");
        FakeNode[] rows =
        [
            new("n10", 150, "listitem", "Offer A Reserve", Children: [new("n11", 150, Text: "Offer A"), reserveA]),
            new("n20", 190, "listitem", "Offer B Reserve", Children: [new("n21", 190, Text: "Offer B"), reserveB]),
        ];
        var session = Session(
            [new("n9", 140, "list", Children: rows)],
            new()
            {
                ["getByRole(\"button\", \"Reserve\")"] = [reserveA, reserveB],
                ["getByText(\"Reserve\")"] = [reserveA, reserveB],
                ["getByRole(\"button\", \"Reserve\", visible: true)"] = [reserveA, reserveB],
                ["getByRole(\"listitem\").filter(hasText: \"Offer B\").getByRole(\"button\", \"Reserve\")"] = [reserveB],
            });
        Assert.Equal(
            "Test line: await Screen.GetByRole(\"listitem\").Filter(\"Offer B\").GetByRole(\"button\", \"Reserve\").TapAsync();",
            NoteOf(await session.RunAsync("tap", new() { ["target"] = "n22" })));
    }

    [Fact]
    public async Task Pins_twins_nothing_else_tells_apart_by_position_and_says_so_in_a_comment()
    {
        var first = new FakeNode("n30", 100, "button", "Add");
        var second = new FakeNode("n31", 140, "button", "Add");
        var session = Session([first, second], new() { ["getByRole(\"button\", \"Add\")"] = [first, second] });
        Assert.Equal(
            "Test line: await Screen.GetByRole(\"button\", \"Add\").Nth(1).TapAsync(); // 2 of 2 matches, by position; a unique name or test id holds up better",
            NoteOf(await session.RunAsync("tap", new() { ["target"] = "n31" })));
        Assert.Contains("GetByRole(\"button\", \"Add\").First().TapAsync();", NoteOf(await session.RunAsync("tap", new() { ["target"] = "n30" })), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pins_a_twin_by_its_turn_not_its_old_box_after_the_page_shifted()
    {
        // A banner pushed every row down 40px: the second row's button now sits where the first one was.
        FakeNode[] observed = [.. new[] { 100, 140, 180 }.Select((y, index) => new FakeNode("n4" + index, y, "button", "Add"))];
        var live = observed.Select(node => node with { Y = node.Y + 40 }).ToArray();
        var session = Session(observed, new() { ["getByRole(\"button\", \"Add\")"] = live });
        Assert.StartsWith("Test line: await Screen.GetByRole(\"button\", \"Add\").Nth(1).TapAsync();", NoteOf(await session.RunAsync("tap", new() { ["target"] = "n41" })), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pins_a_twin_the_engine_lists_out_of_the_observation_order_by_the_box_it_never_left()
    {
        // A shadow root's button is listed in place by observe, after the light DOM by the locator engine.
        var shadow = new FakeNode("n45", 100, "button", "Keep");
        var light = new FakeNode("n46", 140, "button", "Keep");
        var session = Session([shadow, light], new() { ["getByRole(\"button\", \"Keep\")"] = [light, shadow] });
        Assert.StartsWith("Test line: await Screen.GetByRole(\"button\", \"Keep\").Nth(1).TapAsync();", NoteOf(await session.RunAsync("tap", new() { ["target"] = "n45" })), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pins_no_twin_that_both_moved_and_changed_places_nor_reads_a_test_id_off_one()
    {
        var first = new FakeNode("n47", 100, "button", "Keep");
        var second = new FakeNode("n48", 140, "button", "Keep", TestId: "second");
        var session = Session(
            [first, second],
            new() { ["getByRole(\"button\", \"Keep\")"] = [second with { Y = 100 }, first with { Y = 300 }], ["getByTestId(\"second\")"] = [second with { Y = 100 }] });
        Assert.Equal(
            "No test line: it shares Screen.GetByRole(\"button\", \"Keep\") with 1 other nodes, and nothing on the screen tells it apart for certain.",
            NoteOf(await session.RunAsync("tap", new() { ["target"] = "n47" })));
    }

    [Fact]
    public async Task Pins_no_twin_when_the_live_screen_holds_another_number_of_them_than_the_observation_did()
    {
        FakeNode[] observed = [.. new[] { 100, 140 }.Select((y, index) => new FakeNode("n5" + index, y, "button", "Add"))];
        var appeared = new FakeNode("n59", 60, "button", "Add");
        var session = Session(observed, new() { ["getByRole(\"button\", \"Add\")"] = [appeared, .. observed] });
        Assert.Equal(
            "No test line: it shares Screen.GetByRole(\"button\", \"Add\") with 2 other nodes, and nothing on the screen tells it apart for certain.",
            NoteOf(await session.RunAsync("tap", new() { ["target"] = "n51" })));
    }

    [Fact]
    public async Task Tries_a_bounded_number_of_locators_each_a_snapshot_on_a_device_then_falls_back_to_the_position()
    {
        FakeNode[] cards = [.. new[] { 100, 140, 180 }.Select((y, index) => new FakeNode("n7" + index, y, "button", "Open", "Open card", Placeholder: "Card"))];
        var article = new FakeNode("n82", 90, "article", "Board", Children: cards);
        var group = new FakeNode("n81", 90, "group", "Lane", Children: [article]);
        var region = new FakeNode("n80", 90, "region", "Work", Children: [group]);
        var session = Session([region], new() { ["getByRole(\"button\", \"Open\")"] = cards });
        Assert.Matches(
            @"^Test line: await Screen\.GetByRole\(""button"", ""Open""\)\.Nth\(1\)\.TapAsync\(\); // 2 of 3 matches",
            NoteOf(await session.RunAsync("tap", new() { ["target"] = "n71" })));
        Assert.Equal(10, session.Resolves);
    }

    [Fact]
    public async Task Writes_credentials_and_secrets_by_name_never_by_value()
    {
        var user = new FakeNode("n40", 40, "textbox", "Username");
        var password = new FakeNode("n41", 80, "textbox", "Password");
        var key = new FakeNode("n42", 120, "textbox", "API key");
        var session = Session(
            [user, password, key],
            new()
            {
                ["getByRole(\"textbox\", \"Username\")"] = [user],
                ["getByRole(\"textbox\", \"Password\")"] = [password],
                ["getByRole(\"textbox\", \"API key\")"] = [key],
            });
        Assert.Equal(
            "Test line: await Screen.GetByRole(\"textbox\", \"Username\").FillAsync(Credentials.User(\"admin\").Username);",
            NoteOf(await session.RunAsync("type", new() { ["target"] = "n40", ["text"] = "ada@example.test" })));
        Assert.Equal(
            "Test line: await Screen.GetByRole(\"textbox\", \"Password\").FillAsync(Credentials.User(\"admin\").Password);",
            NoteOf(await session.RunAsync("type_secret", new() { ["target"] = "n41", ["secret"] = "admin.password" })));
        Assert.Equal(
            "Test line: await Screen.GetByRole(\"textbox\", \"API key\").FillAsync(Secrets.Get(\"apiKey\"));",
            NoteOf(await session.RunAsync("type_secret", new() { ["target"] = "n42", ["secret"] = "apiKey" })));
    }

    [Fact]
    public async Task Maps_every_verb_onto_the_locator_method_that_repeats_it()
    {
        var field = new FakeNode("n50", 40, "textbox", "Notes");
        var plan = new FakeNode("n51", 80, "combobox", "Plan");
        var terms = new FakeNode("n52", 120, "checkbox", "Terms");
        var card = new FakeNode("n53", 160, "listitem", "Card", "Card");
        var session = Session(
            [field, plan, terms, card, Save],
            new()
            {
                ["getByRole(\"textbox\", \"Notes\")"] = [field],
                ["getByRole(\"combobox\", \"Plan\")"] = [plan],
                ["getByRole(\"checkbox\", \"Terms\")"] = [terms],
                ["getByText(\"Card\")"] = [card],
                ["getByRole(\"button\", \"Save\")"] = [Save],
            });
        (string Line, string Tool, Dictionary<string, object?> Args)[] lines =
        [
            ("await Screen.GetByRole(\"button\", \"Save\").DoubleTapAsync();", "double_tap", new() { ["target"] = "n2" }),
            ("await Screen.GetByRole(\"textbox\", \"Notes\").PressAsync(\"Enter\");", "press", new() { ["target"] = "n50", ["key"] = "Enter" }),
            ("await Screen.GetByRole(\"combobox\", \"Plan\").SelectOptionAsync(\"Pro\");", "select", new() { ["target"] = "n51", ["value"] = "Pro" }),
            ("await Screen.GetByRole(\"checkbox\", \"Terms\").CheckAsync();", "check", new() { ["target"] = "n52" }),
            ("await Screen.GetByRole(\"checkbox\", \"Terms\").UncheckAsync();", "uncheck", new() { ["target"] = "n52" }),
            ("await Screen.GetByRole(\"textbox\", \"Notes\").ClearAsync();", "clear", new() { ["target"] = "n50" }),
            ("await Screen.GetByRole(\"button\", \"Save\").ScrollIntoViewAsync();", "scroll_to", new() { ["target"] = "n2" }),
            ("await App.OpenAsync(\"/settings\");", "navigate", new() { ["url"] = "http://127.0.0.1:4100/settings" }),
            ("await App.BackAsync();", "back", []),
        ];
        foreach (var (line, tool, args) in lines)
        {
            Assert.Equal("Test line: " + line, NoteOf(await session.RunAsync(tool, args)));
        }
    }

    [Fact]
    public async Task Says_why_an_action_has_no_line_no_node_a_repeated_key_a_scroll_a_keyboard_fallback()
    {
        var field = new FakeNode("n60", 40, "textbox", "Notes");
        var canvas = new FakeNode("n61", 80, "image", "Pad");
        var session = Session([field, canvas], new() { ["getByRole(\"textbox\", \"Notes\")"] = [field], ["getByRole(\"image\", \"Pad\")"] = [canvas] });
        Assert.Equal("No test line: the value went to the focused field; type into a listed node by id for a line.", NoteOf(await session.RunAsync("type", new() { ["text"] = "x" })));
        Assert.Matches(@"^No test line: the key went to whatever had focus", NoteOf(await session.RunAsync("press", new() { ["key"] = "Enter" })));
        Assert.Matches(@"^No test line: a test action brings its node into view itself", NoteOf(await session.RunAsync("scroll", new() { ["direction"] = "down" })));
        Assert.Matches(@"^No test line: scrolling to a text has none", NoteOf(await session.RunAsync("scroll_to", new() { ["text"] = "Row 40" })));
        Assert.Matches(@"^No test line: the secret is neither", NoteOf(await session.RunAsync("type_secret", new() { ["target"] = "n60", ["secret"] = "unknown" })));
        Assert.Equal("No test line: #n404 is not on the newest screen.", NoteOf(await session.RunAsync("tap", new() { ["target"] = "n404" })));
    }

    [Fact]
    public async Task Prints_and_records_nothing_for_an_action_that_failed()
    {
        var session = Session([Save], new() { ["getByRole(\"button\", \"Save\")"] = [Save] });
        session.Fail("tap", "tap n2 failed: ACTION_FAILED: covered by an overlay");
        var error = await Assert.ThrowsAsync<ToolFailedException>(() => session.RunAsync("tap", new() { ["target"] = "n2" }));
        Assert.Equal("tap n2 failed: ACTION_FAILED: covered by an overlay", error.Message);
        Assert.Matches(@"^No test lines yet\.", await session.RunAsync("test_lines", []));
    }

    [Fact]
    public async Task Records_the_opening_of_the_app_as_App_OpenAsync_and_drafts_the_lines_in_order()
    {
        var session = Session([Save], new() { ["getByRole(\"button\", \"Save\")"] = [Save] });
        session.Lines.Opened();
        await session.RunAsync("tap", new() { ["target"] = "n2" });
        await session.RunAsync("scroll", new() { ["direction"] = "down" });
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
