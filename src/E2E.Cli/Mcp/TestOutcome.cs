// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using E2E.Cli.Report;
using E2E.Internal;

namespace E2E.Cli.Mcp;

/// <summary>
/// The lines a session that ran a test opens with (upstream's <c>mcp/test-outcome.ts</c>): how the test ended, the failure with the line it
/// came from and the code around it, the nodes on screen nearest what a failed locator asked for, the steps that ran, and what to do next
/// from the screen the session shows below them.
/// </summary>
internal static class TestOutcome
{
    /// <summary>Steps the text lists; a longer run shows its last ones, where it stopped.</summary>
    private const int MaxSteps = 15;

    /// <summary>Frame lines are clipped here, not at a terminal's width: the agent reads the line it fixes whole.</summary>
    private const int FrameChars = 160;

    private static readonly Colors Plain = new(false);

    /// <summary>
    /// The opening lines of a session that ran the test named <paramref name="name"/>, before its summary and catalog. <paramref name="steps"/> and <paramref name="evidence"/> are the session's; <paramref name="elapsed"/> is
    /// how long the run took, set-up and tear-down included. <paramref name="expectedFailure"/> is the reason the test is marked as expected to fail, or
    /// null. Upstream's <c>describeTestOutcome</c>.
    /// </summary>
    public static string Describe(string name, Exception? failure, TimeSpan elapsed, IReadOnlyList<StepRecord> steps, FailureEvidence? evidence, Func<string, string> redact, string projectRoot, RunPhase phase = RunPhase.Body, IReadOnlyList<Exception>? secondary = null, string? expectedFailure = null)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(redact);
        ArgumentNullException.ThrowIfNull(projectRoot);
        var error = failure is null ? null : ErrorSerializer.Serialize(failure, projectRoot, redact);

        // A body that fails as its test is marked expected fails "as expected"; a hook's failure is not the marked failure, so it reads "failed".
        var expected = expectedFailure is not null && phase == RunPhase.Body && error is { Code: not "UNEXPECTED_PASS" };
        var status = failure is null ? "ended without failing" : failure is E2EException { Code: "TEST_TIMEOUT" } ? "timed out" : expected ? "failed as expected" : "failed";
        List<string> lines = ["Test \"" + name + "\" " + status + " after " + Format.FormatTime(elapsed) + "; " + MarkdownText.Plural(steps.Count, "step") + " ran."];
        if (expectedFailure is not null)
        {
            lines.Add("Expected to fail: " + expectedFailure);
        }

        if (error is not null)
        {
            lines.Add(error.Code + PhaseText(phase) + ": " + error.Message);

            // The line the failure unwound through, else the failing step's own call.
            var source = error.Source ?? steps.LastOrDefault(step => step.Status != StepStatus.Passed)?.Source;
            if (source is not null)
            {
                lines.Add("At " + source.File + ":" + source.Line.ToString(CultureInfo.InvariantCulture) + ":" + source.Column.ToString(CultureInfo.InvariantCulture));
                lines.AddRange(CodeFrame.Render(Path.GetFullPath(source.File, projectRoot), source.Line, source.Column, Plain, FrameChars).Select(row => "  " + row));
            }
        }

        if (evidence is not null)
        {
            if (evidence.Url is not null)
            {
                lines.Add("URL at failure: " + evidence.Url);
            }

            if (evidence.Candidates is { Count: > 0 } candidates)
            {
                lines.Add("On screen, closest to what the locator asked for:");
                lines.AddRange(candidates.Select(candidate => "  " + candidate));
            }
        }

        foreach (var other in secondary ?? [])
        {
            var alsoFailed = ErrorSerializer.Serialize(other, projectRoot, redact);
            lines.Add("Also failed: " + alsoFailed.Code + ": " + alsoFailed.Message);
        }

        // A pass needs no story; where a run stopped does.
        if (failure is not null && steps.Count > 0)
        {
            var shown = steps.Skip(Math.Max(0, steps.Count - MaxSteps)).ToList();
            lines.Add(shown.Count < steps.Count ? "Steps, the last " + shown.Count.ToString(CultureInfo.InvariantCulture) + " of " + steps.Count.ToString(CultureInfo.InvariantCulture) + ":" : "Steps:");
            lines.AddRange(shown.Select(step => "  " + StepLine(step)));
        }

        lines.Add(NextSteps(name, failure));
        return redact(string.Join('\n', lines));
    }

    /// <summary>What names where a failure landed: nothing for the body, <c> in beforeAll</c> for a hook.</summary>
    private static string PhaseText(RunPhase phase) => phase switch
    {
        RunPhase.BeforeAll => " in beforeAll",
        RunPhase.BeforeEach => " in beforeEach",
        _ => string.Empty,
    };

    /// <summary><c>7. ✗ locator.fill getByLabel('Name') LOCATOR_NOT_FOUND (tests/a.e2e.ts:13)</c>.</summary>
    private static string StepLine(StepRecord step)
    {
        var hook = step.Phase is { } phase ? " in " + (phase == StepPhase.BeforeEach ? "beforeEach" : "afterEach") : string.Empty;
        var failed = step.Status == StepStatus.Passed ? string.Empty : " " + (step.Error?.Code ?? StatusText(step.Status));
        var at = step.Source is null ? string.Empty : " (" + step.Source.File + ":" + step.Source.Line.ToString(CultureInfo.InvariantCulture) + ")";
        return new StringBuilder()
            .Append((step.Index + 1).ToString(CultureInfo.InvariantCulture)).Append(". ").Append(Glyph(step.Status)).Append(' ').Append(step.Api)
            .Append(step.Label.Length == 0 ? string.Empty : " " + step.Label).Append(hook).Append(failed).Append(at)
            .ToString();
    }

    private static string Glyph(StepStatus status) => status switch
    {
        StepStatus.Passed => "✓",
        StepStatus.Cancelled => "–",
        _ => "✗",
    };

    private static string StatusText(StepStatus status) => status switch
    {
        StepStatus.TimedOut => "timed-out",
        _ => status.ToString().ToLowerInvariant(),
    };

    /// <summary>What the session is for now, and how to run the test again.</summary>
    private static string NextSteps(string name, Exception? failure) => failure is null
        ? "The session is on the screen the test ended on, shown below."
        : "The session is on the screen where the test stopped, shown below: observe and locate from here, and act out the test's next lines to check them too, then fix the test file. "
            + "Its tear-down already ran. To run the fixed test from the start, close_session, then open_session {test: \"" + name + "\"}.";
}
