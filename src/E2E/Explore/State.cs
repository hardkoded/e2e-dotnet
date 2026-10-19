// Copyright 2026 TesterArmy.
// Modified by Dario Kondratiuk.
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using E2E.Internal;

namespace E2E;

/// <summary>A finding as the tool reports it. The state assigns its identity, position, and time.</summary>
internal sealed record FindingInput(
    ReportExploreFindingKind Kind,
    int Severity,
    string Title,
    string Expected,
    string Actual,
    IReadOnlyList<string> Reproduction,
    string? Path = null,
    string? ArtifactId = null);

/// <summary>
/// The record of one exploration: the goal, its budgets, the steps the agent planned and ran, and the findings it reported.
/// One instance is shared by the planner, the explorer's finding tool, the exploration body, and the progress listeners,
/// and its snapshot is <see cref="ExploreOutcome.Explore"/>. The state is the report boundary: every free text is bounded
/// to the report's ceilings here, and nowhere else. A finding's text comes from the model and may echo a secret value, so the
/// redactor runs over it here, before it reaches the outcome, the report, a progress event, or the next step's parameters. Each change is announced to the listeners <see cref="Subscribe"/> registered, as
/// <see cref="ExploreProgress"/>.
/// </summary>
internal sealed class ExploreState(string goal, ExploreBudgets budgets, TimeProvider? time = null, Redactor? redactor = null)
{
    /// <summary>Ceilings the report keeps, as upstream's schema does.</summary>
    public const int MaxTitleChars = 200;

    private const int MaxInstructionChars = 2_000;
    private const int MaxSummaryChars = 4_000;
    private const int MaxDetailChars = 2_000;
    private const int MaxReproductionSteps = 20;
    private const int MaxReproductionChars = 500;
    private const int MaxPathChars = 2_048;

    private readonly object _gate = new();
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly Redactor _redactor = redactor ?? Redactor.None;
    private readonly List<ReportExploreStep> _steps = [];
    private readonly List<ReportExploreFinding> _findings = [];
    private readonly List<ExploreListener> _listeners = [];
    private ReportExploreEnded _ended = ReportExploreEnded.Aborted;
    private string? _summary;
    private OpenStep? _open;

    public string Goal { get; } = goal;

    public ExploreBudgets Budgets { get; } = budgets;

    public IReadOnlyList<ReportExploreStep> Steps
    {
        get
        {
            lock (_gate)
            {
                return [.. _steps];
            }
        }
    }

    public IReadOnlyList<ReportExploreFinding> Findings
    {
        get
        {
            lock (_gate)
            {
                return [.. _findings];
            }
        }
    }

    /// <summary>Why exploration stopped. <see cref="ReportExploreEnded.Aborted"/> until <see cref="End"/> says otherwise.</summary>
    public ReportExploreEnded Ended
    {
        get
        {
            lock (_gate)
            {
                return _ended;
            }
        }
    }

    /// <summary>The agent's closing assessment, once it gave one.</summary>
    public string? Summary
    {
        get
        {
            lock (_gate)
            {
                return _summary;
            }
        }
    }

    /// <summary>The findings of kind <see cref="ReportExploreFindingKind.Issue"/>.</summary>
    public IReadOnlyList<ReportExploreFinding> Issues => [.. Findings.Where(finding => finding.Kind == ReportExploreFindingKind.Issue)];

    /// <summary>Registers a listener for every change from here on.</summary>
    public void Subscribe(ExploreListener listener)
    {
        lock (_gate)
        {
            _listeners.Add(listener);
        }
    }

    /// <summary>Announces the exploration beginning: the goal and its budgets.</summary>
    public void Start() => Notify(new ExploreProgress.Started(Goal, Budgets));

    /// <summary>Announces that the planner is deciding what comes next: a step, or the closing assessment.</summary>
    public void Planning(bool closing) => Notify(new ExploreProgress.Planning(closing));

    /// <summary>Opens the next step. The index is one-based.</summary>
    public OpenedStep BeginStep(string title, string instruction)
    {
        lock (_gate)
        {
            if (_open is not null)
            {
                throw new InvalidOperationException("exploration step " + _open.Index.ToString(CultureInfo.InvariantCulture) + " is still open");
            }

            var now = _time.GetUtcNow();
            _open = new OpenStep(_steps.Count + 1, Clip(title, MaxTitleChars), Clip(instruction, MaxInstructionChars), now);
            var step = new OpenedStep(_open.Index, _open.Title, _open.Instruction);
            Notify(new ExploreProgress.StepStarted(step));
            return step;
        }
    }

    /// <summary>Closes the open step with its outcome.</summary>
    public ReportExploreStep EndStep(ReportExploreStepStatus status, string? summary = null, string? errorCode = null)
    {
        lock (_gate)
        {
            var open = _open ?? throw new InvalidOperationException("no exploration step is open");
            _open = null;
            var step = new ReportExploreStep(
                open.Index,
                open.Title,
                open.Instruction,
                status,
                open.StartedAt,
                TimeSpan.FromMilliseconds(Math.Max(0, (_time.GetUtcNow() - open.StartedAt).TotalMilliseconds)),
                string.IsNullOrEmpty(summary) ? null : Clip(summary, MaxSummaryChars),
                errorCode);
            _steps.Add(step);
            Notify(new ExploreProgress.StepFinished(step));
            return step;
        }
    }

    /// <summary>Records one finding against the step in progress, with its evidence screenshot when one was kept.</summary>
    public ReportExploreFinding AddFinding(FindingInput input)
    {
        // Redacted before the lock, so a slow match never holds the listeners up.
        var title = _redactor.Redact(input.Title);
        var expected = _redactor.Redact(input.Expected);
        var actual = _redactor.Redact(input.Actual);
        var reproduction = input.Reproduction.Take(MaxReproductionSteps).Select(_redactor.Redact).ToList();
        lock (_gate)
        {
            var now = _time.GetUtcNow();
            var finding = new ReportExploreFinding(
                Guid.CreateVersion7(now).ToString(),
                _findings.Count,
                _open?.Index,
                input.Kind,
                input.Severity,
                Clip(title, MaxTitleChars),
                Clip(expected, MaxDetailChars),
                Clip(actual, MaxDetailChars),
                [.. reproduction.Select(entry => Clip(entry, MaxReproductionChars))],
                now,
                input.Path is null ? null : Clip(input.Path, MaxPathChars),
                input.ArtifactId);
            _findings.Add(finding);
            Notify(new ExploreProgress.FindingReported(finding));
            return finding;
        }
    }

    /// <summary>Closes the record: why exploration stopped and, when the agent gave one, its closing assessment.</summary>
    public void End(ReportExploreEnded ended, string? summary = null)
    {
        lock (_gate)
        {
            _ended = ended;
            _summary = string.IsNullOrWhiteSpace(summary) ? null : Clip(summary, MaxSummaryChars);
            Notify(new ExploreProgress.Finished(ended, _summary));
        }
    }

    /// <summary>
    /// How many steps in a row made no progress, newest first: failed or blocked without recording a finding. A failed step that
    /// reported a defect did its job, and a step that ended at its budget is a time-boxed charter, not a failure; neither breaks
    /// the streak nor adds to it.
    /// </summary>
    public int ConsecutiveFailures()
    {
        lock (_gate)
        {
            var productive = _findings.Select(finding => finding.Step).ToHashSet();
            var count = 0;
            for (var index = _steps.Count - 1; index >= 0; index--)
            {
                var step = _steps[index];
                if (step.Status == ReportExploreStepStatus.Passed)
                {
                    break;
                }

                if (step.Status == ReportExploreStepStatus.Exhausted || productive.Contains(step.Index))
                {
                    continue;
                }

                count++;
            }

            return count;
        }
    }

    public ReportExplore Snapshot()
    {
        lock (_gate)
        {
            return new ReportExplore(Goal, Budgets, _ended, [.. _steps], [.. _findings], _summary);
        }
    }

    /// <summary>
    /// Trims and bounds free text so the report stays inside its ceilings. Counted in code points, as upstream's schema counts,
    /// so a cut never splits a surrogate pair.
    /// </summary>
    public static string Clip(string text, int max)
    {
        var trimmed = text.Trim();
        var runes = trimmed.EnumerateRunes().ToList();
        if (runes.Count <= max)
        {
            return trimmed;
        }

        var cut = new StringBuilder();
        foreach (var rune in runes.Take(max - 1))
        {
            cut.Append(rune.ToString());
        }

        return cut.Append('…').ToString();
    }

    private void Notify(ExploreProgress progress)
    {
        ExploreListener[] listeners;
        lock (_gate)
        {
            listeners = [.. _listeners];
        }

        foreach (var listener in listeners)
        {
            listener(progress);
        }
    }

    private sealed record OpenStep(int Index, string Title, string Instruction, DateTimeOffset StartedAt);
}
