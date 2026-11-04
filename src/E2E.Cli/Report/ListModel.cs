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

/// <summary>One finished agent step of a pair, as the <c>End</c> progress reported it (upstream's <c>FinishedStep</c>).</summary>
internal sealed record FinishedStep(string Api, string Label, StepStatus Status, long DurationMs, int ModelCalls);

/// <summary>Whether an event is one the live window shows (upstream's <c>isShownEvent</c>): step events worth a glance; polls and policy decisions stay quiet.</summary>
internal static class ListModel
{
    public static bool IsShownEvent(Reports.StepEvent stepEvent) => stepEvent.Kind is Reports.StepEventKind.Model or Reports.StepEventKind.Engine;
}

/// <summary>The step a running pair is executing right now (upstream's <c>CurrentStep</c>).</summary>
internal sealed class CurrentStep(string api, string label, Reports.StepKind kind)
{
    public string Api { get; } = api;

    public string Label { get; } = label;

    /// <summary>Only an agent step has model turns to wait on.</summary>
    public Reports.StepKind Kind { get; } = kind;

    /// <summary>Model turns and tool calls in stream order. A tool-using executor reports a turn once its tools ran, so the turn follows them here.</summary>
    public List<Reports.StepEvent> Events { get; } = [];

    /// <summary>True while the trace cache has the step, replaying its recorded actions: no model is in the loop, so the wait between events is on the app.</summary>
    public bool Replaying { get; set; }

    /// <summary>What the step waits on since its last event: a screen being read, an action landing. Cleared by the event that reports the phase's end.</summary>
    public Reports.StepActivity? Activity { get; set; }
}

/// <summary>A pair that has started and whose result is still to come (upstream's <c>RunningTest</c>).</summary>
internal sealed class RunningTest(FileGroup group, string title, long startedMs, List<FinishedStep> steps)
{
    public FileGroup Group { get; } = group;

    public string Title { get; } = title;

    public long StartedMs { get; } = startedMs;

    public CurrentStep? Current { get; set; }

    /// <summary>Finished agent steps across every attempt, oldest first. Shown under the test in the live window while it runs, nested under its line in the file block once it is done.</summary>
    public List<FinishedStep> Steps { get; } = steps;
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

    public IReadOnlyList<FinishedStep> Steps { get; init; } = [];

    public IReadOnlyList<ReplayNotice> Replays { get; init; } = [];
}
