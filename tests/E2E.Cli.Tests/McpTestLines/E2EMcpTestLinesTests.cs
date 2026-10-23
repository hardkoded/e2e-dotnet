// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Tests.Helpers;

namespace E2E.Cli.Tests.McpTestLines;

/// <summary>
/// Test lines end to end: the built <c>e2e mcp</c> server on a fixture page, driven by a real MCP client. Every action answers with the line that
/// repeats it, a password by its secret and never its value, a button repeated per row scoped under its row, twins in plain divs by their order, a
/// shadow root's twin by the box it never left, and an icon button by its test id; <c>test_lines</c> returns them in order.
/// </summary>
public sealed class E2EMcpTestLinesTests : IClassFixture<E2EMcpTestLinesTests.Served>
{
    private const string Username = "ada@example.test";

    private readonly Served _served;

    public E2EMcpTestLinesTests(Served served)
    {
        _served = served;
    }

    [Fact]
    public async Task Answers_each_action_with_its_test_line_and_the_drafted_lines_come_back_in_order()
    {
        var opened = await _served.InvokeAsync("open_session");
        Assert.False(opened.IsError, opened.Text);

        var navigated = await _served.CallAsync("navigate", new() { ["url"] = _served.App.Url + "/test-lines" });
        Assert.Equal(["navigated to /test-lines", "Test line: await App.OpenAsync(\"/test-lines\");"], navigated.Text.Split('\n').Take(2));
        var screen = (await _served.CallAsync("observe")).Text;

        (string Tool, Dictionary<string, object?> Args, string Line)[] steps =
        [
            ("type", new() { ["target"] = ServedCli.RefOf(screen, "textbox \"Username\""), ["text"] = Username }, "await Screen.GetByRole(\"textbox\", \"Username\").FillAsync(\"" + Username + "\");"),
            ("type_secret", new() { ["target"] = ServedCli.RefOf(screen, "textbox \"Password\""), ["secret"] = "adminPassword" }, "await Screen.GetByRole(\"textbox\", \"Password\").FillAsync(Secrets.Get(\"adminPassword\"));"),
            ("select", new() { ["target"] = ServedCli.RefOf(screen, "combobox \"Plan\""), ["value"] = "Pro" }, "await Screen.GetByRole(\"combobox\", \"Plan\").SelectOptionAsync(\"Pro\");"),
            ("check", new() { ["target"] = ServedCli.RefOf(screen, "checkbox \"Accept terms\"") }, "await Screen.GetByRole(\"checkbox\", \"Accept terms\").CheckAsync();"),
            (
                "tap",
                new() { ["target"] = ServedCli.RefOf(screen[screen.IndexOf("Offer B", StringComparison.Ordinal)..], "button \"Reserve\"") },
                "await Screen.GetByRole(\"listitem\", \"Offer B Reserve\").GetByRole(\"button\", \"Reserve\").TapAsync();"),
            (
                "tap",
                new() { ["target"] = ServedCli.RefOf(screen, "button \"Keep\"") },
                "await Screen.GetByRole(\"button\", \"Keep\").First().TapAsync(); // 1 of 2 matches, by position; a unique name or test id holds up better"),
            (
                "tap",
                new() { ["target"] = ServedCli.RefOf(screen[screen.IndexOf("Row two", StringComparison.Ordinal)..], "button \"Pick\"") },
                "await Screen.GetByRole(\"button\", \"Pick\").Nth(1).TapAsync(); // 2 of 2 matches, by position; a unique name or test id holds up better"),
            ("tap", new() { ["target"] = ServedCli.RefOf(screen, "button testid=\"star\"") }, "await Screen.GetByTestId(\"star\").TapAsync();"),
            ("tap", new() { ["target"] = ServedCli.RefOf(screen, "button \"Sign in\"") }, "await Screen.GetByRole(\"button\", \"Sign in\").TapAsync();"),
        ];
        foreach (var (tool, args, line) in steps)
        {
            var result = await _served.CallAsync(tool, args);
            Assert.False(result.IsError, result.Text);
            Assert.Equal("Test line: " + line, result.Text.Split('\n')[1]);
            Assert.DoesNotContain(Served.AdminPassword, result.Text, StringComparison.Ordinal);
        }

        Assert.Contains(
            "status \"Summary\" text=\"user=ada@example.test password=set plan=Pro terms=true offer=B kept=shadow picked=2 starred=true\"",
            (await _served.CallAsync("observe")).Text,
            StringComparison.Ordinal);

        var drafted = await _served.CallAsync("test_lines");
        Assert.False(drafted.IsError, drafted.Text);
        Assert.DoesNotContain(Served.AdminPassword, drafted.Text, StringComparison.Ordinal);
        var body = drafted.Text.Split('\n').Where(line => line.StartsWith("await ", StringComparison.Ordinal) || line.StartsWith("// ", StringComparison.Ordinal));
        Assert.Equal(["await App.OpenAsync();", "await App.OpenAsync(\"/test-lines\");", .. steps.Select(step => step.Line)], body);
        var closed = await _served.InvokeAsync("close_session");
        Assert.False(closed.IsError, closed.Text);
    }

    /// <summary>One <c>e2e mcp</c> process, its project, and the fixture app, as upstream's <c>beforeAll</c> starts them.</summary>
    public sealed class Served : IAsyncLifetime
    {
        internal const string AdminPassword = "lines-Pa55-9Kq";

        internal FixtureApp App { get; private set; } = null!;

        internal FixtureProject Project { get; private set; } = null!;

        internal ServedCli Cli { get; private set; } = null!;

        internal Task<ToolText> InvokeAsync(string name, Dictionary<string, object?>? args = null) => Cli.InvokeAsync(name, args);

        internal Task<ToolText> CallAsync(string tool, Dictionary<string, object?>? args = null) => Cli.CallAsync(tool, args);

        public async Task InitializeAsync()
        {
            App = FixtureApp.Start();
            Project = new FixtureProject(new Dictionary<string, string>
            {
                ["e2e.config.json"] = FixtureProject.Config(App.Url, $"\"adminPassword\": \"{AdminPassword}\""),
            });
            Cli = await ServedCli.StartAsync(Project.Directory);
        }

        public async Task DisposeAsync()
        {
            await Cli.DisposeAsync();
            Project.Dispose();
            App.Dispose();
        }
    }
}
