// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Reports;

namespace E2E.Cli.Report;

/// <summary>
/// State the list reporter accumulates from the event stream (upstream's <c>report/list-model.ts</c>). Records, not rendered text: the
/// reporter renders at its own indent and width when it prints, and the live tree reads the same records. Not ported: a running test's
/// serial group and executing flag, which only a serial group sets or reads.
/// </summary>
internal sealed class FileGroup(string file, string target)
{
    public string File { get; } = file;

    public string Target { get; } = target;

    /// <summary>How many results the plan counted for the file; null until the plan arrives.</summary>
    public int? Planned { get; set; }

    public List<TestLine> Lines { get; } = [];

    /// <summary>Whether the file's block has printed.</summary>
    public bool Printed { get; set; }
}

/// <summary>A step of a pair as every row of it names it (upstream's <c>StepCall</c>).</summary>
/// <param name="Api">The step's api.</param>
/// <param name="Label">The step's label.</param>
/// <param name="Actor">The session the step runs on when it is not the test's own.</param>
/// <param name="Kind">Only an agent step has model turns to wait on; a <c>Step</c> is a name, which its title alone gives.</param>
/// <param name="Path">
/// The attempt index, then the step indexes of the <c>Step</c> calls around the step, then its own. Steps list in path order, so each follows the
/// <c>Step</c> it ran in however their ends interleave. Null in a stream without step identity.
/// </param>
/// <param name="Groups">The titles of the <c>Step</c> calls the step runs inside, outermost first.</param>
internal record StepCall(
    string Api,
    string Label,
    string? Actor,
    Reports.StepKind Kind,
    IReadOnlyList<int>? Path,
    IReadOnlyList<string> Groups
);

/// <summary>How a step ended, as its <c>End</c> progress reported it (upstream's <c>StepOutcome</c>).</summary>
internal sealed record StepOutcome(StepStatus Status, long DurationMs, int ModelCalls);

/// <summary>
/// A step listed under its test (upstream's <c>ListedStep</c>). Only a <c>Step</c> is listed before it ends, so the steps inside it follow it: it
/// has no outcome until then, or ever when its worker died first.
/// </summary>
internal sealed record ListedStep(StepCall Call, StepOutcome? Outcome)
{
    public string Api => Call.Api;

    public string Label => Call.Label;

    public string? Actor => Call.Actor;

    public Reports.StepKind Kind => Call.Kind;

    public IReadOnlyList<int>? Path => Call.Path;

    public IReadOnlyList<string> Groups => Call.Groups;
}

/// <summary>What the list reporter makes of steps (upstream's <c>report/list-model.ts</c>): whether an event is one the live window shows, and which steps are listed.</summary>
internal static class ListModel
{
    /// <summary>Step events worth a glance; polls and policy decisions stay quiet (upstream's <c>isShownEvent</c>).</summary>
    public static bool IsShownEvent(Reports.StepEvent stepEvent) =>
        stepEvent.Kind is Reports.StepEventKind.Model or Reports.StepEventKind.Engine;

    /// <summary>Whether a step of this kind has a row under its running test in the live window: an agent step or a <c>Step</c>. Other steps are fast and many (upstream's <c>listsUnderTest</c>).</summary>
    public static bool ListsUnderTest(Reports.StepKind kind) => kind is Reports.StepKind.Agent or Reports.StepKind.Step;

    /// <summary>
    /// Whether the end of a step of this kind is a line of its own where no live window lists it: an agent step, which is slow and costs model
    /// calls. A <c>Step</c> is a name, told on the lines of the agent steps inside it (upstream's <c>printsAtEnd</c>).
    /// </summary>
    public static bool PrintsAtEnd(Reports.StepKind kind) => kind == Reports.StepKind.Agent;

    /// <summary>
    /// The listed steps that stay under a test once it is done: its agent steps, a <c>Step</c> that did not pass, and the <c>Step</c> calls around
    /// either. A <c>Step</c> that passed around plain calls would only repeat its title for every green test (upstream's <c>recordedSteps</c>).
    /// </summary>
    public static List<ListedStep> RecordedSteps(IReadOnlyList<ListedStep> steps)
    {
        static bool Own(ListedStep step) =>
            step.Kind != Reports.StepKind.Step || step.Outcome?.Status != StepStatus.Passed;
        var kept = steps.Where(Own).ToList();
        static bool Around(ListedStep group, ListedStep step) =>
            group.Path is { } path
            && step.Path is { } inner
            && path.Count < inner.Count
            && path.Select((index, at) => index == inner[at]).All(same => same);
        return [.. steps.Where(step => Own(step) || kept.Any(inside => Around(step, inside)))];
    }

    /// <summary>Lists <paramref name="step"/> in path order, in place of the running <c>Step</c> it ends. A step without a path goes last (upstream's <c>listStep</c>).</summary>
    public static void ListStep(List<ListedStep> steps, ListedStep step)
    {
        var path = step.Path;
        var at = path is null
            ? -1
            : steps.FindIndex(listed => listed.Path is { } other && ComparePaths(other, path) >= 0);
        if (at == -1 || path is null)
        {
            steps.Add(step);
        }
        else if (ComparePaths(steps[at].Path!, path) == 0)
        {
            steps[at] = step;
        }
        else
        {
            steps.Insert(at, step);
        }
    }

    private static int ComparePaths(IReadOnlyList<int> a, IReadOnlyList<int> b)
    {
        for (var at = 0; at < a.Count && at < b.Count; at++)
        {
            if (a[at] != b[at])
            {
                return a[at] - b[at];
            }
        }

        return a.Count - b.Count;
    }
}

/// <summary>The step a running pair is executing right now (upstream's <c>CurrentStep</c>).</summary>
internal sealed class CurrentStep(StepCall call)
{
    public StepCall Call { get; } = call;

    public string Api => Call.Api;

    public string Label => Call.Label;

    /// <summary>The session the step runs on when it is not the test's own.</summary>
    public string? Actor => Call.Actor;

    /// <summary>Only an agent step has model turns to wait on; a <c>Step</c> is a name, which its title alone gives.</summary>
    public Reports.StepKind Kind => Call.Kind;

    public IReadOnlyList<int>? Path => Call.Path;

    public IReadOnlyList<string> Groups => Call.Groups;

    /// <summary>Model turns and tool calls in stream order. A tool-using executor reports a turn once its tools ran, so the turn follows them here.</summary>
    public List<Reports.StepEvent> Events { get; } = [];

    /// <summary>True while the trace cache has the step, replaying its recorded actions: no model is in the loop, so the wait between events is on the app.</summary>
    public bool Replaying { get; set; }

    /// <summary>What the step waits on since its last event: a screen being read, an action landing. Cleared by the event that reports the phase's end.</summary>
    public Reports.StepActivity? Activity { get; set; }
}

/// <summary>A pair that has started and whose result is still to come (upstream's <c>RunningTest</c>).</summary>
internal sealed class RunningTest(FileGroup group, string title, long startedMs, List<ListedStep> steps)
{
    public FileGroup Group { get; } = group;

    public string Title { get; } = title;

    public long StartedMs { get; } = startedMs;

    /// <summary>
    /// Steps started and not yet ended, by step id, oldest first. A step can open inside another (an <c>onAction</c> hook's screenshot inside an
    /// act) or beside it (<c>Promise.all</c>), so each phase updates the step it names. One an earlier attempt left open stays, so its late end
    /// still finds the <c>Step</c> calls it ran in.
    /// </summary>
    public OrderedDictionary<string, CurrentStep> Open { get; } = [];

    /// <summary>
    /// The index of the attempt running now. A retry starts no new pair, and the body of a timed-out attempt keeps running: a step it left open
    /// may never end, and one it starts after the retry began is not shown.
    /// </summary>
    public int? AttemptIndex { get; set; }

    /// <summary>
    /// The step the live window shows for a pair: the call its running attempt started last of those still open. An open <c>Step</c> has its own
    /// row among the listed steps (upstream's <c>currentStep</c>).
    /// </summary>
    public CurrentStep? Current
    {
        get
        {
            var attempt = AttemptIndex ?? 0;
            CurrentStep? last = null;
            foreach (var step in Open.Values)
            {
                if (step.Kind != Reports.StepKind.Step && (step.Path is null || step.Path[0] == attempt))
                {
                    last = step;
                }
            }

            return last;
        }
    }

    /// <summary>Agent steps and <c>Step</c> calls across every attempt, in the order they started. Shown under the test in the live window while it runs, nested under its line in the file block once it is done.</summary>
    public List<ListedStep> Steps { get; } = steps;
}

/// <summary>The setup step in flight, ticking in the live window: <c>starting service "postgres"</c> (upstream's <c>SetupInFlight</c>). <c>Narrated</c> once a notice printed under it.</summary>
internal sealed record SetupInFlight(string Verb, string Subject, long StartedMs)
{
    public bool Narrated { get; set; }
}

/// <summary>The model and agent named in the run header (upstream's <c>ConfiguredModel</c>).</summary>
internal sealed record ConfiguredModel(string Label, string Agent);

/// <summary>One recording mismatch, rendered under its test (upstream's <c>ReplayNotice</c>). <c>Source</c> is the model that recorded, as <c>IAgentModel.Name</c> spells it.</summary>
internal sealed record ReplayNotice(string Api, string Label, string Source);

/// <summary>One finished test of a file group.</summary>
internal sealed record TestLine
{
    public required string Title { get; init; }

    public required int DeclarationIndex { get; init; }

    public required ResultStatus Status { get; init; }

    public required long DurationMs { get; init; }

    public required Format.AiUsage Usage { get; init; }

    public string? FirstErrorLine { get; init; }

    public string? SkipReason { get; init; }

    /// <summary>The <c>test.fail</c> mark that covered this test's failure: its reason and the covered error's first line. Null when none did.</summary>
    public (string Reason, string ErrorLine)? ExpectedFailure { get; init; }

    /// <summary>The cleanup failures shown as glance lines under a passed or flaky test.</summary>
    public IReadOnlyList<ReportError> CleanupErrors { get; init; } = [];

    public IReadOnlyList<ListedStep> Steps { get; init; } = [];

    public IReadOnlyList<ReplayNotice> Replays { get; init; } = [];
}
