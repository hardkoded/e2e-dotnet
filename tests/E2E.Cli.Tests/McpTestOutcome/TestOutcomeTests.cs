// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using E2E.Cli.Mcp;
using E2E.Internal;

namespace E2E.Cli.Tests.McpTestOutcome;

/// <summary>
/// Part of <c>describe('over e2e mcp')</c> of <c>tests/integration/mcp-test-run.test.ts</c>: "opens a failing test with its code, line, code
/// frame, and nearest nodes, on the screen it failed on", at the text (<c>describeTestOutcome</c>, <c>mcp/test-outcome.ts</c>) and not through
/// the server. The tests are .NET-only because upstream has no unit test of the text; they name the test by its full name, not <c>file:line</c>.
/// </summary>
public sealed class TestOutcomeTests
{
    private static readonly string Root = Path.GetDirectoryName(ThisFile())!;

    [Fact]
    public void Leads_a_failure_with_its_code_line_code_frame_nearest_nodes_and_last_steps()
    {
        var failure = Fail();
        var steps = new[] { Step(0, "app.open", "/", StepStatus.Passed), Step(1, "locator.tap", "getByRole(\"button\", name: \"Save\")", StepStatus.Failed, "LOCATOR_NOT_FOUND") };
        var evidence = new FailureEvidence { Url = "/profile", Candidates = ["#n4 button \"Safe\""] };

        var lines = TestOutcome.Describe("Shop.ProfileTests.SavesTheName", failure, TimeSpan.FromMilliseconds(1_500), steps, evidence, text => text, Root).Split('\n');

        Assert.Equal("Test \"Shop.ProfileTests.SavesTheName\" failed after 1.50s; 2 steps ran.", lines[0]);
        Assert.Equal("LOCATOR_NOT_FOUND: no node matches getByRole(\"button\", name: \"Save\")", lines[1]);
        Assert.Matches(@"^At TestOutcomeTests\.cs:\d+:\d+$", lines[2]);
        Assert.Contains(lines, line => line.Contains("throw new E2EException(\"LOCATOR_NOT_FOUND\"", StringComparison.Ordinal) && line.StartsWith("  ", StringComparison.Ordinal));
        Assert.Contains("URL at failure: /profile", lines);
        var nodes = Array.IndexOf(lines, "On screen, closest to what the locator asked for:");
        Assert.Equal("  #n4 button \"Safe\"", lines[nodes + 1]);
        Assert.Contains("Steps:", lines);
        Assert.Contains("  1. ✓ app.open /", lines);
        Assert.Contains("  2. ✗ locator.tap getByRole(\"button\", name: \"Save\") LOCATOR_NOT_FOUND", lines);
        Assert.Contains("open_session {test: \"Shop.ProfileTests.SavesTheName\"}.", lines[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void Lists_only_the_last_steps_of_a_long_run_and_names_the_failing_steps_line_when_the_error_has_none()
    {
        var steps = Enumerable.Range(0, 20).Select(index => Step(index, "app.back", string.Empty, StepStatus.Passed))
            .Append(Step(20, "locator.tap", "x", StepStatus.Failed, "ACTION_FAILED", new SourceLocation("TestOutcomeTests.cs", 1, 1)))
            .ToList();

        var text = TestOutcome.Describe("A.B.C", new E2EException("ACTION_FAILED", "nope"), TimeSpan.FromMilliseconds(40), steps, null, text => text, Root);

        Assert.Contains("\nAt TestOutcomeTests.cs:1:1\n", text, StringComparison.Ordinal);
        Assert.Contains("\nSteps, the last 15 of 21:\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("  6. ", text, StringComparison.Ordinal);
        Assert.Contains("  7. ✓ app.back\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Tells_a_test_that_did_not_fail_without_a_story()
    {
        var text = TestOutcome.Describe("A.B.C", null, TimeSpan.FromMilliseconds(40), [Step(0, "app.open", "/", StepStatus.Passed)], null, text => text, Root);

        Assert.Equal("Test \"A.B.C\" ended without failing after 40ms; 1 step ran.\nThe session is on the screen the test ended on, shown below.", text);
    }

    [Fact]
    public void Redacts_the_text_with_the_sessions_redactor()
    {
        var text = TestOutcome.Describe("A.B.C", new E2EException("ACTION_FAILED", "sk-live-0000 is wrong"), TimeSpan.Zero, [], null, text => text.Replace("sk-live-0000", "<secret:apiKey>", StringComparison.Ordinal), Root);

        Assert.DoesNotContain("sk-live-0000", text, StringComparison.Ordinal);
        Assert.Contains("ACTION_FAILED: <secret:apiKey> is wrong", text, StringComparison.Ordinal);
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;

    private static E2EException Fail()
    {
        try
        {
            throw new E2EException("LOCATOR_NOT_FOUND", "no node matches getByRole(\"button\", name: \"Save\")");
        }
        catch (E2EException failure)
        {
            return failure;
        }
    }

    private static StepRecord Step(int index, string api, string label, StepStatus status, string? code = null, SourceLocation? source = null) => new()
    {
        Id = "s" + index,
        Index = index,
        Kind = StepKind.Locator,
        Api = api,
        Label = label,
        Status = status,
        StartedAt = DateTimeOffset.UnixEpoch,
        Source = source,
        Error = code is null ? null : new SerializedError("test", code, "failed", false, null),
    };
}
