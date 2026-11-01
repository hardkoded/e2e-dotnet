// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Cli.Tests.Helpers;

namespace E2E.Cli.Tests.McpTestLines;

/// <summary>
/// Test lines end to end: the built <c>e2e mcp</c> server on a fixture page, driven by a real MCP client. Every action
/// answers with the line that repeats it, a password by its secret and never its value, a button repeated per row scoped under
/// its row (the web engine names a list item from its content, so the row is found by that name), twins in plain divs by their order, a shadow root's twin by its place among the matches, and an icon button by its test
/// id; <c>test_lines</c> returns them in order. Pasted into a test class, that draft passes a run.
/// </summary>
public sealed class E2EMcpTestLinesTests : IAsyncLifetime
{
    private const string Username = "ada@example.test";

    private const string Password = "lines-Pa55-9Kq";

    private FixtureApp _app = null!;
    private FixtureProject _project = null!;
    private ServedCli _cli = null!;

    public async Task InitializeAsync()
    {
        _app = FixtureApp.Start();
        _project = new FixtureProject(new Dictionary<string, string>
        {
            ["e2e.config.json"] = FixtureProject.Config(_app.Url, $"\"adminPassword\": \"{Password}\""),
        });
        _cli = await ServedCli.StartAsync(_project.Directory);
    }

    public async Task DisposeAsync()
    {
        await _cli.DisposeAsync();
        _project.Dispose();
        _app.Dispose();
    }

    [Fact]
    public async Task Answers_each_action_with_its_test_line_and_the_drafted_lines_pass_a_run()
    {
        var opened = await _cli.InvokeAsync("open_session");
        Assert.False(opened.IsError, opened.Text);

        var navigated = await _cli.CallAsync("navigate", new() { ["url"] = _app.Url + "/test-lines" });
        Assert.Equal(["Navigated to " + _app.Url + "/test-lines.", "Test line: await App.OpenAsync(\"/test-lines\");"], navigated.Text.Split('\n').Take(2));
        var screen = (await _cli.CallAsync("observe")).Text;
        var afterOfferB = screen[screen.IndexOf("Offer B", StringComparison.Ordinal)..];
        var afterRowTwo = screen[screen.IndexOf("Row two", StringComparison.Ordinal)..];

        (string Tool, Dictionary<string, object?> Args, string Line)[] steps =
        [
            ("type", new() { ["target"] = ServedCli.RefOf(screen, "textbox \"Username\""), ["text"] = Username }, $"await Screen.GetByRole(\"textbox\", \"Username\").FillAsync(\"{Username}\");"),
            ("type_secret", new() { ["target"] = ServedCli.RefOf(screen, "textbox \"Password\""), ["secret"] = "adminPassword" }, "await Screen.GetByRole(\"textbox\", \"Password\").FillAsync(Secrets.Get(\"adminPassword\"));"),
            ("select", new() { ["target"] = ServedCli.RefOf(screen, "combobox \"Plan\""), ["value"] = "Pro" }, "await Screen.GetByRole(\"combobox\", \"Plan\").SelectOptionAsync(\"Pro\");"),
            ("check", new() { ["target"] = ServedCli.RefOf(screen, "checkbox \"Accept terms\"") }, "await Screen.GetByRole(\"checkbox\", \"Accept terms\").CheckAsync();"),
            (
                "tap",
                new() { ["target"] = ServedCli.RefOf(afterOfferB, "button \"Reserve\"") },
                "await Screen.GetByRole(\"listitem\", \"Offer B Reserve\").GetByRole(\"button\", \"Reserve\").TapAsync();"),
            (
                "tap",
                new() { ["target"] = ServedCli.RefOf(screen, "button \"Keep\"") },
                "await Screen.GetByRole(\"button\", \"Keep\").First().TapAsync(); // 1 of 2 matches, by position; a unique name or test id holds up better"),
            (
                "tap",
                new() { ["target"] = ServedCli.RefOf(afterRowTwo, "button \"Pick\"") },
                "await Screen.GetByRole(\"button\", \"Pick\").Nth(1).TapAsync(); // 2 of 2 matches, by position; a unique name or test id holds up better"),
            ("tap", new() { ["target"] = ServedCli.RefOf(screen, "testid=\"star\"") }, "await Screen.GetByTestId(\"star\").TapAsync();"),
            ("tap", new() { ["target"] = ServedCli.RefOf(screen, "button \"Sign in\"") }, "await Screen.GetByRole(\"button\", \"Sign in\").TapAsync();"),
        ];
        foreach (var (tool, args, line) in steps)
        {
            var result = await _cli.CallAsync(tool, args);
            Assert.False(result.IsError, result.Text);
            Assert.Equal("Test line: " + line, result.Text.Split('\n')[1]);
            Assert.DoesNotContain(Password, result.Text, StringComparison.Ordinal);
        }

        Assert.Contains(
            "status \"Summary\" text=\"user=ada@example.test password=set plan=Pro terms=true offer=B kept=shadow picked=2 starred=true\"",
            (await _cli.CallAsync("observe")).Text,
            StringComparison.Ordinal);

        var drafted = await _cli.CallAsync("test_lines");
        Assert.False(drafted.IsError, drafted.Text);
        Assert.DoesNotContain(Password, drafted.Text, StringComparison.Ordinal);
        Assert.Contains("Paste it into a test method, and add an Expect after each step that should change the screen.", drafted.Text, StringComparison.Ordinal);
        string[] body = [.. drafted.Text.Split('\n').Where(line => line.StartsWith("await ", StringComparison.Ordinal) || line.StartsWith("// ", StringComparison.Ordinal))];
        Assert.Equal(["await App.OpenAsync();", "await App.OpenAsync(\"/test-lines\");", .. steps.Select(step => step.Line)], body);
        await _cli.InvokeAsync("close_session");

        // The real proof: the draft, pasted as is, replays the flow in a run.
        var run = await DraftedTestRun.RunAsync(
            _project.Directory,
            body,
            "await Expect.That(Screen.GetByRole(\"status\", \"Summary\")).ToHaveTextAsync(\"user=ada@example.test password=set plan=Pro terms=true offer=B kept=shadow picked=2 starred=true\");");
        Assert.True(run.Code == 0, run.Output);
    }
}
