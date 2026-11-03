// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using E2E.Reports;

namespace E2E.Cli.Report;

/// <summary>
/// State the list reporter accumulates from the event stream (upstream's <c>report/list-model.ts</c>). Records, not rendered text: the
/// reporter renders at its own indent and width when it prints. Not ported: the live window's <c>CurrentStep</c> and a running
/// test's serial group, executing flag, and start time, and a test line's finished steps, which only the live window collects.
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

/// <summary>A pair that has started and whose result is still to come (upstream's <c>RunningTest</c>).</summary>
internal sealed record RunningTest(FileGroup Group, string Title);

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

    public IReadOnlyList<ReplayNotice> Replays { get; init; } = [];
}
