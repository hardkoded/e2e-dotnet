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
    public void Tells_a_failure_marked_as_expected_as_expected_with_its_reason()
    {
        var steps = new[] { Step(0, "app.open", "/", StepStatus.Passed), Step(1, "expect.toHaveText", "Loads", StepStatus.Failed, "ASSERTION_FAILED") };

        var lines = TestOutcome.Describe("A.B.C", new E2EException("ASSERTION_FAILED", "no"), TimeSpan.FromMilliseconds(40), steps, null, text => text, Root, expectedFailure: "the counter starts at one").Split('\n');

        Assert.Equal("Test \"A.B.C\" failed as expected after 40ms; 2 steps ran.", lines[0]);
        Assert.Equal("Expected to fail: the counter starts at one", lines[1]);
        Assert.Equal("ASSERTION_FAILED: no", lines[2]);
    }

    [Fact]
    public void Tells_a_hook_failure_under_a_mark_as_failed_not_as_expected()
    {
        var failure = new TestException("HOOK_FAILED", "beforeAll failed: database is down");

        var lines = TestOutcome.Describe("A.B.C", failure, TimeSpan.FromMilliseconds(40), [], null, text => text, Root, RunPhase.BeforeAll, null, "the database gap").Split('\n');

        Assert.Equal("Test \"A.B.C\" failed after 40ms; 0 steps ran.", lines[0]);
        Assert.Equal("Expected to fail: the database gap", lines[1]);
        Assert.Equal("HOOK_FAILED in beforeAll: beforeAll failed: database is down", lines[2]);
    }

    [Fact]
    public void Tells_a_test_that_passed_though_marked_as_expected_to_fail()
    {
        var failure = new TestException("UNEXPECTED_PASS", "test is marked test.fail (\"the home page is broken\") but passed");
        var steps = new[] { Step(0, "app.open", "/", StepStatus.Passed) };

        var lines = TestOutcome.Describe("A.B.C", failure, TimeSpan.FromMilliseconds(40), steps, null, text => text, Root, expectedFailure: "the home page is broken").Split('\n');

        Assert.Equal("Test \"A.B.C\" failed after 40ms; 1 step ran.", lines[0]);
        Assert.Equal("Expected to fail: the home page is broken", lines[1]);
        Assert.Equal("UNEXPECTED_PASS: test is marked test.fail (\"the home page is broken\") but passed", lines[2]);
    }

    [Fact]
    public void Tells_a_test_that_did_not_fail_without_a_story()
    {
        var text = TestOutcome.Describe("A.B.C", null, TimeSpan.FromMilliseconds(40), [Step(0, "app.open", "/", StepStatus.Passed)], null, text => text, Root);

        Assert.Equal("Test \"A.B.C\" ended without failing after 40ms; 1 step ran.\nThe session is on the screen the test ended on, shown below.", text);
    }

    [Fact]
    public void Names_the_phase_of_a_hook_that_failed_and_tells_a_test_that_ran_no_step()
    {
        var text = TestOutcome.Describe("Shop.Hooks.NeedsTheDatabase", new TestException("HOOK_FAILED", "beforeAll failed: database is down"), TimeSpan.FromMilliseconds(40), [], null, text => text, Root, RunPhase.BeforeAll);

        var lines = text.Split('\n');
        Assert.Equal("Test \"Shop.Hooks.NeedsTheDatabase\" failed after 40ms; 0 steps ran.", lines[0]);
        Assert.Equal("HOOK_FAILED in beforeAll: beforeAll failed: database is down", lines[1]);
        Assert.DoesNotContain("Steps:", lines);
    }

    [Fact]
    public void Tells_a_test_that_timed_out_and_lists_the_failures_that_landed_after_the_first()
    {
        var failure = new TestException("TEST_TIMEOUT", "test timed out after 1000 ms in phase body");
        var late = new E2EException("UNHANDLED_REJECTION", "a promise rejected after the test's body ended: late failure");

        var text = TestOutcome.Describe("A.B.C", failure, TimeSpan.FromMilliseconds(1_040), [Step(0, "app.open", "/", StepStatus.Passed)], null, text => text, Root, RunPhase.Body, [late]);

        var lines = text.Split('\n');
        Assert.Equal("Test \"A.B.C\" timed out after 1.04s; 1 step ran.", lines[0]);
        Assert.Equal("TEST_TIMEOUT: test timed out after 1000 ms in phase body", lines[1]);
        Assert.Contains("Also failed: UNHANDLED_REJECTION: a promise rejected after the test's body ended: late failure", lines);
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
