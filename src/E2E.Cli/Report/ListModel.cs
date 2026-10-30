// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

namespace E2E.Cli.Report;

/// <summary>
/// State the list reporter accumulates from the event stream (upstream's <c>report/list-model.ts</c>). Records, not rendered text: the
/// reporter renders at its own indent and width when it prints. Not ported: the live window's <c>RunningTest</c> and
/// <c>CurrentStep</c>, a test line's finished steps and replay notices, and <c>ConfiguredModel</c>, which only the replay notices read.
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
}
